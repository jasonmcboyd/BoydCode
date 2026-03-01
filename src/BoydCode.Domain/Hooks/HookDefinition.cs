using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Hooks;

public sealed record HookDefinition(
    string Name,
    HookTiming Timing,
    string Command,
    string? ToolNamePattern = null,
    int TimeoutMs = 10000);
