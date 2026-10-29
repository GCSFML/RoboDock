using RoboDock.Core;

namespace RoboDock.Core.Tests;

/// <summary>
/// 中文环境下 robocopy 会把标签全部本地化（目录/文件/字节、新文件/新目录/*额外），
/// 这些数据是从本机 chcp 936 的真实输出里抄下来的。
/// 解析器必须只依赖结构，不能依赖标签文本。
/// </summary>
public class ChineseOutputTests
{
    private static readonly string[] ChineseSummary =
    [
        "",
        "------------------------------------------------------------------------------",
        "",
        "                  总数        复制        跳过       不匹配        失败        其他",
        "       目录:         2         2         0         0         0         0",
        "       文件:         6         6         0         0         0         0",
        "       字节:        60        60         0         0         0         0",
        "       时间:   0:00:00   0:00:00                       0:00:00   0:00:00",
        "   已结束: 2026年10月30日 1:10:11",
        "",
    ];

    private static readonly string[] ChineseSummaryWithUnits =
    [
        "       目录:         2         2         0         0         0         0",
        "       文件:         6         6         0         0         0         0",
        "       字节:   1.23 g    1.23 g         0         0         0        14",
        "       时间:   0:00:00   0:00:00                       0:00:00   0:00:00",
    ];

    [Fact]
    public void ChineseSummaryParsesByStructure()
    {
        var summary = SummaryParser.Parse(ChineseSummary);

        Assert.NotNull(summary);
        Assert.Equal(2, summary.DirsTotal);
        Assert.Equal(6, summary.FilesTotal);
        Assert.Equal(6, summary.FilesCopied);
        Assert.Equal(60L, summary.BytesTotal);
        Assert.False(summary.HasFailures);
    }

    [Fact]
    public void ChineseSummaryParsesByteUnits()
    {
        var summary = SummaryParser.Parse(ChineseSummaryWithUnits);

        Assert.NotNull(summary);
        Assert.Equal(1_320_702_443L, summary.BytesTotal);
        Assert.Equal(14L, summary.BytesExtras);
    }

    [Fact]
    public void ChineseFileLinesClassifyAsCopy()
    {
        var entry = PreviewParser.ParseLine("\t    新文件    \t\t      10\ta1.txt");

        Assert.NotNull(entry);
        Assert.Equal(PreviewKind.CopyFile, entry.Kind);
        Assert.Equal(10L, entry.SizeBytes);
    }

    [Fact]
    public void ChineseDirectoryLinesClassifyAsDirectoryByTrailingBackslash()
    {
        var entry = PreviewParser.ParseLine("\t  新目录           1\tF:\\DSHWork\\_probe\\src\\sub\\");

        Assert.NotNull(entry);
        Assert.Equal(PreviewKind.CreateDirectory, entry.Kind);
    }

    [Fact]
    public void ChineseExtraLinesAreDeletionsBecauseOfAsterisk()
    {
        var entry = PreviewParser.ParseLine("\t  *额外 文件 \t\t       7\textra1.txt");

        Assert.NotNull(entry);
        Assert.Equal(PreviewKind.DeleteFile, entry.Kind);
        Assert.Equal(7L, entry.SizeBytes);
    }

    [Fact]
    public void EnglishAndChineseProduceIdenticalCounts()
    {
        var english = PreviewParser.Parse(
        [
            "\t  *EXTRA File \t\t       7\textra1.txt",
            "\t    New File  \t\t      10\ta1.txt",
            "\t  New Dir          1\tF:\\src\\sub\\",
        ]);

        var chinese = PreviewParser.Parse(
        [
            "\t  *额外 文件 \t\t       7\textra1.txt",
            "\t    新文件    \t\t      10\ta1.txt",
            "\t  新目录           1\tF:\\src\\sub\\",
        ]);

        Assert.Equal(english.FileCountToCopy, chinese.FileCountToCopy);
        Assert.Equal(english.FileCountToDelete, chinese.FileCountToDelete);
        Assert.Equal(english.DirectoryCountToCreate, chinese.DirectoryCountToCreate);
    }

    [Fact]
    public void EncodingIsDetectedAsChineseCodePageForGbkBytes()
    {
        // "文件:" 的 GBK 字节
        var gbk = new byte[] { 0xCE, 0xC4, 0xBC, 0xFE, 0x3A, 0x20, 0x31 };

        var encoding = OutputDecoder.Detect(gbk);

        Assert.Equal(936, encoding.CodePage);
    }

    [Fact]
    public void EncodingIsDetectedAsUtf8ForUtf8Bytes()
    {
        var utf8 = System.Text.Encoding.UTF8.GetBytes("文件: 1 2 3");

        var encoding = OutputDecoder.Detect(utf8);

        Assert.Equal(65001, encoding.CodePage);
    }
}
