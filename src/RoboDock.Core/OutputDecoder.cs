using System.Globalization;
using System.Text;

namespace RoboDock.Core;

/// <summary>
/// 猜测子进程输出的编码并解码。
/// <para>
/// robocopy 用控制台的代码页输出：可能是 UTF-8（chcp 65001），
/// 也可能是 GBK(936)、Big5(950)、Shift-JIS(932) 等本地代码页。
/// </para>
/// <para>
/// <b>不能只按系统 OEM 代码页兜底</b>：在英文 Windows 上 OEM 是 437，
/// 拿它解 GBK 字节会得到乱码。所以这里按<b>内容投票</b>——谁能解出更多中日韩字符就用谁。
/// </para>
/// </summary>
public static class OutputDecoder
{
    /// <summary>常见的中日韩双字节代码页，按优先级排列。</summary>
    private static readonly int[] CjkCodePages = [936, 950, 932, 949];

    static OutputDecoder()
    {
        // 让 .NET 认识 GBK 等传统代码页；缺少包时静默降级
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
        catch { /* 没有代码页支持时下面会降级到 Latin1 */ }
    }

    /// <summary>根据样本字节猜测编码。</summary>
    public static Encoding Detect(ReadOnlySpan<byte> sample)
    {
        if (sample.Length == 0) return new UTF8Encoding(false);

        // UTF-16LE：奇数位大量 0x00
        var pairs = sample.Length / 2;
        if (pairs >= 8)
        {
            var zeros = 0;
            for (var i = 1; i < pairs * 2; i += 2)
            {
                if (sample[i] == 0) zeros++;
            }

            if (zeros > pairs * 0.7) return Encoding.Unicode;
        }

        // 严格 UTF-8：能无损解码就直接用（纯 ASCII 也会走到这里）
        try
        {
            _ = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(sample);
            return new UTF8Encoding(false);
        }
        catch (DecoderFallbackException)
        {
            // 不是合法 UTF-8，继续按中日韩代码页试
        }

        // 投票：哪个代码页能解出最多的 CJK 字符
        Encoding? best = null;
        var bestScore = 0;

        foreach (var codePage in CjkCodePages)
        {
            Encoding encoding;
            try
            {
                encoding = Encoding.GetEncoding(codePage);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                continue;
            }

            var score = Score(encoding.GetString(sample));
            if (score > bestScore)
            {
                bestScore = score;
                best = encoding;
            }
        }

        if (best is not null) return best;

        // 兜底：系统 OEM 代码页（西文环境下通常就是正确解）
        try
        {
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Encoding.Latin1;
        }
    }

    /// <summary>
    /// 给候选编码打分：解出的 CJK 字符越多越好，出现替换字符（解码失败）则扣分。
    /// </summary>
    private static int Score(string text)
    {
        var cjk = 0;
        var replacement = 0;

        foreach (var c in text)
        {
            if (c is >= '\u4E00' and <= '\u9FFF') cjk++;        // 中日韩统一表意文字
            else if (c is >= '\u3400' and <= '\u4DBF') cjk++;   // 扩展 A
            else if (c is >= '\u3040' and <= '\u30FF') cjk++;   // 日文假名
            else if (c is >= '\uAC00' and <= '\uD7AF') cjk++;   // 韩文音节
            else if (c is >= '\u3100' and <= '\u312F') cjk++;   // 注音
            else if (c == '\uFFFD') replacement++;
        }

        return cjk - replacement * 3;
    }
}
