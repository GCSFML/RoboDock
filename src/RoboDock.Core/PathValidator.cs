namespace RoboDock.Core;

public enum PathIssueCode
{
    SourceEmpty,
    DestinationEmpty,
    SourceNotRooted,
    DestinationNotRooted,
    SourceMissing,
    SourceNotDirectory,
    SamePath,
    DestinationInsideSource,
    SourceInsideDestination,
    DestinationWillBeCreated,
    SourceIsDriveRoot,
    DestinationIsDriveRoot,
}

/// <summary>一条路径检查结果。<see cref="IsError"/> 为 false 的是提醒，不阻止执行。</summary>
public sealed record PathIssue(PathIssueCode Code, string Message, bool IsError);

/// <summary>
/// 路径校验。重点不是"格式对不对"，而是拦住那些会让人丢数据的组合：
/// 源和目标相同、目标在源里面（镜像时会自我复制/自我删除）。
/// </summary>
public static class PathValidator
{
    public static IReadOnlyList<PathIssue> Validate(
        string? source,
        string? destination,
        Language language = Language.Zh)
    {
        var issues = new List<PathIssue>();
        var zh = language == Language.Zh;

        if (string.IsNullOrWhiteSpace(source))
        {
            issues.Add(new(PathIssueCode.SourceEmpty, zh ? "请选择源目录。" : "Please choose a source folder.", true));
        }

        if (string.IsNullOrWhiteSpace(destination))
        {
            issues.Add(new(PathIssueCode.DestinationEmpty, zh ? "请选择目标目录。" : "Please choose a destination folder.", true));
        }

        if (issues.Count > 0) return issues;

        var src = Normalize(source!);
        var dst = Normalize(destination!);

        if (!IsRooted(src))
        {
            issues.Add(new(PathIssueCode.SourceNotRooted,
                zh ? "源路径必须是完整路径，例如 D:\\照片 或 \\\\nas\\share\\照片。"
                   : "Source must be a full path, e.g. D:\\Photos or \\\\nas\\share\\Photos.", true));
        }

        if (!IsRooted(dst))
        {
            issues.Add(new(PathIssueCode.DestinationNotRooted,
                zh ? "目标路径必须是完整路径，例如 E:\\备份 或 \\\\nas\\share\\备份。"
                   : "Destination must be a full path, e.g. E:\\Backup or \\\\nas\\share\\Backup.", true));
        }

        if (issues.Count > 0) return issues;

        if (!Directory.Exists(src) && !File.Exists(src))
        {
            issues.Add(new(PathIssueCode.SourceMissing,
                zh ? "源目录不存在，请检查路径或磁盘是否已连接。" : "Source folder does not exist. Check the path or whether the drive is connected.", true));
        }
        else if (!Directory.Exists(src))
        {
            issues.Add(new(PathIssueCode.SourceNotDirectory,
                zh ? "源是一个文件。当前版本只支持复制整个目录。" : "Source is a file. This version only copies whole folders.", true));
        }

        if (string.Equals(src, dst, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new(PathIssueCode.SamePath,
                zh ? "源和目标不能是同一个目录。" : "Source and destination cannot be the same folder.", true));
        }
        else if (IsSubPath(src, dst))
        {
            issues.Add(new(PathIssueCode.DestinationInsideSource,
                zh ? "目标目录在源目录里面。这会导致无限嵌套（尤其是镜像模式），请换一个目标。"
                   : "Destination is inside the source. This causes runaway nesting (especially with mirror mode). Pick another destination.", true));
        }
        else if (IsSubPath(dst, src))
        {
            issues.Add(new(PathIssueCode.SourceInsideDestination,
                zh ? "源目录在目标目录里面。镜像模式下可能删除源文件，请换一个路径。"
                   : "Source is inside the destination. Mirror mode could delete source files. Choose different paths.", true));
        }

        if (!issues.Any(i => i.IsError) && !Directory.Exists(dst))
        {
            issues.Add(new(PathIssueCode.DestinationWillBeCreated,
                zh ? "目标目录还不存在，开始复制时会自动创建。" : "Destination does not exist yet; it will be created when copying starts.", false));
        }

        if (IsDriveRoot(src))
        {
            issues.Add(new(PathIssueCode.SourceIsDriveRoot,
                zh ? "源是整个磁盘根目录，文件数量可能非常大。" : "Source is a whole drive root; this may involve a huge number of files.", false));
        }

        if (IsDriveRoot(dst))
        {
            issues.Add(new(PathIssueCode.DestinationIsDriveRoot,
                zh ? "目标是磁盘根目录。镜像模式下会删除该盘上所有多余文件，请特别小心。"
                   : "Destination is a drive root. Mirror mode would delete every extra file on that drive — be very careful.", false));
        }

        return issues;
    }

    /// <summary>去掉首尾空白和包裹的引号，并删除多余的反斜杠（保留根路径的斜杠）。</summary>
    public static string Normalize(string path)
    {
        var value = path.Trim().Trim('"').Trim();
        if (value.Length == 0) return value;

        // 保留 "C:\" 和 "\\server\share\" 这类根路径
        while (value.Length > 3 && value.EndsWith('\\') && !IsRootEnding(value))
        {
            value = value[..^1];
        }

        return value;
    }

    private static bool IsRootEnding(string path)
        => path.Length == 3 && path[1] == ':' && path[2] == '\\';

    private static bool IsRooted(string path)
    {
        if (path.Length == 0) return false;
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return true;      // UNC
        return path.Length >= 3 && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
    }

    private static bool IsDriveRoot(string path)
        => path.Length == 3 && path[1] == ':' && path[2] == '\\';

    private static bool IsSubPath(string parent, string child)
    {
        var prefix = parent.EndsWith('\\') ? parent : parent + "\\";
        return child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
