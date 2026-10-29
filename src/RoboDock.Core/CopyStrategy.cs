namespace RoboDock.Core;

/// <summary>
/// 面向用户的四种复制策略。每一项对应一组固定的 robocopy 参数组合，
/// 这样界面上只需要问"你想做什么"，而不是问"你想开哪个开关"。
/// </summary>
public enum CopyStrategy
{
    /// <summary>备份过去 —— 复制缺失的、覆盖更新的，绝不删除目标端任何东西（<c>/E</c>）。</summary>
    BackupOver = 0,

    /// <summary>保持一模一样 —— 镜像，目标端多余的文件/目录会被删除（<c>/MIR</c>）。危险。</summary>
    KeepIdentical = 1,

    /// <summary>只更新改动过的 —— 覆盖较旧的，但保留目标端多余文件（<c>/E /XO</c>）。</summary>
    UpdateChanged = 2,

    /// <summary>只补缺失的 —— 目标端已存在的文件一律跳过（<c>/E /XC /XN /XO</c>）。</summary>
    FillMissing = 3,
}

public static class CopyStrategyExtensions
{
    /// <summary>该策略是否会删除目标端的文件。界面上凡是为 true 的都要红色警示。</summary>
    public static bool DeletesDestinationFiles(this CopyStrategy strategy)
        => strategy == CopyStrategy.KeepIdentical;

    /// <summary>返回该策略对应的 robocopy 开关（不含源/目标）。</summary>
    public static IReadOnlyList<string> ToSwitches(this CopyStrategy strategy) => strategy switch
    {
        CopyStrategy.BackupOver => ["/E"],
        CopyStrategy.KeepIdentical => ["/MIR"],
        CopyStrategy.UpdateChanged => ["/E", "/XO"],
        CopyStrategy.FillMissing => ["/E", "/XC", "/XN", "/XO"],
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null),
    };

    /// <summary>用于界面提示和日志的简短参数文本，如 <c>/E /XO</c>。</summary>
    public static string ToSwitchText(this CopyStrategy strategy)
        => string.Join(' ', strategy.ToSwitches());
}
