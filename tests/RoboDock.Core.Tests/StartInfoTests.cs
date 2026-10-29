using System.Diagnostics;
using RoboDock.Core;

namespace RoboDock.Core.Tests;

/// <summary>
/// robocopy 的界面语言跟随控制台代码页，所以启动方式决定了预览输出的语言。
/// 这里锁住"按界面语言选代码页"这个契约。
/// </summary>
public class StartInfoTests
{
    private static RobocopyOptions Options() => new()
    {
        Source = @"F:\demo\src",
        Destination = @"F:\demo\dst",
    };

    [Fact]
    public void EnglishUsesUtf8CodePageToGetEnglishOutput()
    {
        var startInfo = RobocopyRunner.CreateStartInfo(Options(), RobocopyMode.Preview, Language.En);

        Assert.Equal("cmd.exe", startInfo.FileName);
        Assert.Contains("chcp 65001", startInfo.Arguments);
        Assert.Contains("robocopy", startInfo.Arguments);
        Assert.Contains("/L", startInfo.Arguments);
    }

    [Fact]
    public void ChineseUsesOemCodePage()
    {
        var startInfo = RobocopyRunner.CreateStartInfo(Options(), RobocopyMode.Preview, Language.Zh);

        Assert.Equal("cmd.exe", startInfo.FileName);
        Assert.Contains("chcp ", startInfo.Arguments);
        Assert.DoesNotContain("chcp 65001", startInfo.Arguments);
        Assert.Contains("& robocopy", startInfo.Arguments);
    }

    [Fact]
    public void PathsWithSpacesAreQuoted()
    {
        var options = new RobocopyOptions
        {
            Source = @"F:\my demo\src",
            Destination = @"F:\my demo\dst",
        };

        var startInfo = RobocopyRunner.CreateStartInfo(options, RobocopyMode.Preview, Language.En);

        Assert.Contains(@"""F:\my demo\src""", startInfo.Arguments);
        Assert.Contains(@"""F:\my demo\dst""", startInfo.Arguments);
    }

    [Fact]
    public void CmdMetaCharactersFallBackToDirectLaunch()
    {
        // 路径里有 '&' 时套 cmd 会出事，必须直接启动（代价：语言不可控）
        var options = new RobocopyOptions
        {
            Source = @"F:\a&b\src",
            Destination = @"F:\dst",
        };

        var startInfo = RobocopyRunner.CreateStartInfo(options, RobocopyMode.Preview, Language.En);

        Assert.Equal("robocopy", startInfo.FileName);
        Assert.Contains(@"F:\a&b\src", startInfo.ArgumentList);
    }

    [Fact]
    public void DirectLaunchKeepsArgumentsSeparate()
    {
        var options = new RobocopyOptions
        {
            Source = @"F:\a|b\src",
            Destination = @"F:\dst",
        };

        var startInfo = RobocopyRunner.CreateStartInfo(options, RobocopyMode.Execute, Language.En);

        Assert.Equal("robocopy", startInfo.FileName);
        Assert.DoesNotContain("/L", startInfo.ArgumentList);
        Assert.Contains("/E", startInfo.ArgumentList);
    }
}
