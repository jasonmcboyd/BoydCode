using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Tools;

public sealed record ToolDefinition(
    string Name,
    string Description,
    ToolCategory Category,
    IReadOnlyList<ToolParameter> Parameters);
