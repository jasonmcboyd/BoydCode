namespace BoydCode.Domain.Hooks;

public sealed record HookResult(
    bool Success,
    string Output,
    string? Error = null,
    bool ShouldBlock = false);
