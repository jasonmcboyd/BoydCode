namespace BoydCode.Domain.Tools;

public sealed record ToolExecutionResult(
    string Content,
    bool IsError = false,
    TimeSpan? Duration = null);
