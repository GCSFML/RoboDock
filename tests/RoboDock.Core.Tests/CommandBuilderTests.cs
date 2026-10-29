using RoboDock.Core;

namespace RoboDock.Core.Tests;

public class CommandBuilderTests
{
    private static RobocopyOptions Options(CopyStrategy strategy = CopyStrategy.BackupOver) => new()
    {
        Source = @"D:\src",
        Destination = @"E:\dst",
        Strategy = strategy,
    };

    [Fact]
    public void BackupOver_UsesE_AndNeverMirrors()
    {
        var args = CommandBuilder.BuildArguments(Options(CopyStrategy.BackupOver));

        Assert.Contains("/E", args);
        Assert.DoesNotContain("/MIR", args);
        Assert.DoesNotContain("/XO", args);
    }

    [Fact]
    public void KeepIdentical_UsesMirror_AndIsDestructive()
    {
        var options = Options(CopyStrategy.KeepIdentical);

        Assert.Contains("/MIR", CommandBuilder.BuildArguments(options));
        Assert.True(options.IsDestructive);
    }

    [Fact]
    public void UpdateChanged_UsesEAndXO()
    {
        var args = CommandBuilder.BuildArguments(Options(CopyStrategy.UpdateChanged));

        Assert.Contains("/E", args);
        Assert.Contains("/XO", args);
        Assert.DoesNotContain("/MIR", args);
    }

    [Fact]
    public void FillMissing_ExcludesChangedNewerAndOlder()
    {
        var args = CommandBuilder.BuildArguments(Options(CopyStrategy.FillMissing));

        Assert.Contains("/E", args);
        Assert.Contains("/XC", args);
        Assert.Contains("/XN", args);
        Assert.Contains("/XO", args);
    }

    [Fact]
    public void AlwaysOverridesRobocopyRetryDefaults()
    {
        var args = CommandBuilder.BuildArguments(Options());

        // robocopy 默认 /R:1000000 /W:30，不覆盖会让人以为程序卡死
        Assert.Contains("/R:2", args);
        Assert.Contains("/W:2", args);
    }

    [Fact]
    public void SourceAndDestinationComeFirst()
    {
        var args = CommandBuilder.BuildArguments(Options());

        Assert.Equal(@"D:\src", args[0]);
        Assert.Equal(@"E:\dst", args[1]);
    }

    [Fact]
    public void MultiThreadOnlyAddedWhenGreaterThanOne()
    {
        var single = CommandBuilder.BuildArguments(Options());
        var multi = CommandBuilder.BuildArguments(Options() with { MultiThread = 16 });
        var pointless = CommandBuilder.BuildArguments(Options() with { MultiThread = 1 });

        Assert.DoesNotContain(single, a => a.StartsWith("/MT"));
        Assert.Contains("/MT:16", multi);
        Assert.DoesNotContain(pointless, a => a.StartsWith("/MT"));
    }

    [Fact]
    public void PreviewAddsListOnlyFlag()
    {
        var execute = CommandBuilder.BuildArguments(Options(), RobocopyMode.Execute);
        var preview = CommandBuilder.BuildArguments(Options(), RobocopyMode.Preview);

        Assert.DoesNotContain("/L", execute);
        Assert.Contains("/L", preview);
    }

    [Fact]
    public void CopyFlagsOmittedWhenDefault()
    {
        var args = CommandBuilder.BuildArguments(Options());

        Assert.DoesNotContain(args, a => a.StartsWith("/COPY:"));
    }

    [Fact]
    public void SecurityAddsCopyDats()
    {
        var args = CommandBuilder.BuildArguments(Options() with { CopySecurity = true });

        Assert.Contains("/COPY:DATS", args);
    }

    [Fact]
    public void OwnerAddsCopyDatso()
    {
        var args = CommandBuilder.BuildArguments(Options() with { CopyOwner = true, CopySecurity = true });

        Assert.Contains("/COPY:DATSO", args);
    }

    [Fact]
    public void LogFileUsesAppendSyntax()
    {
        var args = CommandBuilder.BuildArguments(Options() with { LogFile = @"C:\log.txt", AppendLog = true });

        Assert.Contains(@"/LOG+:C:\log.txt", args);
        Assert.Contains("/TEE", args);
    }

    [Fact]
    public void ExcludedFilesArePassedToXf()
    {
        var args = CommandBuilder.BuildArguments(Options() with { ExcludedFiles = ["*.tmp", "~$*"] });

        var index = args.ToList().IndexOf("/XF");
        Assert.True(index >= 0);
        Assert.Equal("*.tmp", args[index + 1]);
        Assert.Equal("~$*", args[index + 2]);
    }

    [Theory]
    [InlineData(@"D:\plain", @"D:\plain")]
    [InlineData(@"D:\with space\dir", @"""D:\with space\dir""")]
    [InlineData(@"D:\trailing\", @"D:\trailing\")]
    [InlineData(@"D:\my folder\", @"""D:\my folder\\""")]
    public void QuoteForDisplayEscapesTrailingBackslash(string input, string expected)
    {
        Assert.Equal(expected, CommandBuilder.QuoteForDisplay(input));
    }

    [Fact]
    public void DisplayCommandStartsWithExecutableName()
    {
        var command = CommandBuilder.BuildDisplayCommand(Options());

        Assert.StartsWith("robocopy ", command);
        Assert.Contains("/E", command);
    }
}
