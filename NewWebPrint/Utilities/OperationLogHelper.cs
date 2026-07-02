namespace NewWebPrint.Utilities;

/// <summary>与 <see cref="Components.Tools.OperationLog"/> 配合使用的日志内容辅助类。</summary>
public static class OperationLogHelper
{
    /// <summary>追加一条带时间戳的日志行，返回更新后的完整内容。</summary>
    public static string Append(string? content, string message) =>
        (content ?? string.Empty) + FormatLine(message);

    /// <summary>格式化单条日志行（含时间戳与换行）。</summary>
    public static string FormatLine(string message) =>
        $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
}
