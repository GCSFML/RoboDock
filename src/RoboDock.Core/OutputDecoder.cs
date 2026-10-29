using System.Globalization;
using System.Text;

namespace RoboDock.Core;

/// <summary>
/// 猜测子进程输出的编码并解码。
/// <para>
/// robocopy 用控制台的代码页输出：在中文 Windows 上可能是 GBK(936)，
/// 也可能因为控制台被设为 UTF-8 而输出 UTF-8。两种都要能读，
/// 否则中文路径会变成乱码。
/// </para>
/// </summary>
public static class OutputDecoder
{
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

        try
        {
            _ = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(sample);
            return new UTF8Encoding(false);
        }
        catch (DecoderFallbackException)
        {
            // 不是合法 UTF-8，按系统 OEM 代码页处理
        }

        try
        {
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Encoding.Latin1;
        }
    }
}
