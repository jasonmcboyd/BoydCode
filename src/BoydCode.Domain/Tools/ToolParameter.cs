namespace BoydCode.Domain.Tools;

public sealed record ToolParameter(
    string Name,
    string Type,
    string Description,
    bool Required = false,
    IReadOnlyList<string>? EnumValues = null);
