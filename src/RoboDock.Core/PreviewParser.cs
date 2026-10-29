using System.Globalization;
using System.Text.RegularExpressions;

namespace RoboDock.Core;

/// <summary>
/// 解析 <c>robocopy /L</c> 的输出，得出"将会发生什么"。
/// <para>
/// <b>不依赖语言</b>。robocopy 的类别词会随系统语言变化
/// （英文 <c>New File</c> / 中文 <c>新文件</c>），但有两个稳定的结构标记：
/// </para>
/// <list type="bullet">
///   <item>要被<b>删除</b>的项，类别列一定以 <c>*</c> 开头（<c>*EXTRA File</c> / <c>*额外 文件</c>）；</item>
///   <item><b>目录</b>的路径一定以反斜杠结尾。</item>
/// </list>
/// <para>实测输出形如（列之间用制表符分隔）：</para>
/// <code>
/// 	  *EXTRA File 		       7	extra1.txt
/// 	    New File  		      10	a1.txt
/// 	  New Dir          1	F:\src\sub\
/// </code>
/// </summary>
public static class PreviewParser
{
    private static readonly Regex TrailingSizeRegex = new(@"(\d+)\s*$", RegexOptions.CultureInvariant);

    public static PreviewResult Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var entries = new List<PreviewEntry>();
        foreach (var line in lines)
        {
            if (ParseLine(line) is { } entry) entries.Add(entry);
        }

        return new PreviewResult(entries);
    }

    /// <summary>解析单行；不是文件/目录条目时返回 null。</summary>
    public static PreviewEntry? ParseLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var trimmed = line.TrimEnd();
        var lastTab = trimmed.LastIndexOf('\t');
        if (lastTab < 0) return null;

        var path = trimmed[(lastTab + 1)..].Trim();
        var prefix = trimmed[..lastTab];
        if (path.Length == 0) return null;

        long size = 0;
        string label;
        var sizeMatch = TrailingSizeRegex.Match(prefix);
        if (sizeMatch.Success)
        {
            _ = long.TryParse(sizeMatch.Groups[1].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out size);
            label = prefix[..sizeMatch.Index];
        }
        else
        {
            label = prefix;
        }

        label = label.Replace('\t', ' ').Trim();
        if (label.Length == 0) return null;   // 目录汇总行（只有大小和路径），不是条目

        var isDirectory = path.EndsWith('\\') || path.EndsWith('/');
        var isDeletion = label.StartsWith('*');

        var kind = isDeletion
            ? (isDirectory ? PreviewKind.DeleteDirectory : PreviewKind.DeleteFile)
            : (isDirectory ? PreviewKind.CreateDirectory : PreviewKind.CopyFile);

        return new PreviewEntry(kind, path, size);
    }
}
