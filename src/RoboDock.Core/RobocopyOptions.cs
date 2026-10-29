namespace RoboDock.Core;

/// <summary>
/// robocopy 一次运行所需的全部参数。全部强类型，不拼字符串——
/// 拼字符串是这类 GUI 最常见的 bug 来源（引号、空格、尾随反斜杠）。
/// </summary>
public sealed record RobocopyOptions
{
    /// <summary>源目录（本地路径或 UNC）。</summary>
    public required string Source { get; init; }

    /// <summary>目标目录（本地路径或 UNC）。</summary>
    public required string Destination { get; init; }

    /// <summary>复制策略，决定是否删除目标端文件。</summary>
    public CopyStrategy Strategy { get; init; } = CopyStrategy.BackupOver;

    // ---------- 复制内容 ----------

    /// <summary>复制文件数据（<c>/COPY</c> 的 D）。</summary>
    public bool CopyData { get; init; } = true;

    /// <summary>复制文件属性（<c>/COPY</c> 的 A）。</summary>
    public bool CopyAttributes { get; init; } = true;

    /// <summary>复制文件时间戳（<c>/COPY</c> 的 T）。</summary>
    public bool CopyTimestamps { get; init; } = true;

    /// <summary>复制 NTFS 安全权限（<c>/COPY</c> 的 S，等价 <c>/SEC</c>）。</summary>
    public bool CopySecurity { get; init; }

    /// <summary>复制所有者信息（<c>/COPYALL</c>）。</summary>
    public bool CopyOwner { get; init; }

    // ---------- 性能 ----------

    /// <summary>多线程数（<c>/MT:n</c>）。null 表示单线程。</summary>
    public int? MultiThread { get; init; }

    /// <summary>可断点续传（<c>/Z</c>）。大文件中途中断后能接着来。</summary>
    public bool Restartable { get; init; }

    /// <summary>无缓冲 I/O（<c>/J</c>）。适合单文件几十 GB 的搬运。</summary>
    public bool UnbufferedIo { get; init; }

    /// <summary>备份模式（<c>/B</c>）。可读取无权限的文件，需要管理员权限。</summary>
    public bool BackupMode { get; init; }

    // ---------- 容错 ----------

    /// <summary>失败重试次数（<c>/R:n</c>）。robocopy 默认是 1000000，必须覆盖。</summary>
    public int RetryCount { get; init; } = 2;

    /// <summary>重试间隔秒数（<c>/W:n</c>）。</summary>
    public int RetryWaitSeconds { get; init; } = 2;

    // ---------- 过滤 ----------

    /// <summary>排除的文件名/通配符（<c>/XF</c>）。</summary>
    public IReadOnlyList<string> ExcludedFiles { get; init; } = [];

    /// <summary>排除的目录名/通配符（<c>/XD</c>）。</summary>
    public IReadOnlyList<string> ExcludedDirectories { get; init; } = [];

    // ---------- 日志 ----------

    /// <summary>日志文件路径。null 表示不写文件。</summary>
    public string? LogFile { get; init; }

    /// <summary>日志追加而不是覆盖（<c>/LOG+</c>）。</summary>
    public bool AppendLog { get; init; } = true;

    /// <summary>同时把日志输出到屏幕（<c>/TEE</c>）。</summary>
    public bool Tee { get; init; } = true;

    /// <summary>该配置是否可能删除目标端文件。UI 用它决定要不要强制试运行。</summary>
    public bool IsDestructive => Strategy.DeletesDestinationFiles();
}
