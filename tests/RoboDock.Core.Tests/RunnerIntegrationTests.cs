using RoboDock.Core;

namespace RoboDock.Core.Tests;

/// <summary>
/// 真机集成测试：会真的调用系统的 robocopy.exe。
/// 默认跳过；设置 ROBODOCK_INTEGRATION=1 并给出两个目录后才会执行：
/// <code>
/// $env:ROBODOCK_INTEGRATION=1
/// $env:ROBODOCK_TEST_SRC="F:\tmp\src"
/// $env:ROBODOCK_TEST_DST="F:\tmp\dst"
/// </code>
/// </summary>
public class RunnerIntegrationTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("ROBODOCK_INTEGRATION") == "1";

    private static string? Source => Environment.GetEnvironmentVariable("ROBODOCK_TEST_SRC");

    private static string? Destination => Environment.GetEnvironmentVariable("ROBODOCK_TEST_DST");

    private static bool Ready => Enabled && Source is { Length: > 0 } && Destination is { Length: > 0 };

    [Fact]
    public void PreviewDoesNotTouchDestination()
    {
        if (!Ready) return;

        var runner = new RobocopyRunner();
        var result = runner.RunAsync(
            new RobocopyOptions { Source = Source!, Destination = Destination! },
            RobocopyMode.Preview).GetAwaiter().GetResult();

        Assert.True(result.IsUsable);
        Assert.True(result.Lines.Count > 0, "应当捕获到运行输出");
        Assert.Equal(0, result.ExitCode & 8);
    }

    [Fact]
    public void ExecuteCopiesAndReportsSuccessExitCode()
    {
        if (!Ready) return;

        var runner = new RobocopyRunner();
        var result = runner.RunAsync(
            new RobocopyOptions
            {
                Source = Source!,
                Destination = Destination!,
                Strategy = CopyStrategy.BackupOver,
                RetryCount = 0,
                RetryWaitSeconds = 0,
                MultiThread = 8,
            }).GetAwaiter().GetResult();

        Assert.NotNull(result.Summary);

        // robocopy 的成功码是 0–7；8 以上代表有文件失败
        Assert.Equal(0, result.ExitCode & 8);
        Assert.True(result.Verdict.IsSuccess, $"退出码 {result.ExitCode} 应被判定为成功");
        Assert.True(result.Summary.FilesTotal > 0, "摘要里应当有文件统计");
    }

    [Fact]
    public void EnglishLanguageProducesEnglishRobocopyOutput()
    {
        if (!Ready) return;

        var runner = new RobocopyRunner();
        var result = runner.RunAsync(
            new RobocopyOptions { Source = Source!, Destination = Destination! },
            RobocopyMode.Preview,
            language: Language.En).GetAwaiter().GetResult();

        // 英文输出用 "Started :" / "Files :"，中文输出是 "开始时间:" / "文件:"
        Assert.Contains(result.Lines, l => l.Contains("Started", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Lines, l => l.Contains("Files :", StringComparison.Ordinal));
    }

    [Fact]
    public void ChineseLanguageProducesChineseRobocopyOutput()
    {
        if (!Ready) return;

        var runner = new RobocopyRunner();
        var result = runner.RunAsync(
            new RobocopyOptions { Source = Source!, Destination = Destination! },
            RobocopyMode.Preview,
            language: Language.Zh).GetAwaiter().GetResult();

        Assert.Contains(result.Lines, l => l.Contains("开始时间") || l.Contains("文件:"));
        Assert.NotNull(result.Summary);   // 中文输出也必须能解析出摘要
    }

    [Fact]
    public void CancellationStopsProcess()
    {
        if (!Ready) return;

        using var cts = new CancellationTokenSource();
        cts.Cancel();   // 启动即取消，避免依赖"任务跑得够慢"这种不确定条件

        var runner = new RobocopyRunner();
        var result = runner.RunAsync(
            new RobocopyOptions { Source = Source!, Destination = Destination! },
            RobocopyMode.Preview,
            cancellationToken: cts.Token).GetAwaiter().GetResult();

        Assert.True(result.Cancelled, "取消标记应当为 true");
    }
}
