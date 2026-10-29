namespace RoboDock.Core;

/// <summary>
/// robocopy 结束时打印的摘要表（Total / Copied / Skipped / Mismatch / FAILED / Extras）。
/// <para>
/// 表头是英文，在所有语言的 Windows 上都一样——所以它是解析的<b>主数据源</b>，
/// 比逐行猜"这个文件复制了没有"可靠得多。
/// </para>
/// </summary>
public sealed record RobocopySummary(
    int DirsTotal,
    int DirsCopied,
    int DirsSkipped,
    int DirsMismatch,
    int DirsFailed,
    int DirsExtras,
    int FilesTotal,
    int FilesCopied,
    int FilesSkipped,
    int FilesMismatch,
    int FilesFailed,
    int FilesExtras,
    long BytesTotal,
    long BytesCopied,
    long BytesSkipped,
    long BytesMismatch,
    long BytesFailed,
    long BytesExtras)
{
    /// <summary>是否一切正常（没有失败的文件和目录）。</summary>
    public bool HasFailures => FilesFailed > 0 || DirsFailed > 0;
}
