using RoboDock.Core;

namespace RoboDock.Core.Tests;

/// <summary>
/// 解析器的测试数据全部来自本机真实 robocopy 输出（简体中文 Windows），
/// 这样能保证"改了解析器但线上挂了"这类问题不会发生。
/// </summary>
public class ParserTests
{
    private static readonly string[] RealSummaryBlock =
    [
        "",
        "-------------------------------------------------------------------------------",
        "   ROBOCOPY     ::     Robust File Copy for Windows",
        "-------------------------------------------------------------------------------",
        "",
        "  Started : 2026年10月30日 1:02:34",
        "   Source : F:\\DSHWork\\_probe\\src\\",
        "     Dest : F:\\DSHWork\\_probe\\dst\\",
        "",
        "    Files : *.*",
        "",
        "  Options : *.* /L /S /E /DCOPY:DA /COPY:DAT /PURGE /MIR /NP /R:1000000 /W:30 ",
        "",
        "------------------------------------------------------------------------------",
        "",
        "\t                   5\tF:\\DSHWork\\_probe\\src\\",
        "\t  *EXTRA File \t\t       7\textra1.txt",
        "\t  *EXTRA File \t\t       7\textra2.txt",
        "\t    Newer     \t\t      10\ta1.txt",
        "\t    New File  \t\t      10\ta2.txt",
        "\t  New Dir          1\tF:\\DSHWork\\_probe\\src\\sub\\",
        "\t    New File  \t\t      10\tb1.txt",
        "",
        "------------------------------------------------------------------------------",
        "",
        "               Total    Copied   Skipped  Mismatch    FAILED    Extras",
        "    Dirs :         2         1         1         0         0         0",
        "   Files :         6         6         0         0         0         2",
        "   Bytes :        60        60         0         0         0        14",
        "   Times :   0:00:00   0:00:00                       0:00:00   0:00:00",
        "   Ended : 2026年10月30日 1:02:34",
        "",
    ];

    [Fact]
    public void SummaryParsesRealOutput()
    {
        var summary = SummaryParser.Parse(RealSummaryBlock);

        Assert.NotNull(summary);
        Assert.Equal(2, summary.DirsTotal);
        Assert.Equal(1, summary.DirsCopied);
        Assert.Equal(6, summary.FilesTotal);
        Assert.Equal(6, summary.FilesCopied);
        Assert.Equal(2, summary.FilesExtras);
        Assert.Equal(60L, summary.BytesTotal);
        Assert.Equal(14L, summary.BytesExtras);
        Assert.False(summary.HasFailures);
    }

    [Fact]
    public void SummaryParsesByteColumnsWithUnits()
    {
        var lines = new[]
        {
            "    Dirs :         2         1         1         0         0         0",
            "   Files :         6         6         0         0         0         2",
            "   Bytes :   1.23 g    1.23 g         0         0         0        14",
            "   Times :   0:00:00   0:00:00                       0:00:00   0:00:00",
        };

        var summary = SummaryParser.Parse(lines);

        Assert.NotNull(summary);
        Assert.Equal(1_320_702_443L, summary.BytesTotal);
        Assert.Equal(14L, summary.BytesExtras);
    }

    [Fact]
    public void SummaryReturnsNullWhenNoFilesLine()
    {
        var lines = new[] { "  Simple Usage :: ROBOCOPY source destination /MIR", "" };

        Assert.Null(SummaryParser.Parse(lines));
    }

    [Fact]
    public void SummaryDetectsFailures()
    {
        var lines = new[]
        {
            "    Dirs :         3         3         0         0         0         0",
            "   Files :        10         7         0         0         3         0",
        };

        var summary = SummaryParser.Parse(lines);

        Assert.NotNull(summary);
        Assert.True(summary.HasFailures);
        Assert.Equal(3, summary.FilesFailed);
    }

    [Fact]
    public void PreviewParsesExtraFileAsDeletion()
    {
        var entry = PreviewParser.ParseLine(
            "\t  *EXTRA File \t\t       7\tF:\\DSHWork\\_probe\\dst\\extra1.txt");

        Assert.NotNull(entry);
        Assert.Equal(PreviewKind.DeleteFile, entry.Kind);
        Assert.Equal(7L, entry.SizeBytes);
        Assert.Equal(@"F:\DSHWork\_probe\dst\extra1.txt", entry.Path);
    }

    [Fact]
    public void PreviewParsesNewFileAsCopy()
    {
        var entry = PreviewParser.ParseLine(
            "\t    New File  \t\t      10\tF:\\DSHWork\\_probe\\src\\a2.txt");

        Assert.NotNull(entry);
        Assert.Equal(PreviewKind.CopyFile, entry.Kind);
        Assert.Equal(10L, entry.SizeBytes);
    }

    [Fact]
    public void PreviewParsesNewerAsCopy()
    {
        var entry = PreviewParser.ParseLine(
            "\t    Newer     \t\t      10\tF:\\DSHWork\\_probe\\src\\a1.txt");

        Assert.NotNull(entry);
        Assert.Equal(PreviewKind.CopyFile, entry.Kind);
    }

    [Fact]
    public void PreviewParsesNewDirAsCreateDirectory()
    {
        var entry = PreviewParser.ParseLine("\t  New Dir          1\tF:\\DSHWork\\_probe\\src\\sub\\");

        Assert.NotNull(entry);
        Assert.Equal(PreviewKind.CreateDirectory, entry.Kind);
    }

    [Fact]
    public void PreviewIgnoresDirectorySummaryLinesAndNoise()
    {
        Assert.Null(PreviewParser.ParseLine("\t                   5\tF:\\DSHWork\\_probe\\src\\"));
        Assert.Null(PreviewParser.ParseLine(""));
        Assert.Null(PreviewParser.ParseLine("   ROBOCOPY     ::     Robust File Copy for Windows"));
        Assert.Null(PreviewParser.ParseLine("  45.0%"));
    }

    [Fact]
    public void PreviewAggregatesCounts()
    {
        var result = PreviewParser.Parse(RealSummaryBlock);

        Assert.Equal(3, result.FileCountToCopy);          // a1 (Newer), a2, b1
        Assert.Equal(2, result.FileCountToDelete);
        Assert.Equal(1, result.DirectoryCountToCreate);
        Assert.True(result.HasDeletions);
        Assert.Equal(2, result.TotalDeletions);
    }

    [Theory]
    [InlineData("  45.0%", 45.0)]
    [InlineData("100%", 100.0)]
    [InlineData("  0.0%", 0.0)]
    [InlineData("\t  12.5%  ", 12.5)]
    public void ProgressParsesPercentages(string line, double expected)
    {
        Assert.True(ProgressParser.TryParsePercent(line, out var percent));
        Assert.Equal(expected, percent, 3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("robocopy")]
    [InlineData("100 % done")]
    [InlineData("  1.2.3%")]
    public void ProgressRejectsNonProgressLines(string line)
    {
        Assert.False(ProgressParser.TryParsePercent(line, out _));
    }
}
