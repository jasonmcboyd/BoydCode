# ADR-0004: Context-Aware Summarization, Smart Pruning, and Proactive Context Management

**Status:** Proposed

---

## Context

BoydCode has two context management mechanisms today: automatic compaction and manual summarization. Both have significant gaps that leave users blind to context
pressure and produce suboptimal results when the context window fills up.

**Automatic compaction** (`AgentOrchestrator.CompactIfNeededAsync`) triggers when the estimated token count exceeds `CompactionThresholdPercent` (default 80%) of
`MaxContextWindowTokens` (from `ProviderCapabilities`, falling back to `AppSettings.ContextWindowTokenLimit`). It delegates to `EvictionContextCompactor`, which
walks backward from the newest messages, keeping tool-call/tool-result pairs intact, and discards everything that does not fit within `contextLimit / 2` tokens. A
compaction notice is prepended. The user has no input on what is important, receives no advance warning, and cannot see what was removed until the next `/context
show`.

**Manual summarization** (`ContextSlashCommand.HandleSummarizeAsync`) builds a separate `LlmRequest` with an empty tools array and `ToolChoice.None`. It extracts
the most recent user-assistant exchange, sends everything else to the LLM with a summarization system prompt, and presents a preview/approve/revise loop (Apply,
Revise, Cancel). Revision feedback is appended to the ephemeral summarization system prompt and does not enter the conversation history. On Apply, old messages
are replaced with the summary plus the preserved recent exchange.

Five gaps compound into a degraded experience as conversations grow:

**1. No token budget communicated to the LLM during summarization.** The summarization prompt tells the model what to capture but not how much space it has. The
summary can be arbitrarily large -- potentially larger than the free context space after the system prompt, tool definitions, and recent exchange are accounted
for. There is no feedback loop when the summary exceeds the available budget.

**2. No fork-to-new-conversation option.** When a conversation has accumulated substantial context and the user wants to continue on a narrower thread, the only
choices are to replace the history in place (lossy) or start a completely new session from scratch (losing all context). There is no way to start a new
conversation seeded with the summary while preserving the original session.

**3. No proactive warnings.** The user does not know they are approaching the context limit. Auto-compaction fires silently at 80%, evicting messages with no
opportunity for the user to choose what to preserve. By the time the user sees the compaction warning, the damage is done.

**4. Auto-compaction is opaque and cuts at arbitrary boundaries.** `EvictionContextCompactor` is a pure eviction strategy -- it drops the oldest messages that
exceed the target token count. It has no understanding of topic boundaries or what the user considers important. For a conversation that spent 30 messages setting
up a complex architectural context followed by 20 messages of routine edits, eviction discards the architecture context first, which is exactly backwards. Worse,
it can chop a topic in half -- discarding the first 15 messages of a 20-message architectural discussion while keeping the last 5, which now lack context and can
cause the LLM's behavior to drift in unpredictable ways. The command was named "compact" which users confused with summarization; it was removed from the user-
facing help menu in the slash command reorganization (see ADR-0002 follow-up) because the UX was user-hostile.

**5. Logging does not capture full mutation detail.** `LogContextSummarizeAsync` records `messagesBefore` and `messagesAfter` counts but not the summary text
itself, the summarization instructions, or the before/after token estimates. A conversation cannot be fully reconstructed from the JSONL log after a summarization
event.

---

## Decision

Introduce context-aware summarization with token budgets, conversation forking, smart pruning at topic boundaries, proactive warnings, and enriched logging. Replace
blind eviction with LLM-assisted topic-boundary pruning as the automatic safety net. The three context management tools form a tiered defense: proactive warnings
at 70% give users time to act, auto-prune at 80% fires silently during agentic loops, and summarize remains always user-initiated for maximum control.

### 1. Token budget for summarization

`ContextSlashCommand.HandleSummarizeAsync` must calculate the available token budget before constructing the summarization `LlmRequest` and communicate that
budget to the LLM in the system prompt.

**Budget calculation:**

```
contextLimit         = ProviderCapabilities.MaxContextWindowTokens
                      (or AppSettings.ContextWindowTokenLimit)
systemPromptTokens   = EstimateStringTokens(MetaPrompt.Build(...) + session.SystemPrompt)
toolDefinitionTokens = EstimateToolDefinitionTokens(ShellToolDefinition)
recentExchangeTokens = EstimateContentBlockTokens(recentExchange)
safetyMargin         = contextLimit * 10 / 100   // 10% headroom
availableTokens      = contextLimit - systemPromptTokens - toolDefinitionTokens
                      - recentExchangeTokens - safetyMargin
targetChars          = availableTokens * 4        // inverse of chars/4 estimation
```

The summarization system prompt is extended with a budget directive:

```
Your summary must not exceed {targetChars} characters (approximately {availableTokens} tokens).
Prioritize density over completeness. If you cannot fit everything within the budget,
preserve decisions and file references over discussion context.
```

After the LLM returns a summary, `HandleSummarizeAsync` estimates the summary token count. If it exceeds `availableTokens`, a warning is rendered:

```
Warning: Summary exceeds budget ({actualTokens} > {availableTokens} tokens).
  Applying it may leave limited space for new conversation turns.
```

The user still sees the four-option menu and can choose to apply, revise (with explicit instructions to compress further), fork, or cancel.

### 2. Four-option summarization menu

The current `SummarizeChoices` array (`["Apply", "Revise", "Cancel"]`) is replaced with:

```csharp
private static readonly string[] SummarizeChoices = ["Apply", "Fork conversation", "Revise", "Cancel"];
```

Each option behaves as follows:

| Option | Behavior |
|---|---|
| **Apply** | Current behavior. Replaces conversation history with the summary message plus the preserved recent exchange. Logs `context_summarize` event. |
| **Fork conversation** | Creates a new `Session` seeded with the summary. Old session is preserved and saved. New session becomes the active session. Logs
`context_fork` to both session logs. |
| **Revise** | Current behavior. Appends revision instructions to the ephemeral summarization system prompt. The LLM returns a new summary. No conversation
modification until Apply or Fork. |
| **Cancel** | Current behavior. Discards the summary, no modification. |

### 3. Fork conversation flow

When the user selects "Fork conversation":

```
1.  Save the current session via ISessionRepository.SaveAsync.
2.  Create a new Session:
    - new Session(session.WorkingDirectory)
    - Copy ProjectName from the old session
    - Set Name to null (will be auto-named in step 5)
3.  Seed the new conversation with a single user message:
    "[Summary of previous conversation {oldSession.Id}]\n\n{summaryText}"
4.  Rebuild system prompt via ChatCommand.BuildSystemPrompt(project, resolvedDirs, pathMappings).
5.  Auto-name the new session:
    - Send a separate LlmRequest (empty tools, ToolChoice.None, Stream=false) with system prompt:
      "Name this conversation in 3-5 words based on the following summary.
      Respond with only the name."
    - Set newSession.Name to the response text (trimmed, max 50 chars)
    - If the naming request fails, fall back to "Fork of {oldSession.Id}"
6.  Set ActiveSession.Set(newSession).
7.  Initialize IConversationLogger for the new session.
8.  Log context_fork event to the OLD session log (old_session_id, new_session_id, summary_text).
9.  Log context_fork event to the NEW session log (old_session_id, new_session_id, summary_text).
10. Log session_start event to the new session log.
11. Save the new session via ISessionRepository.SaveAsync.
12. Render confirmation with the auto-assigned name and new session ID. Old session preserved.
```

The fork flow requires `ContextSlashCommand` to have access to `ISessionRepository`, `IConversationLogger`, `ActiveSession`, and the project/directory resolution
machinery. It already has all of these except that `IConversationLogger` initialization for the new session requires calling `InitializeAsync(newSessionId)` --
the logger must support re-initialization or a new logger instance must be resolved.

### 4. Proactive context warnings

`AgentOrchestrator.RunAgentTurnAsync` is extended to check context usage **after each completed LLM turn** and render a warning when estimated usage crosses a
configurable threshold.

**New configuration:**

```csharp
// AppSettings
public int ContextWarningThresholdPercent { get; set; } = 70;
```

**Warning state:**

A private field `_contextWarningIssued` on `AgentOrchestrator` tracks whether a warning has been shown for the current threshold crossing. It resets to `false`
when estimated usage drops below the threshold (e.g., after summarization or compaction).

**Check location:** After `response.HasToolUse` is false (turn complete, no more tool calls) and before `AutoSaveSessionAsync`:

```csharp
// After a completed turn (no more tool use), check context pressure
var postTurnTokens = session.Conversation.EstimateTokenCount() + systemPromptTokens;
var warningThreshold = contextLimit * _settings.ContextWarningThresholdPercent / 100;

if (postTurnTokens > warningThreshold && !_contextWarningIssued)
{
  var usagePercent = (int)((double)postTurnTokens / contextLimit * 100);
  _ui.RenderWarning(
    $"Context usage at {usagePercent}% ({postTurnTokens:N0}/{contextLimit:N0} tokens). " +
    "Consider /context summarize or /context prune to free space before auto-prune at " +
    $"{_settings.CompactionThresholdPercent}%.");
  _contextWarningIssued = true;
}
else if (postTurnTokens <= warningThreshold)
{
  _contextWarningIssued = false;
}
```

The warning fires once per threshold crossing. If context usage drops (via summarization, clear, or fork) and then rises again, it fires again.

### 5. Enriched logging

#### Existing events -- extended signatures

`IConversationLogger.LogContextSummarizeAsync` gains additional parameters:

```csharp
// Before
Task LogContextSummarizeAsync(
    int messagesBefore, int messagesAfter,
    CancellationToken ct = default);

// After
Task LogContextSummarizeAsync(
    string summaryText, string? instructions,
    int messagesBefore, int messagesAfter,
    int tokensBefore, int tokensAfter,
    CancellationToken ct = default);
```

#### New event -- context_fork

```csharp
Task LogContextForkAsync(
    string oldSessionId, string newSessionId,
    string summaryText, string? autoName,
    CancellationToken ct = default);
```

The `context_fork` event is logged to **both** the old and new session JSONL files. The old session log records that it was forked from; the new session log
records that it was forked to. This provides bidirectional traceability -- given either session log, you can find the other.

#### JSONL event schemas

**`context_summarize`** (enriched):

```json
{
  "event": "context_summarize",
  "timestamp": "2026-02-28T12:00:00Z",
  "summary_text": "...",
  "instructions": "focus on architecture decisions",
  "messages_before": 42,
  "messages_after": 3,
  "tokens_before": 28000,
  "tokens_after": 4200
}
```

**`context_fork`** (new):

```json
{
  "event": "context_fork",
  "timestamp": "2026-02-28T12:00:00Z",
  "old_session_id": "a1b2c3d4e5f6",
  "new_session_id": "f6e5d4c3b2a1",
  "summary_text": "...",
  "auto_name": "Architecture refactor continuation"
}
```

### 6. Smart pruning replaces blind eviction

The current `EvictionContextCompactor` (blind oldest-first eviction) is replaced with a smarter strategy: **pruning at topic boundaries**. This becomes both a
user-facing command (`/context prune`) and the automatic safety net at 80%.

**Prune vs summarize -- distinct operations:**

| | `/context summarize` | `/context prune` |
|---|---|---|
| **What it does** | Condenses the conversation into a dense summary via LLM | Drops older messages at logical topic boundaries |
| **Content fate** | Transformed (information preserved in compressed form) | Deleted from context (gone, though logged) |
| **LLM involvement** | Full LLM call to generate summary | LLM call for boundary detection only (lightweight) |
| **Interactive** | Yes -- preview/approve/revise/fork loop | Optional -- shows what will be pruned, confirm |
| **Automatic** | Never (always user-initiated) | Yes -- auto-prune at 80% as safety net |
| **Speed** | Slow (full generation) | Fast (boundary detection + deletion) |

**Smart boundary detection:**

When pruning, the LLM is asked to identify logical topic transition points in the conversation rather than cutting at an arbitrary message index. The boundary
detection request is a lightweight LLM call:

```
System prompt:
"You are analyzing a conversation to find topic transition points. Identify the message
indices where the conversation shifts to a new topic or task. Return only the indices
as a comma-separated list, ordered from oldest to newest. A good transition point is
where the user starts a new request, shifts focus to a different part of the codebase,
or begins a new line of inquiry."

Messages: [first N messages of the conversation, enough to cover the prune target]
```

The pruner selects the transition point closest to (but not exceeding) the target token reduction and prunes everything before it.

**Invariant: pruning is always a contiguous prefix deletion.** The pruner deletes messages 0 through N and keeps messages N+1 through the end. It never skips
messages or selectively preserves islands of older content while deleting surrounding messages. The LLM's role is strictly to identify the best value of N — the
optimal cut point at a topic boundary. The conversation after pruning is always a contiguous suffix of the original, preserving the natural flow of the remaining
topics. This invariant prevents incoherent context states where the LLM sees conclusions without premises or responses without the requests that prompted them.

If boundary detection fails (LLM error, timeout, or unparseable response), the pruner falls back to the existing `EvictionContextCompactor` behavior (oldest-first
with tool-pair preservation) as a degraded-but-safe fallback. The fallback also respects the prefix-deletion invariant — it walks backward from the newest message
and the cut point is wherever the token budget is satisfied.

**`/context prune` user-facing command:**

Added as a subcommand of `/context`:

```
/context prune         Prune older topics to free context space
```

Interactive flow:
1. Calculate how many tokens need to be freed (target: reduce to 50% of context limit, same as current compaction target)
2. Run boundary detection to find topic transitions
3. Show preview: "Will prune {N} messages (topics: {topic summaries}). Estimated savings: {X} tokens."
4. Confirm with `SpectreHelpers.Confirm("Prune?", defaultValue: true)` (default Yes, since the user explicitly asked)
5. On confirm: prune messages, prepend compaction notice, log event
6. On decline: cancel

Non-interactive mode: auto-prune without confirmation (same as auto-prune behavior).

**Tiered context management strategy:**

The three mechanisms form a tiered defense:

```
 65-70%   Proactive warning (user's input turn)
           "Context at 72%. Consider /context summarize or /context prune."
           → User has time to choose the right strategy
           → Fires once per threshold crossing, does not nag

 80%      Auto-prune (during agentic loop, silent)
           "Pruned 12 messages at topic boundary (freed ~8,400 tokens)."
           → Fast, no user interruption, finds smart topic boundaries
           → Fallback to blind eviction if boundary detection fails
           → One-liner notification, does not block the agentic loop

 90%+     Emergency — should never happen in practice
           → If auto-prune at 80% was insufficient (extremely rare),
             a second auto-prune fires with more aggressive targets
           → No summarization — too slow for an emergency safety net
```

This design ensures:
- **Long-running conversations never stall** waiting for user input
- **Users who care** get a warning at 70% with time to summarize or prune thoughtfully
- **Users who don't care** get auto-prune as a sensible, topic-aware default
- **Nobody hits the hard wall** — the 80% auto-prune prevents context overflow

**`EvictionContextCompactor` becomes the fallback:**

The existing `EvictionContextCompactor` is retained as the degraded fallback when LLM-assisted boundary detection is unavailable (provider error, timeout, or
non-interactive terminal where the LLM call would add unacceptable latency). `IContextCompactor` interface is unchanged; a new `SmartPruneCompactor` implements
it with LLM-assisted boundaries and delegates to `EvictionContextCompactor` on failure.

**`CompactIfNeededAsync` updated:**

`AgentOrchestrator.CompactIfNeededAsync` is updated to use `SmartPruneCompactor` instead of `EvictionContextCompactor` directly. The threshold remains at
`CompactionThresholdPercent` (default 80%). The one-liner notification after auto-prune includes the topic boundary information when available:

```
Pruned 12 older messages at topic boundary (freed ~8,400 tokens).
```

Or, on fallback:

```
Pruned 12 older messages (freed ~8,400 tokens).
```

### Types affected

| Type | Layer | Change |
|---|---|---|
| `ContextSlashCommand` | Presentation.Console | Token budget calculation, fork flow, four-option menu, `/context prune` subcommand |
| `AgentOrchestrator` | Application | Proactive warning check, `_contextWarningIssued` field, use `SmartPruneCompactor` |
| `AppSettings` | Domain | New `ContextWarningThresholdPercent` property (default 70) |
| `SmartPruneCompactor` | Application (new) | LLM-assisted topic boundary detection, delegates to `EvictionContextCompactor` on failure |
| `IConversationLogger` | Application | Extended `LogContextSummarizeAsync`, new `LogContextForkAsync` |
| `JsonlConversationLogger` | Infrastructure.Persistence | Implement extended/new log methods |
| `ActiveSession` | Application | No interface change (fork calls existing `Set` method) |
| `Session` | Domain | No change (fork creates a new instance via existing constructor) |
| `ISessionRepository` | Application | No interface change (fork calls existing `SaveAsync`) |
| `Conversation` | Domain | No change (fork creates a new instance, seeds via `AddUserMessage`) |
| `EvictionContextCompactor` | Infrastructure.Persistence | No interface change; retained as fallback for `SmartPruneCompactor` |

### Types not affected

- `LlmRequest`, `MetaPrompt`, `ILlmProvider`, `IExecutionEngine` -- no changes to the LLM API protocol or execution layer
- `IContextCompactor` -- interface unchanged; `SmartPruneCompactor` and `EvictionContextCompactor` both implement it

---

## Consequences

### Benefits

- **Users control context reduction.** The four-option menu (Apply, Fork, Revise, Cancel) gives users agency over what happens to their conversation history.
Forking preserves the original session while starting fresh with a focused summary.
- **Summaries fit the context window.** The token budget calculation and budget directive in the summarization prompt prevent summaries from consuming more space
than is available. The over-budget warning gives users an informed choice rather than a silent failure.
- **Tiered defense prevents both data loss and interruption.** The 70/80 split gives users a window to act (summarize, prune, or fork) before auto-prune fires.
Auto-prune at 80% is silent and fast — it never blocks the agentic loop. Users who care get agency; users who don't care get a sensible default. Long-running
conversations never stall waiting for user input.
- **Smart pruning preserves topic coherence.** LLM-assisted boundary detection ensures that auto-prune (and manual `/context prune`) cuts at logical topic
transitions rather than arbitrary message indices. This prevents the "half a topic" problem where eviction removes the setup of a discussion but keeps the
conclusions, leaving the LLM with missing context that causes behavior drift.
- **Clear distinction between summarize and prune.** Summarize condenses (transforms content, preserves information density). Prune drops (removes content, frees
maximum space). Users can choose the right tool for the situation. Both are visible in `/context` and have clear, distinct names.
- **Full reconstruction from logs.** Enriched `context_summarize` events (with summary text, instructions, token counts) and bidirectional `context_fork` events
mean the complete conversation state can be reconstructed from the JSONL log at any point in time — including across fork boundaries.
- **Incremental implementation.** The six changes (token budget, fork option, prune command, proactive warnings, enriched logging, four-option menu) can be
implemented and shipped independently. Suggested ordering: (1) enriched logging, (2) token budget, (3) proactive warnings, (4) smart prune + `/context prune`,
(5) fork option, (6) four-option menu. The fork option requires enriched logging; smart prune is independent of the summarization changes.

### Costs and risks

- **Boundary detection adds an LLM call to pruning.** Both `/context prune` and auto-prune at 80% require a lightweight LLM call to identify topic transitions.
This adds latency and cost compared to the current blind eviction. The fallback to `EvictionContextCompactor` when boundary detection fails ensures pruning never
blocks indefinitely. For auto-prune during an agentic loop, the boundary detection must be fast — the system prompt and message payload should be kept minimal.
- **Boundary detection quality varies by model.** Smaller or less capable models may produce poor topic boundaries (e.g., every message is a "transition"). The
pruner must validate the LLM's response (indices must be monotonically increasing, within range, and parseable) and fall back to eviction on invalid output. A
minimum gap between transition points (e.g., at least 3 messages) may be needed to prevent over-segmentation.
- **Fork creates session management complexity.** Users who fork frequently will accumulate sessions. The existing `/conversations list` and `/conversations delete`
commands handle cleanup, but there is no visual indication of fork relationships in the session list. A future enhancement may add a `ForkedFrom` property to
`Session` to enable tree-style session listing.
- **Auto-naming adds latency and cost.** The fork flow sends an extra `LlmRequest` to name the new session. This is a small, fast request (short prompt, no tools,
no streaming), but it is a billable API call. The fallback name ("Fork of {id}") ensures the flow completes even if the naming request fails.
- **Token estimation remains crude.** The `chars / 4` heuristic used for budget calculation is the same heuristic used everywhere else in the codebase
(`Conversation.EstimateTokenCount`, `EvictionContextCompactor.EstimateMessageTokens`, `ContextSlashCommand.EstimateStringTokens`). The budget calculation
amplifies the impact of estimation error — an overestimate wastes available space, an underestimate produces summaries that are too large. Replacing the
heuristic with a provider-specific tokenizer is outside the scope of this ADR but would improve all context management decisions.
- **`IConversationLogger` breaking change.** `LogContextSummarizeAsync` gains four new parameters. All callers (currently `ContextSlashCommand` only) and all
implementations (`JsonlConversationLogger`) must be updated together. This is a small surface area change but requires coordination.
- **Proactive warning threshold is a heuristic.** The default 70% may be too early for users with large context windows (1M+ tokens on Gemini) or too late for
users with small windows. Making it configurable via `AppSettings` mitigates this, but the default must be chosen carefully. The 70/80 split (warn at 70, auto-
prune at 80) provides a 10-point buffer that should be sufficient for most usage patterns.
- **Re-initialization of `IConversationLogger` for forked sessions.** The current `InitializeAsync(sessionId)` contract assumes a single initialization per logger
lifetime. The fork flow either needs to call `InitializeAsync` with the new session ID (if the logger supports re-initialization) or resolve a new
`IConversationLogger` instance from the DI container. The implementation must handle this cleanly — likely by having `JsonlConversationLogger` close the current
file handle and open a new one on re-initialization.
