using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Tools;

namespace BoydCode.Domain.LlmRequests;

/// <summary>
/// Encapsulates all inputs to a single LLM API call.
/// Properties are declared in cache-priority order: most stable first, most volatile last.
/// Adapter serializers MUST serialize in property-declaration order to preserve prefix-cache hits.
/// </summary>
public sealed record LlmRequest
{
  // Tier 1 — constant for the session; must lead the payload
  public required string Model { get; init; }
  public string? SystemPrompt { get; init; }
  public IReadOnlyList<ToolDefinition> Tools { get; init; } = [];
  public ToolChoiceStrategy ToolChoice { get; init; } = ToolChoiceStrategy.Auto;
  public IReadOnlyList<ResolvedDirectory> Directories { get; init; } = [];

  // Tier 2 — set at session start; change rarely
  public SamplingOptions? Sampling { get; init; }
  public ThinkingConfig? Thinking { get; init; }
  public RequestMetadata? Metadata { get; init; }

  // Tier 3 — volatile; changes every turn
  public IReadOnlyList<ConversationMessage> Messages { get; init; } = [];
  public bool Stream { get; init; }
}
