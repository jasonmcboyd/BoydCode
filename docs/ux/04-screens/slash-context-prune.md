# Screen: /context prune

## Overview

The context prune screen is an interactive command that uses LLM-assisted
topic boundary detection to intelligently remove older messages from the
conversation, freeing context window space. The LLM analyzes the conversation
to find natural topic transition points, then prunes messages before the best
boundary. If the LLM fails to detect boundaries, the command falls back to
simple eviction-based compaction (oldest messages first).

The prune target is half the context window capacity. The command shows a
before/after summary and asks for confirmation before committing.

**Screen IDs**: CTX-09, CTX-10, CTX-11, CTX-12, CTX-13, CTX-14

## Trigger

- User types `/context prune` during an active session.
- Handled by `ContextSlashCommand.HandlePruneAsync()`.

## Layout (80 columns)

### Confirmation Dialog

```
(blank line)
  Will prune 12 messages (estimated savings: 4,200 tokens).
  38 messages, ~12,400 tokens -> 26 messages, ~8,200 tokens
(blank line)
Prune? [y/n] (y): _
```

### After Confirm (Success)

```
  v Pruned 12 messages (freed ~4,200 tokens).
```

### After Cancel

```
  Cancelled.
```

### Nothing to Prune

```
Nothing to prune -- conversation is within target size.
```

### Too Few Messages

```
Not enough conversation to prune (need at least 4 messages).
```

### No Active Session

```
Error: No active session.
```

### No Provider

```
Error: No LLM provider configured.
```

### Pruning Failed

```
Error: Pruning failed: Connection timed out.
```

### Anatomy

1. **Guards** -- No session, no provider, and too-few-messages checks run
   before any LLM call. Errors are rendered and the command returns.
2. **Activity bar** -- `ActivityState.Thinking` is set before the LLM-
   assisted compaction call. `ActivityState.Idle` is restored in a `finally`
   block after the compaction completes or fails.
3. **Nothing-to-prune check** -- If the compactor returns the same or more
   messages than the original, the conversation is already within the target
   size and a dim message is shown.
4. **Summary lines** -- Two lines at 2-space indent:
   - Bold message count and bold estimated token savings
   - Dim before/after breakdown showing message counts and token estimates
5. **Confirmation prompt** -- `SpectreHelpers.Confirm("Prune?", defaultValue:
   true)`. Default is yes. Only shown in interactive terminals.
6. **Success message** -- Green checkmark with pruned count and freed tokens.

## States

| State | Condition | Visual |
|---|---|---|
| Confirmation | Compaction produced fewer messages | Summary + confirm prompt |
| Success | User confirms (or non-interactive) | Green "v" + pruned count and tokens freed |
| Cancelled | User declines confirmation | Dim "Cancelled." |
| Nothing to prune | Compaction returns same/more messages | Dim informational message |
| Too few messages | < 4 messages in conversation | Plain text explaining minimum requirement |
| No session | Session is null | Red "Error:" + "No active session." |
| No provider | `ActiveProvider.IsConfigured` is false | Red "Error:" + "No LLM provider configured." |
| Prune failed | Compaction throws (non-cancellation) | Red "Error:" + "Pruning failed: {message}" |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | Pruned message count, estimated token savings in summary line |
| `[dim]` | dim (2.2) | Before/after breakdown line; "Nothing to prune" message; "Cancelled." |
| `[green]✓[/]` | success-green + success indicator (1.1, 3.1) | Success message prefix |
| `[red]Error:[/]` | error-red (1.1) | Error prefix for all error states |

## Interactive Elements

| Element | Type | Condition |
|---|---|---|
| Prune confirmation | `SpectreHelpers.Confirm` | Rendered after summary; interactive terminals only |

## Behavior

- **Target calculation**: The prune target is half the context window
  capacity. `MaxContextWindowTokens` from the provider capabilities is used
  if positive; otherwise, `AppSettings.ContextWindowTokenLimit` is the
  fallback.

- **Compaction**: `IContextCompactor.CompactAsync` is called with the
  conversation and target token count. The default implementation is
  `SmartPruneCompactor`, which:
  1. Sends a numbered message summary to the LLM asking for topic boundary
     indices.
  2. Parses the response for monotonically increasing, in-range boundary
     indices with a minimum gap of 3.
  3. Prunes at the boundary that brings the conversation closest to the
     target token count while removing a contiguous prefix.
  4. Falls back to `EvictionContextCompactor` (oldest-first eviction) if
     boundary detection fails.

- **Before/after snapshot**: Before and after message counts and estimated
  token counts are captured and compared.

- **Non-interactive fallback**: When `_ui.IsInteractive` is false, the prune
  is applied immediately without the confirmation prompt.

- **Activity bar**: `ActivityState.Thinking` is set during the compaction
  call (which may involve an LLM request for boundary detection). Always
  restored to `ActivityState.Idle` in the `finally` block.

- **Message replacement**: On confirmation, `conversation.ReplaceMessages()`
  is called with the compacted message list. This replaces the conversation's
  message list in-place.

- **Logging**: After successful prune, the event is logged via
  `IConversationLogger.LogContextCompactionAsync()` with before and after
  message counts and token estimates.

## Edge Cases

- **Conversation already within target**: The compactor may return the
  original messages if no pruning is beneficial. The "Nothing to prune"
  message is shown without prompting.

- **Exactly 4 messages**: The minimum 4-message check allows pruning. With
  very few messages, the compactor may find nothing worth pruning (especially
  if messages are small). The "Nothing to prune" state handles this
  gracefully.

- **Boundary detection failure**: If the LLM call for boundary detection
  fails (network error, empty response, unparseable output), the
  `SmartPruneCompactor` falls back to `EvictionContextCompactor`. The user
  sees the same confirmation flow regardless of which compaction strategy
  was used internally.

- **Compaction exception**: If the compaction call itself throws (both smart
  and fallback fail), a red error is shown with the exception message. The
  conversation is not modified.

- **Cancellation**: `OperationCanceledException` propagates without being
  caught, allowing the outer cancellation flow to handle it. The
  conversation is not modified.

- **Non-interactive/piped terminal**: Auto-applies the prune without the
  confirmation prompt. Summary lines are still rendered for logging/output
  purposes.

- **Large conversations**: Conversations with many messages produce a longer
  summary sent to the LLM for boundary detection. The numbered summary
  format is compact (one line per message with a 100-character preview).

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | Section 1 | Success, error, cancelled, and informational messages |
| Confirm Prompt | Section 6 | "Prune?" yes/no confirmation |
