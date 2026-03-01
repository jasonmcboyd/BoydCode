namespace BoydCode.Domain.Tools;

public sealed record ToolDefinition(
    string Name,
    string Description,
    IReadOnlyList<ToolParameter> Parameters);
