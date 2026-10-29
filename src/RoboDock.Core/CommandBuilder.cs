using System.Globalization;

namespace RoboDock.Core;

/// <summary>
/// 把 <see cref="RobocopyOptions"/> 变成真实的命令行参数。
/// <para>
/// 执行时用 <see cref="BuildArguments"/> 返回的参数数组交给
/// <c>ProcessStartInfo.ArgumentList</c>，由系统负责转义，不会出错；
/// <see cref="BuildDisplayCommand"/> 只用于界面预览和"复制命令"按钮。
/// </para>
/// </summary>
public static class CommandBuilder
{
    public const string ExecutableName = "robocopy";

    /// <summary>构造参数数组（不含 exe 名）。</summary>
    public static IReadOnlyList<string> BuildArguments(
        RobocopyOptions options,
        RobocopyMode mode = RobocopyMode.Execute)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = new List<string>
        {
            options.Source,
            options.Destination,
        };

        args.AddRange(options.Strategy.ToSwitches());

        // /COPY:DAT 是默认值，只有用户改动过才显式写出
        if (BuildCopyFlags(options) is { } copyFlags)
        {
            args.Add($"/COPY:{copyFlags}");
        }

        // 显式声明目录时间戳，便于日志阅读（与 robocopy 默认一致）
        args.Add("/DCOPY:DA");

        if (options.MultiThread is int threads && threads > 1)
        {
            args.Add($"/MT:{threads}");
        }

        if (options.Restartable) args.Add("/Z");
        if (options.UnbufferedIo) args.Add("/J");
        if (options.BackupMode) args.Add("/B");

        // robocopy 默认 /R:1000000 /W:30——无限重试是新手最常见的"卡住"原因，必须覆盖
        args.Add($"/R:{options.RetryCount}");
        args.Add($"/W:{options.RetryWaitSeconds}");

        if (options.ExcludedFiles.Count > 0)
        {
            args.Add("/XF");
            args.AddRange(options.ExcludedFiles);
        }

        if (options.ExcludedDirectories.Count > 0)
        {
            args.Add("/XD");
            args.AddRange(options.ExcludedDirectories);
        }

        if (!string.IsNullOrWhiteSpace(options.LogFile))
        {
            args.Add(options.AppendLog ? $"/LOG+:{options.LogFile}" : $"/LOG:{options.LogFile}");
            if (options.Tee) args.Add("/TEE");
        }

        if (mode == RobocopyMode.Preview) args.Add("/L");

        return args;
    }

    /// <summary>构造用于展示和复制的完整命令行字符串。</summary>
    public static string BuildDisplayCommand(
        RobocopyOptions options,
        RobocopyMode mode = RobocopyMode.Execute)
    {
        var args = BuildArguments(options, mode);
        return ExecutableName + " " + string.Join(' ', args.Select(QuoteForDisplay));
    }

    /// <summary>
    /// 按 Windows 命令行规则为展示加引号。注意结尾反斜杠必须成对，
    /// 否则 <c>"C:\dir\"</c> 会把引号转义掉——这是手写命令行的经典坑。
    /// </summary>
    public static string QuoteForDisplay(string value)
    {
        if (value.Length == 0) return "\"\"";

        var needsQuote = value.Any(char.IsWhiteSpace) || value.Contains('"');
        if (!needsQuote) return value;

        var inner = value.Replace("\"", "\\\"");
        if (inner.EndsWith('\\')) inner += '\\';
        return "\"" + inner + "\"";
    }

    /// <summary>把 <c>/COPY</c> 的开关字母拼出来；等于默认值 DAT 时返回 null。</summary>
    private static string? BuildCopyFlags(RobocopyOptions options)
    {
        var flags = new System.Text.StringBuilder(4);
        if (options.CopyData) flags.Append('D');
        if (options.CopyAttributes) flags.Append('A');
        if (options.CopyTimestamps) flags.Append('T');
        if (options.CopySecurity) flags.Append('S');
        if (options.CopyOwner) flags.Append('O');

        var text = flags.ToString();
        return text == "DAT" ? null : text;
    }
}
