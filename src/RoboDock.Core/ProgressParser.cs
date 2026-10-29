using System.Globalization;
using System.Text.RegularExpressions;

namespace RoboDock.Core;

/// <summary>
/// 解析 robocopy 打印的单文件进度百分比。
/// <para>
/// robocopy 没有机器可读的进度，只有形如 <c>  45.0%</c> 的行。
/// 整体进度靠"已完成文件数 / 试运行得到的总文件数"来估算。
/// </para>
/// </summary>
public static class ProgressParser
{
    private static readonly Regex PercentRegex = new(
        @"^\s*(\d{1,3}(?:\.\d+)?)\s*%\s*$",
        RegexOptions.CultureInvariant);

    /// <summary>尝试从一行中解析出百分比（0–100）。</summary>
    public static bool TryParsePercent(string? line, out double percent)
    {
        percent = 0;
        if (string.IsNullOrWhiteSpace(line)) return false;

        var match = PercentRegex.Match(line);
        if (!match.Success) return false;

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        percent = Math.Clamp(value, 0d, 100d);
        return true;
    }
}
