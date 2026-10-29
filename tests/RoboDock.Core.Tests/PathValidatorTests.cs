using RoboDock.Core;

namespace RoboDock.Core.Tests;

public class PathValidatorTests
{
    private static bool HasError(IReadOnlyList<PathIssue> issues, PathIssueCode code)
        => issues.Any(i => i.Code == code && i.IsError);

    [Fact]
    public void EmptyPathsAreErrors()
    {
        var issues = PathValidator.Validate(null, "  ");

        Assert.True(HasError(issues, PathIssueCode.SourceEmpty));
        Assert.True(HasError(issues, PathIssueCode.DestinationEmpty));
    }

    [Fact]
    public void RelativePathsAreRejected()
    {
        var issues = PathValidator.Validate(@"src", @"dst");

        Assert.True(HasError(issues, PathIssueCode.SourceNotRooted));
        Assert.True(HasError(issues, PathIssueCode.DestinationNotRooted));
    }

    [Fact]
    public void UncPathsAreAccepted()
    {
        var issues = PathValidator.Validate(@"\\nas\share\photos", @"\\nas2\share\backup");

        Assert.False(HasError(issues, PathIssueCode.SourceNotRooted));
        Assert.False(HasError(issues, PathIssueCode.DestinationNotRooted));
    }

    [Fact]
    public void SamePathIsRejected()
    {
        var issues = PathValidator.Validate(@"C:\data", @"C:\data\");

        Assert.True(HasError(issues, PathIssueCode.SamePath));
    }

    [Fact]
    public void DestinationInsideSourceIsRejected()
    {
        var issues = PathValidator.Validate(@"C:\data", @"C:\data\backup");

        Assert.True(HasError(issues, PathIssueCode.DestinationInsideSource));
    }

    [Fact]
    public void SourceInsideDestinationIsRejected()
    {
        var issues = PathValidator.Validate(@"C:\data\sub", @"C:\data");

        Assert.True(HasError(issues, PathIssueCode.SourceInsideDestination));
    }

    [Fact]
    public void SiblingDirectoriesAreFine()
    {
        var issues = PathValidator.Validate(@"C:\data\a", @"C:\data\b");

        Assert.DoesNotContain(issues, i => i.IsError &&
            i.Code is PathIssueCode.DestinationInsideSource or PathIssueCode.SourceInsideDestination or PathIssueCode.SamePath);
    }

    [Fact]
    public void MissingSourceIsAnError()
    {
        var issues = PathValidator.Validate(@"Z:\definitely-not-here\src", @"Z:\definitely-not-here\dst");

        Assert.True(HasError(issues, PathIssueCode.SourceMissing));
    }

    [Fact]
    public void MissingDestinationIsOnlyAWarning()
    {
        var source = Directory.GetCurrentDirectory();
        var destination = Path.GetFullPath(Path.Combine(source, "..", "robodock-test-not-created"));

        var issues = PathValidator.Validate(source, destination);

        Assert.DoesNotContain(issues, i => i.IsError);
        var warning = issues.SingleOrDefault(i => i.Code == PathIssueCode.DestinationWillBeCreated);
        Assert.NotNull(warning);
        Assert.False(warning.IsError);
    }

    [Theory]
    [InlineData(@"C:\data\", @"C:\data")]
    [InlineData("  \"C:\\data\"  ", @"C:\data")]
    [InlineData(@"C:\", @"C:\")]
    public void NormalizeStripsQuotesAndTrailingSeparators(string input, string expected)
    {
        Assert.Equal(expected, PathValidator.Normalize(input));
    }

    [Fact]
    public void MessagesAreLocalized()
    {
        var zh = PathValidator.Validate(null, null, Language.Zh);
        var en = PathValidator.Validate(null, null, Language.En);

        Assert.NotEqual(zh[0].Message, en[0].Message);
        Assert.False(string.IsNullOrWhiteSpace(en[0].Message));
    }
}
