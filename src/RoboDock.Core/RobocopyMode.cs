namespace RoboDock.Core;

/// <summary>
/// 运行模式。<see cref="Preview"/> 会在参数里追加 <c>/L</c>，
/// robocopy 只列出"将会发生什么"，不改动任何文件。
/// </summary>
public enum RobocopyMode
{
    Execute = 0,
    Preview = 1,
}

/// <summary>界面语言。Core 层自己保存文案，方便单元测试，也避免 UI 与逻辑耦合。</summary>
public enum Language
{
    Zh = 0,
    En = 1,
}
