using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Permissions;

public sealed record PermissionRule(
    string ToolName,
    PermissionLevel Level,
    string? Description = null);
