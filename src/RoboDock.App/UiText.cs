using RoboDock.Core;

namespace RoboDock.App;

/// <summary>
/// 界面文案。用字典而不是 resx，是为了让 Core 和 App 都能用同一套语言枚举，
/// 而且加一个键不用碰设计器。
/// </summary>
public static class UiText
{
    public static Language Language { get; set; } =
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
            ? Language.Zh
            : Language.En;

    private static bool Zh => Language == Language.Zh;

    public static string T(string key) => (Zh, key) switch
    {
        (true, "AppTitle") => "RoboDock — robocopy 图形界面",
        (false, "AppTitle") => "RoboDock — a GUI for robocopy",

        (true, "Source") => "从哪复制（源）",
        (false, "Source") => "Copy from (source)",
        (true, "Destination") => "复制到哪（目标）",
        (false, "Destination") => "Copy to (destination)",
        (true, "Browse") => "选择…",
        (false, "Browse") => "Browse…",

        (true, "WhatToDo") => "你想做什么？",
        (false, "WhatToDo") => "What do you want to do?",

        (true, "StrategyBackup") => "📄 备份过去\n只增不改，不删目标",
        (false, "StrategyBackup") => "📄 Backup over\nAdditive, never deletes",
        (true, "StrategyMirror") => "🔁 保持一模一样 ⚠\n会删除目标多余文件",
        (false, "StrategyMirror") => "🔁 Keep identical ⚠\nDeletes extras at destination",
        (true, "StrategyUpdate") => "⬆️ 只更新改动过的\n覆盖旧的，不删多余",
        (false, "StrategyUpdate") => "⬆️ Update changed only\nOverwrites older, keeps extras",
        (true, "StrategyFill") => "➕ 只补缺失的\n已有的跳过",
        (false, "StrategyFill") => "➕ Fill missing only\nSkips anything existing",

        (true, "Advanced") => "高级选项",
        (false, "Advanced") => "Advanced options",
        (true, "MultiThread") => "多线程（/MT）",
        (false, "MultiThread") => "Multi-thread (/MT)",
        (true, "Restartable") => "可断点续传（/Z）",
        (false, "Restartable") => "Restartable (/Z)",
        (true, "Unbuffered") => "大文件模式（/J）",
        (false, "Unbuffered") => "Large-file mode (/J)",
        (true, "BackupMode") => "备份模式（/B，需管理员）",
        (false, "BackupMode") => "Backup mode (/B, needs admin)",
        (true, "Retry") => "失败重试",
        (false, "Retry") => "Retry",
        (true, "Exclude") => "排除（分号分隔，如 *.tmp;~$*）",
        (false, "Exclude") => "Exclude (semicolon separated, e.g. *.tmp;~$*)",
        (true, "LogFile") => "日志文件",
        (false, "LogFile") => "Log file",
        (true, "Off") => "关闭",
        (false, "Off") => "Off",
        (true, "ExcludeTip") => "分号分隔，例如 *.tmp;~$*",
        (false, "ExcludeTip") => "Semicolon separated, e.g. *.tmp;~$*",

        (true, "Command") => "将要执行的命令",
        (false, "Command") => "Command to run",
        (true, "Copy") => "复制",
        (false, "Copy") => "Copy",

        (true, "Preview") => "先看看会发生什么",
        (false, "Preview") => "Preview first",
        (true, "Start") => "开始复制",
        (false, "Start") => "Start copy",
        (true, "Cancel") => "取消",
        (false, "Cancel") => "Cancel",

        (true, "WillCopy") => "复制",
        (false, "WillCopy") => "Copy",
        (true, "WillDelete") => "删除",
        (false, "WillDelete") => "Delete",
        (true, "WillSkip") => "跳过",
        (false, "WillSkip") => "Skip",
        (true, "Files") => "个文件",
        (false, "Files") => "files",

        (true, "PreviewRequired") => "先点「先看看会发生什么」，确认后再执行。",
        (false, "PreviewRequired") => "Run the preview first, then confirm.",
        (true, "DangerMirror") => "⚠ 这个策略会删除目标端多余的文件，且不进回收站。",
        (false, "DangerMirror") => "⚠ This strategy deletes extra files at the destination. No recycle bin.",

        (true, "Running") => "正在运行…",
        (false, "Running") => "Running…",
        (true, "Done") => "完成",
        (false, "Done") => "Done",

        (true, "Templates") => "常用场景",
        (false, "Templates") => "Presets",
        (true, "TemplateNone") => "（不套用模板）",
        (false, "TemplateNone") => "(no preset)",
        (true, "TemplatePhotos") => "备份照片/视频到 U 盘（只增不改）",
        (false, "TemplatePhotos") => "Back up photos/videos to USB (additive)",
        (true, "TemplateNas") => "把项目同步到 NAS（保持一模一样）",
        (false, "TemplateNas") => "Mirror a project to a NAS (identical)",
        (true, "TemplateLarge") => "迁移大文件到移动硬盘（多线程+续传）",
        (false, "TemplateLarge") => "Move large files to an external drive (MT + restartable)",
        (true, "TemplateBulk") => "大量小文件快速搬运（/MT:32）",
        (false, "TemplateBulk") => "Bulk small files fast (/MT:32)",

        (true, "ExitCode") => "退出码",
        (false, "ExitCode") => "Exit code",

        (true, "WillHappen") => "将要发生什么",
        (false, "WillHappen") => "What will happen",
        (true, "CardCopy") => "复制",
        (false, "CardCopy") => "Copy",
        (true, "CardCreate") => "创建",
        (false, "CardCreate") => "Create",
        (true, "CardDelete") => "删除",
        (false, "CardDelete") => "Delete",
        (true, "CardFiles") => "个文件",
        (false, "CardFiles") => "files",
        (true, "CardDirs") => "个目录",
        (false, "CardDirs") => "dirs",
        (true, "Duration") => "用时 ",
        (false, "Duration") => "Duration ",
        (true, "Cancelled") => "已取消",
        (false, "Cancelled") => "Cancelled",
        (true, "Stopping") => "正在停止…",
        (false, "Stopping") => "Stopping…",
        (true, "ConfirmMirror") =>
            "这是镜像模式：目标端多余的文件和目录会被删除，且不进回收站。\n\n确定继续吗？",
        (false, "ConfirmMirror") =>
            "Mirror mode: extra files and folders at the destination will be deleted permanently.\n\nContinue?",
        (true, "StatsLine") => "已复制 {0:N0} · 跳过 {1:N0} · 失败 {2:N0} · 额外 {3:N0}",
        (false, "StatsLine") => "Copied {0:N0} · Skipped {1:N0} · Failed {2:N0} · Extras {3:N0}",

        _ => key,
    };
}
