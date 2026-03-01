using BoydCode.Domain.Enums;

namespace BoydCode.Domain.ContentBlocks;

public sealed record ToolUseBlock(string Id, string Name, string ArgumentsJson) : ContentBlock(ContentBlockType.ToolUse);
