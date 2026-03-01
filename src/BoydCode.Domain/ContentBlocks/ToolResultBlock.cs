using BoydCode.Domain.Enums;

namespace BoydCode.Domain.ContentBlocks;

public sealed record ToolResultBlock(string ToolUseId, string Content, bool IsError = false) : ContentBlock(ContentBlockType.ToolResult);
