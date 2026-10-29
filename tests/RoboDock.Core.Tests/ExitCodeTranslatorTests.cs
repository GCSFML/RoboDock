using RoboDock.Core;

namespace RoboDock.Core.Tests;

public class ExitCodeTranslatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void CodesBelowEightAreSuccess(int code)
    {
        Assert.True(ExitCodeTranslator.Translate(code).IsSuccess);
    }

    [Fact]
    public void CodeOneIsSuccessNotFailure()
    {
        var info = ExitCodeTranslator.Translate(1, Language.En);

        Assert.True(info.IsSuccess);
        Assert.Equal(ExitSeverity.Success, info.Severity);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(11)]
    [InlineData(15)]
    public void CodesWithFailureBitAreFailures(int code)
    {
        var info = ExitCodeTranslator.Translate(code);

        Assert.False(info.IsSuccess);
        Assert.Equal(ExitSeverity.Failure, info.Severity);
    }

    [Fact]
    public void CodeSixteenIsFatal()
    {
        var info = ExitCodeTranslator.Translate(16);

        Assert.False(info.IsSuccess);
        Assert.Equal(ExitSeverity.Fatal, info.Severity);
    }

    [Fact]
    public void BitmaskCombinationMentionsBothFacts()
    {
        // 11 = 8 + 2 + 1：复制了文件、有失败、目标端有额外项
        var info = ExitCodeTranslator.Translate(11, Language.En);

        Assert.Contains("failed", info.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("extras", info.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownCodeDoesNotThrow()
    {
        var info = ExitCodeTranslator.Translate(99);

        Assert.False(info.IsSuccess);
        Assert.Equal(ExitSeverity.Fatal, info.Severity);
    }

    [Fact]
    public void BothLanguagesProduceNonEmptyText()
    {
        foreach (var code in new[] { 0, 1, 2, 8, 16 })
        {
            var zh = ExitCodeTranslator.Translate(code, Language.Zh);
            var en = ExitCodeTranslator.Translate(code, Language.En);

            Assert.False(string.IsNullOrWhiteSpace(zh.Title));
            Assert.False(string.IsNullOrWhiteSpace(zh.Detail));
            Assert.False(string.IsNullOrWhiteSpace(en.Title));
            Assert.False(string.IsNullOrWhiteSpace(en.Detail));
        }
    }
}
