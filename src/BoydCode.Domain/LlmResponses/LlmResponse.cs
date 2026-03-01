using BoydCode.Domain.ContentBlocks;

namespace BoydCode.Domain.LlmResponses;

public sealed class LlmResponse
{
  public required IReadOnlyList<ContentBlock> Content { get; init; }
  public required string StopReason { get; init; }
  public required TokenUsage Usage { get; init; }

  public bool HasToolUse => Content.Any(c => c is ToolUseBlock);
  public IEnumerable<ToolUseBlock> ToolUseCalls => Content.OfType<ToolUseBlock>();
  public string? TextContent => string.Join("", Content.OfType<TextBlock>().Select(t => t.Text));
}
