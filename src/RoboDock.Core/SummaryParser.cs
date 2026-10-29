using System.Globalization;
using System.Text.RegularExpressions;

namespace RoboDock.Core;

/// <summary>
/// 解析 robocopy 的摘要表。
/// <para>
/// <b>不能按标签匹配</b>：robocopy 的界面语言跟随控制台代码页，
/// 同一个程序在英文环境输出 <c>Files :</c>，在中文环境输出 <c>文件:</c>。
/// 但表格的<b>结构</b>是稳定的——四行、每行六个数字列，顺序固定为
/// 目录 / 文件 / 字节 / 时间。所以这里按结构识别：
/// 找到最后两行"六个数字列"的相邻行，前一行是目录、后一行是文件。
/// </para>
/// </summary>
public static partial class SummaryParser
{
    /// <summary>行首可以是任意非数字标签（"Files :"、"文件:"、"目录:"），后面必须紧跟六个数字列。</summary>
    [GeneratedRegex(@"^[^\d]*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SixCountsRegex();

    public static RobocopySummary? Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var list = lines as IList<string> ?? lines.ToList();

        // 摘要表是"目录 / 文件 / 字节 / 时间"四行连续出现；前三行都可能被解析成六个整数。
        // 找出最后一段连续的"六个整数"行：第 1 行目录、第 2 行文件、第 3 行字节（若有单位则跳过）。
        var runStart = -1;
        var runLength = 0;
        var bestStart = -1;
        var bestLength = 0;

        for (var i = 0; i < list.Count; i++)
        {
            if (TryParseSixCounts(list[i], out _))
            {
                if (runStart < 0) runStart = i;
                runLength++;
                continue;
            }

            if (runLength >= 2)
            {
                bestStart = runStart;
                bestLength = runLength;
            }

            runStart = -1;
            runLength = 0;
        }

        if (runLength >= 2)
        {
            bestStart = runStart;
            bestLength = runLength;
        }

        if (bestStart < 0 || bestLength < 2) return null;

        if (!TryParseSixCounts(list[bestStart], out var dirs)) return null;
        if (!TryParseSixCounts(list[bestStart + 1], out var files)) return null;

        var bytes = new long[6];
        if (bestLength >= 3 && TryParseByteRow(list[bestStart + 2], out var inlineBytes))
        {
            bytes = inlineBytes;
        }
        else
        {
            for (var j = bestStart + 2; j < Math.Min(bestStart + 5, list.Count); j++)
            {
                if (!TryParseByteRow(list[j], out var parsed)) continue;
                bytes = parsed;
                break;
            }
        }

        return new RobocopySummary(
            dirs[0], dirs[1], dirs[2], dirs[3], dirs[4], dirs[5],
            files[0], files[1], files[2], files[3], files[4], files[5],
            bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5]);
    }

    private static bool TryParseSixCounts(string line, out int[] values)
    {
        values = new int[6];

        var match = SixCountsRegex().Match(line);
        if (!match.Success) return false;

        for (var i = 0; i < 6; i++)
        {
            if (!int.TryParse(match.Groups[i + 1].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 解析字节行。robocopy 写成 <c>1.23 g</c> 这种"数字 + 单位"成对形式，
    /// 而且列宽随数值变化，所以按 token 配对解析。
    /// </summary>
    private static bool TryParseByteRow(string line, out long[] values)
    {
        values = new long[6];

        var firstDigit = -1;
        for (var i = 0; i < line.Length; i++)
        {
            if (char.IsAsciiDigit(line[i]))
            {
                firstDigit = i;
                break;
            }
        }

        if (firstDigit < 0) return false;

        var tokens = line[firstDigit..].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var parsed = new List<long>(6);

        for (var i = 0; i < tokens.Length && parsed.Count < 6; i++)
        {
            if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            var multiplier = 1L;
            if (i + 1 < tokens.Length && tokens[i + 1].Length == 1 && TryUnit(tokens[i + 1][0], out var factor))
            {
                multiplier = factor;
                i++;
            }

            parsed.Add((long)(number * multiplier));
        }

        if (parsed.Count != 6) return false;

        values = [.. parsed];
        return true;
    }

    private static bool TryUnit(char unit, out long factor)
    {
        factor = char.ToLowerInvariant(unit) switch
        {
            'k' => 1024L,
            'm' => 1024L * 1024,
            'g' => 1024L * 1024 * 1024,
            't' => 1024L * 1024 * 1024 * 1024,
            _ => 0L,
        };
        return factor > 0;
    }
}
