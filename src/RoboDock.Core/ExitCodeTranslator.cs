namespace RoboDock.Core;

/// <summary>退出码的严重程度，UI 用它决定颜色。</summary>
public enum ExitSeverity
{
    /// <summary>完全成功，没有需要说明的事。</summary>
    Success = 0,

    /// <summary>成功，但有值得知道的附加信息（例如目标端有额外文件）。</summary>
    SuccessWithNotes = 1,

    /// <summary>部分失败：至少一个文件没能复制。</summary>
    Failure = 2,

    /// <summary>致命错误：一个文件都没复制。</summary>
    Fatal = 3,
}

/// <summary>一次翻译结果：人话标题、详细说明、以及是否算成功。</summary>
public sealed record ExitCodeInfo(
    int Code,
    bool IsSuccess,
    ExitSeverity Severity,
    string Title,
    string Detail);

/// <summary>
/// robocopy 退出码翻译。退出码是位掩码：1=复制了文件，2=目标端有额外项，
/// 4=有不匹配项，8=有失败。<b>0–7 都算成功</b>，这是最反直觉的地方。
/// </summary>
public static class ExitCodeTranslator
{
    public static ExitCodeInfo Translate(int code, Language language = Language.Zh)
    {
        var zh = language == Language.Zh;

        // 常见值给精确文案，避免用户看到"位掩码组合"这种解释
        return code switch
        {
            0 => new ExitCodeInfo(code, true, ExitSeverity.Success,
                zh ? "完成，无需复制" : "Done, nothing to copy",
                zh ? "源和目标已经一致，没有文件需要复制，也没有失败。"
                   : "Source and destination already match. No files copied, no failures."),

            1 => new ExitCodeInfo(code, true, ExitSeverity.Success,
                zh ? "成功复制" : "Copied successfully",
                zh ? "文件已成功复制。这是最常见的成功结果，不是错误。"
                   : "Files were copied successfully. This is the normal success result, not an error."),

            2 => new ExitCodeInfo(code, true, ExitSeverity.SuccessWithNotes,
                zh ? "已复制，目标端有额外项" : "Copied; extras exist at destination",
                zh ? "目标端存在源端没有的文件或目录。当前策略不会删除它们。"
                   : "The destination holds files/directories that don't exist in the source. This strategy leaves them alone."),

            3 => new ExitCodeInfo(code, true, ExitSeverity.SuccessWithNotes,
                zh ? "复制完成（目标端有额外项）" : "Copied (extras at destination)",
                zh ? "已复制文件，同时目标端存在源端没有的项。属正常，未删除任何东西。"
                   : "Files copied, and the destination has extra items. Normal; nothing was deleted."),

            4 => new ExitCodeInfo(code, true, ExitSeverity.SuccessWithNotes,
                zh ? "完成，存在不匹配项" : "Done; mismatches detected",
                zh ? "检测到源与目标不一致的文件或目录（时间戳/大小/属性）。"
                   : "Some files or directories differ (timestamp/size/attributes)."),

            5 => new ExitCodeInfo(code, true, ExitSeverity.SuccessWithNotes,
                zh ? "复制完成，存在不匹配项" : "Copied; mismatches detected",
                zh ? "已复制文件，另有部分项不匹配。" : "Files copied; some items still differ."),

            6 => new ExitCodeInfo(code, true, ExitSeverity.SuccessWithNotes,
                zh ? "完成，有额外项和不匹配项" : "Done; extras and mismatches",
                zh ? "目标端有额外项，同时存在不匹配项。" : "Extras at destination plus mismatched items."),

            7 => new ExitCodeInfo(code, true, ExitSeverity.SuccessWithNotes,
                zh ? "复制完成，有额外项和不匹配项" : "Copied; extras and mismatches",
                zh ? "已复制文件，目标端有额外项，且存在不匹配项。" : "Files copied; extras and mismatches present."),

            16 => new ExitCodeInfo(code, false, ExitSeverity.Fatal,
                zh ? "严重错误：什么都没复制" : "Fatal error: nothing copied",
                zh ? "robocopy 无法继续，通常意味着源路径无效、目标不可写、或参数冲突。请检查路径后重试。"
                   : "robocopy could not proceed — usually an invalid source, an unwritable destination, or conflicting switches."),

            _ => TranslateBitmask(code, language),
        };
    }

    private static ExitCodeInfo TranslateBitmask(int code, Language language)
    {
        var zh = language == Language.Zh;

        if (code < 0 || code > 16)
        {
            return new ExitCodeInfo(code, false, ExitSeverity.Fatal,
                zh ? $"未知退出码 {code}" : $"Unknown exit code {code}",
                zh ? "这个值超出了 robocopy 文档范围，请查看日志。" : "Outside robocopy's documented range; check the log.");
        }

        var copied = (code & 1) != 0;
        var extras = (code & 2) != 0;
        var mismatched = (code & 4) != 0;
        var failed = (code & 8) != 0;

        var parts = new List<string>(3);
        if (copied) parts.Add(zh ? "已复制文件" : "files copied");
        if (extras) parts.Add(zh ? "目标端有额外项" : "extras at destination");
        if (mismatched) parts.Add(zh ? "有不匹配项" : "mismatched items");

        var title = failed
            ? (zh ? "部分文件复制失败" : "Some files failed to copy")
            : (zh ? "完成" : "Done");

        var notes = parts.Count > 0
            ? (zh ? " 附注：" : " Notes: ") + string.Join(zh ? "、" : ", ", parts)
            : string.Empty;

        var detail = failed
            ? (zh
                ? "至少一个文件没能复制（权限、占用、路径过长等）。日志里有具体文件名，可修复后重试。"
                : "At least one file could not be copied (permissions, locks, path length). The log lists them; retry after fixing.")
            : (zh ? "操作完成，无失败。" : "Completed without failures.");

        return new ExitCodeInfo(
            code,
            !failed,
            failed ? ExitSeverity.Failure : ExitSeverity.SuccessWithNotes,
            title,
            detail + notes);
    }
}
