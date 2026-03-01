# ADR-0001: Prefix-Cache-Optimized LLM Request Field Ordering

**Status:** Implemented

---

## Context

LLM providers (Anthropic, Gemini, OpenAI) implement prefix caching: they tokenize the serialized request payload and cache the longest matching prefix. On subsequent requests, any cached prefix is reused, skipping re-tokenization and, where supported, re-computation of the KV attention cache. The consequence is that **field ordering in the serialized payload directly determines cache hit rate**, and therefore cost and latency.

In a multi-turn conversation the fields that change each turn are exactly the conversation messages. Everything else — the model name, system prompt, tool definitions, tool choice strategy, and sampling parameters — is either constant for the lifetime of the session or changes rarely. If these stable fields appear before the messages in the serialized payload, every new turn extends a prefix that the provider has already cached. If they appear after the messages, or interleaved with them, no prefix ever matches across turns.

The existing `ILlmProvider` interface passes `Conversation` and `IReadOnlyList<ToolDefinition>` as separate parameters, leaving the adapter responsible for assembling the payload in whatever order it chooses. MEAI's `ChatOptions` is constructed separately and does not declare a serialization order. This is sufficient today but creates a fragile implicit contract: any adapter that serializes fields in a suboptimal order silently degrades cache performance with no compiler or test feedback.

The system prompt currently lives on `Conversation.SystemPrompt`, mixing a session-stable field into the message-accumulator entity. This makes it harder to reason about which parts of a request are stable vs. volatile.

---

## Decision

We introduce a `LlmRequest` sealed record in `BoydCode.Domain/LlmRequests/` whose properties are declared in cache-priority order — most stable first, most volatile last. The property declaration order is the canonical serialization order. Adapters MUST serialize in property-declaration order to preserve the cache benefit.

### Field ordering tiers

| Tier | Fields | Stability |
|------|--------|-----------|
| 1 — Most stable | `Model`, `SystemPrompt`, `Tools`, `ToolChoice`, `Directories` | Constant for the lifetime of a session |
| 2 — Session-stable | `Sampling` (`SamplingOptions`), `Thinking` (`ThinkingConfig`), `Metadata` (`RequestMetadata`) | Set once at session start; rarely changed mid-session |
| 3 — Per-turn | `Messages`, `Stream` | Grow or change every round-trip |

The record definition reads:

```csharp
// BoydCode.Domain/LlmRequests/LlmRequest.cs

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
```

Supporting value-object records live in the same namespace:

- `SamplingOptions` — `Temperature`, `TopP`, `TopK`, `MaxOutputTokens`
- `ThinkingConfig` — `Enabled`, `BudgetTokens`
- `ToolChoiceStrategy` — enum: `Auto`, `Any`, `None`
- `RequestMetadata` — `UserId` and any other provider-specific annotations

### Interface change

`ILlmProvider.SendAsync` and `ILlmProvider.StreamAsync` change from multi-parameter signatures to a single `LlmRequest`:

```csharp
// Before
Task<LlmResponse> SendAsync(Conversation conversation, IReadOnlyList<ToolDefinition> tools, CancellationToken ct);
IAsyncEnumerable<string> StreamAsync(Conversation conversation, IReadOnlyList<ToolDefinition> tools, CancellationToken ct);

// After
Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct);
IAsyncEnumerable<string> StreamAsync(LlmRequest request, CancellationToken ct);
```

`AgentOrchestrator` constructs the `LlmRequest` each turn using the `with` expression pattern, so only `Messages` needs to change:

```csharp
// Session start — build the base request once
var baseRequest = new LlmRequest
{
    Model        = _activeProvider.Model,
    SystemPrompt = session.SystemPrompt,
    Tools        = _toolRegistry.GetAllDefinitions(),
    ToolChoice   = ToolChoiceStrategy.Auto,
    Sampling     = new SamplingOptions { MaxOutputTokens = _settings.MaxOutputTokens },
};

// Each turn — cheap copy that extends the cached prefix
var turnRequest = baseRequest with { Messages = session.Conversation.Messages };
var response = await provider.SendAsync(turnRequest, ct);
```

### Conversation becomes a pure message accumulator

`Conversation.SystemPrompt` is removed. `Conversation` accumulates only `ConversationMessage` instances plus token-estimation and compaction helpers. The system prompt is owned by the call site (`AgentOrchestrator`) and placed into `LlmRequest.SystemPrompt`. This cleanly separates the stable request envelope from the volatile message history.

### Adapter serialization contract

Each adapter is responsible for mapping `LlmRequest` to its provider's wire format. Adapters MUST write fields in the tier order defined by `LlmRequest`'s property declaration:

- **MEAI adapters** (`MeaiLlmProviderAdapter`): pass `SystemPrompt` as the first `ChatMessage(ChatRole.System, …)` before all conversation messages; set `ChatOptions.ModelId`, `Tools`, and `ToolMode` before constructing the message list.
- **Gemini adapter** (`GeminiLlmProviderAdapter`): map to the native `GenerateContentRequest` with `SystemInstruction` and `Tools` before `Contents`.

The ordering is enforced by convention and documented here, not by runtime checks. The ADR is the enforcement mechanism.

---

## Consequences

### Benefits

- **Lower cost and latency per turn.** System prompt and tool definitions — often thousands of tokens — are served from cache on every turn after the first. At scale this is the dominant factor in per-request cost.
- **Per-request control.** Callers can independently override `Temperature`, `ThinkingConfig`, or `ToolChoice` per request without touching the session's conversation history or spawning a new session.
- **Cleaner separation of concerns.** `Conversation` is a pure message accumulator. `LlmRequest` is the complete, self-describing API call envelope. Adapters map one to the other; no adapter needs to know about `Session` or `AppSettings`.
- **Cheap per-turn copies.** Because `LlmRequest` is a sealed record, `with` expressions produce a new instance that shares all stable field references and updates only `Messages`. No allocation pressure from copying tool definitions or the system prompt.
- **Discoverable contract.** The tier ordering is visible in property declaration order, not buried in adapter code or implicit in call-site argument ordering.

### Costs and risks

- **Breaking interface change.** `ILlmProvider.SendAsync` and `StreamAsync` change signature. All adapters (`MeaiLlmProviderAdapter`, `GeminiLlmProviderAdapter`) and all callers (`AgentOrchestrator`) must be updated together.
- **`Conversation.SystemPrompt` removal.** `MessageConverter.ToMeaiMessages` currently reads `SystemPrompt` from `Conversation`. That responsibility moves to the call site. Any code that constructs a `Conversation` and expects to carry the system prompt through it must be updated.
- **Serialization order is a convention.** There is no compile-time guarantee that an adapter serializes in declaration order. A future adapter author who is unaware of this ADR could inadvertently break prefix-cache alignment. This risk is mitigated by keeping this ADR close to the code and referencing it from `CLAUDE.md`.
- **Context compaction must preserve tier ordering.** `IContextCompactor` returns compacted `ConversationMessage` instances. It does not touch `LlmRequest` fields, so compaction is safe. However, `Conversation.EstimateTokenCount()` no longer includes the system prompt; callers that need a total estimate must add `LlmRequest.SystemPrompt.Length / 4` separately, or `EstimateTokenCount` should be moved to a helper that accepts an `LlmRequest`.
