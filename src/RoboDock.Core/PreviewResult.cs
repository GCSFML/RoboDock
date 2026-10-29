namespace RoboDock.Core;

/// <summary>试运行（<c>/L</c>）里一条记录代表什么。</summary>
public enum PreviewKind
{
    /// <summary>将会被复制的文件。</summary>
    CopyFile = 0,

    /// <summary>将会被创建的目录。</summary>
    CreateDirectory = 1,

    /// <summary>将会被删除的文件（仅 /MIR、/PURGE 下出现）。</summary>
    DeleteFile = 2,

    /// <summary>将会被删除的目录（仅 /MIR、/PURGE 下出现）。</summary>
    DeleteDirectory = 3,

    /// <summary>内容相同、会被跳过。</summary>
    Unchanged = 4,

    /// <summary>无法归类的行（例如错误提示）。</summary>
    Unknown = 5,
}

/// <summary>试运行结果中的一条记录。</summary>
public sealed record PreviewEntry(PreviewKind Kind, string Path, long SizeBytes);

/// <summary>一次试运行的完整结果，界面上的"结论卡"直接读这里。</summary>
public sealed record PreviewResult(IReadOnlyList<PreviewEntry> Entries)
{
    public int FileCountToCopy => Entries.Count(e => e.Kind == PreviewKind.CopyFile);

    public int DirectoryCountToCreate => Entries.Count(e => e.Kind == PreviewKind.CreateDirectory);

    public int FileCountToDelete => Entries.Count(e => e.Kind == PreviewKind.DeleteFile);

    public int DirectoryCountToDelete => Entries.Count(e => e.Kind == PreviewKind.DeleteDirectory);

    public int UnchangedCount => Entries.Count(e => e.Kind == PreviewKind.Unchanged);

    public long BytesToCopy => Entries.Where(e => e.Kind == PreviewKind.CopyFile).Sum(e => e.SizeBytes);

    public long BytesToDelete => Entries.Where(e => e.Kind is PreviewKind.DeleteFile or PreviewKind.DeleteDirectory)
                                        .Sum(e => e.SizeBytes);

    /// <summary>这次运行会不会删除目标端的东西——决定结论卡是否显示红色警告。</summary>
    public bool HasDeletions => FileCountToDelete > 0 || DirectoryCountToDelete > 0;

    /// <summary>"会删除 N 个文件（共 X）"这类一句话结论用得到。</summary>
    public int TotalDeletions => FileCountToDelete + DirectoryCountToDelete;
}
