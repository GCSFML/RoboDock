using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace RoboDock.Core;

/// <summary>一次运行的进度快照。</summary>
public sealed record RunProgress(
    int FilesDone,
    int? FilesTotal,
    double CurrentFilePercent,
    string? CurrentFile)
{
    /// <summary>整体进度 0–1；没有总数时返回 null。</summary>
    public double? Overall => FilesTotal is > 0
        ? Math.Clamp((FilesDone + CurrentFilePercent / 100d) / FilesTotal.Value, 0d, 1d)
        : null;
}

/// <summary>一次运行的结果。</summary>
public sealed record RobocopyRunResult(
    int ExitCode,
    RobocopySummary? Summary,
    PreviewResult Preview,
    IReadOnlyList<string> Lines,
    DateTime StartedAt,
    DateTime FinishedAt,
    bool Cancelled)
{
    public TimeSpan Duration => FinishedAt - StartedAt;

    public ExitCodeInfo Verdict => ExitCodeTranslator.Translate(ExitCode);

    /// <summary>摘要缺失通常意味着参数错误或进程被中断。</summary>
    public bool IsUsable => Summary is not null;
}

/// <summary>
/// 启动并监控 robocopy。
/// <para>参数通过 <see cref="ProcessStartInfo.ArgumentList"/> 传递，由系统负责转义，
/// 因此路径里有空格、中文、引号都不会出问题。</para>
/// </summary>
public sealed class RobocopyRunner
{
    private const int MaxStoredLines = 50_000;
    private const int MaxStoredEntries = 200_000;

    public async Task<RobocopyRunResult> RunAsync(
        RobocopyOptions options,
        RobocopyMode mode = RobocopyMode.Execute,
        IProgress<RunProgress>? progress = null,
        Action<string>? onLine = null,
        CancellationToken cancellationToken = default,
        Language? language = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var startInfo = CreateStartInfo(options, mode, language ?? DetectUiLanguage());

        var startedAt = DateTime.Now;
        var lines = new List<string>();
        var entries = new List<PreviewEntry>();
        var filesDone = 0;
        var currentFile = (string?)null;
        var currentPercent = 0d;

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "无法启动 robocopy，请确认系统为 Windows 且 Robocopy.exe 可用。", ex);
        }

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch
            {
                // 进程可能刚好结束，忽略
            }
        });

        // stderr 必须和 stdout 并发读，否则管道写满会互相卡死
        var stderrLines = new List<string>();
        var stderrTask = Task.Run(async () =>
        {
            await foreach (var line in ReadLinesAsync(process.StandardError.BaseStream, cancellationToken))
            {
                stderrLines.Add(line);
            }
        }, cancellationToken);

        await foreach (var line in ReadLinesAsync(process.StandardOutput.BaseStream, cancellationToken))
        {
            if (lines.Count < MaxStoredLines) lines.Add(line);
            onLine?.Invoke(line);

            if (ProgressParser.TryParsePercent(line, out var percent))
            {
                currentPercent = percent;
                progress?.Report(new RunProgress(filesDone, null, currentPercent, currentFile));
                continue;
            }

            if (PreviewParser.ParseLine(line) is { } entry)
            {
                if (entries.Count < MaxStoredEntries) entries.Add(entry);

                if (entry.Kind == PreviewKind.CopyFile)
                {
                    filesDone++;
                    currentFile = entry.Path;
                    currentPercent = 0d;
                }

                progress?.Report(new RunProgress(filesDone, null, currentPercent, currentFile));
            }
        }

        try
        {
            await stderrTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 与 stdout 一样：取消时静默结束
        }

        foreach (var line in stderrLines)
        {
            if (lines.Count < MaxStoredLines) lines.Add(line);
            onLine?.Invoke(line);
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 用户取消：进程已被终止，继续收集结果
        }

        var finishedAt = DateTime.Now;
        var summary = SummaryParser.Parse(lines);

        return new RobocopyRunResult(
            process.ExitCode,
            summary,
            new PreviewResult(entries),
            lines,
            startedAt,
            finishedAt,
            cancellationToken.IsCancellationRequested);
    }

    /// <summary>
    /// 构造启动信息。
    /// <para>
    /// robocopy 的界面语言<b>跟随控制台输出代码页</b>（实测：<c>chcp 65001</c> → 英文，
    /// <c>chcp 936</c> → 中文）。所以用 <c>cmd /c "chcp N &amp; robocopy ..."</c> 包一层，
    /// 就能让 robocopy 自己的输出跟着界面语言走，顺带把输出编码也定下来。
    /// </para>
    /// </summary>
    public static ProcessStartInfo CreateStartInfo(RobocopyOptions options, RobocopyMode mode, Language language)
    {
        var arguments = CommandBuilder.BuildArguments(options, mode);

        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // 路径里带 cmd 元字符时不套 cmd（避免转义出错），代价只是语言不可控
        if (arguments.Any(ContainsCmdMetaCharacter))
        {
            startInfo.FileName = CommandBuilder.ExecutableName;
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
            return startInfo;
        }

        var codePage = language == Language.En ? 65001 : OemCodePage();
        var command = $"chcp {codePage} >nul & robocopy "
                      + string.Join(' ', arguments.Select(CommandBuilder.QuoteForDisplay));

        startInfo.FileName = "cmd.exe";
        startInfo.Arguments = "/c " + command;
        return startInfo;
    }

    private static bool ContainsCmdMetaCharacter(string value)
        => value.IndexOfAny(['&', '|', '<', '>', '^', '%', '!']) >= 0;

    private static int OemCodePage()
    {
        try
        {
            return CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
        }
        catch
        {
            return 936;
        }
    }

    /// <summary>没显式指定时按系统界面语言决定。</summary>
    private static Language DetectUiLanguage()
        => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? Language.Zh : Language.En;

    /// <summary>
    /// 按行读取子进程输出。编码要"攒够字节再判断"——
    /// robocopy 开头的表头可能全是 ASCII，只看第一块会把 GBK 误判成 UTF-8。
    /// </summary>
    public static async IAsyncEnumerable<string> ReadLinesAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buffer = new byte[8192];
        var pending = new List<byte>(16_384);
        Encoding? encoding = null;
        Decoder? decoder = null;
        var previousEndedWithCr = false;

        while (true)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;   // 用户取消：正常结束读取
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException)
            {
                yield break;
            }

            if (read == 0) break;

            pending.AddRange(buffer.AsSpan(0, read).ToArray());

            if (encoding is null)
            {
                // 关键：不能拿"第一块数据"就下结论。
                // robocopy 开头的表头可能是纯 ASCII（那条 --- 线），此时判成 UTF-8
                // 会让后面的 GBK 中文全部变成乱码。所以要攒够再看。
                if (pending.Count < 256 || (!ContainsNonAscii(pending) && pending.Count < 65_536))
                {
                    continue;
                }

                encoding = OutputDecoder.Detect(pending.ToArray());
                decoder = encoding.GetDecoder();
            }

            if (encoding is UnicodeEncoding)
            {
                // UTF-16 时按字节找换行不可靠：攒起来最后一次性解码
                continue;
            }

            // robocopy 用 '\r' 原地刷新进度（"文件名\r 45%\r100%"），所以 \r 也要算行结束符
            var lastTerminator = LastTerminator(pending);
            if (lastTerminator < 0) continue;

            var chunk = pending.GetRange(0, lastTerminator + 1).ToArray();
            pending.RemoveRange(0, lastTerminator + 1);

            var text = Decode(decoder!, chunk);
            if (previousEndedWithCr && text.Length > 0 && text[0] == '\n')
            {
                text = text[1..];       // 上一块以 \r 结尾，这一块开头的 \n 属于同一个换行
            }

            previousEndedWithCr = chunk[^1] == (byte)'\r';

            foreach (var line in SplitLines(text))
            {
                yield return line;
            }
        }

        if (pending.Count > 0)
        {
            encoding ??= OutputDecoder.Detect(pending.ToArray());
            decoder ??= encoding.GetDecoder();

            foreach (var line in SplitLines(Decode(decoder, pending.ToArray())))
            {
                yield return line;
            }
        }
    }

    /// <summary>字节流里是否已经出现过非 ASCII 字符（说明编码探测可以下结论了）。</summary>
    private static bool ContainsNonAscii(List<byte> bytes)
    {
        foreach (var b in bytes)
        {
            if (b >= 0x80) return true;
        }

        return false;
    }

    /// <summary>最后一个行结束符的位置（<c>\r</c> 或 <c>\n</c>）。</summary>
    private static int LastTerminator(List<byte> bytes)
    {
        for (var i = bytes.Count - 1; i >= 0; i--)
        {
            if (bytes[i] is (byte)'\r' or (byte)'\n') return i;
        }

        return -1;
    }

    /// <summary>用有状态的 <see cref="Decoder"/> 解码一段字节（可跨块处理半个字符）。</summary>
    private static string Decode(Decoder decoder, byte[] bytes)
    {
        var chars = new char[decoder.GetCharCount(bytes, 0, bytes.Length)];
        var written = decoder.GetChars(bytes, 0, bytes.Length, chars, 0);
        return new string(chars, 0, written);
    }

    /// <summary>按 <c>\r</c>、<c>\n</c>、<c>\r\n</c> 切行。</summary>
    private static IEnumerable<string> SplitLines(string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is not ('\r' or '\n')) continue;

            yield return text[start..i];

            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            start = i + 1;
        }

        if (start < text.Length)
        {
            yield return text[start..];
        }
    }
}
