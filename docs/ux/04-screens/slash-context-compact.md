# Screen: /context compact

## Overview

The context compact screen handles manual triggering of conversation
compaction -- removing older messages to free up context window space. It is a
quick operational command that produces a single success or error line. The
heavy lifting is delegated to `IContextCompactor`; this screen is purely the
trigger and result display.

**Screen IDs**: CTX-04, CTX-05, CTX-06

## Trigger

- User types `/context compact` during an active session.
- Handled by `ContextSlashCommand.HandleCompactAsync()`.

## Layout (80 columns)

### Success

```
  v Compacted: 12 message(s) removed. Estimated tokens: 4,200
```

### Nothing to Compact

```
Nothing to compact.
```

### No Active Session

```
Error: No active session.
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Success | Messages exist and compaction removes some | Green "v" + compaction summary with message count removed and new estimated token count |
| Nothing to compact | Conversation has 0 messages | Plain text: "Nothing to compact." |
| No session | Session is null | Red "Error:" prefix + "No active session." |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green]v[/]` | success-green + success indicator (1.1, 3.1) | Success prefix |
| `[red]Error:[/]` | error-red (1.1) | Error prefix |

## Interactive Elements

None. This is a non-interactive command that produces immediate output.

## Behavior

- **Target tokens**: The compactor targets `contextLimit / 2` tokens. The
  context limit comes from `ILlmProvider.Capabilities.MaxContextWindowTokens`
  if available, falling back to `AppSettings.ContextWindowTokenLimit`.

- **Compaction process**: Delegates to `IContextCompactor.CompactAsync()`,
  which evicts older messages until the estimated token count is at or below
  the target. The compacted message list replaces the current conversation
  messages via `conversation.ReplaceMessages()`.

- **Token counting**: Before and after token counts are obtained via
  `conversation.EstimateTokenCount()`. The success message shows the "after"
  count formatted with `N0` (thousands separator).

- **Logging**: After successful compaction, the event is logged via
  `IConversationLogger.LogContextCompactionAsync()` with before/after message
  counts and token counts.

- **Rendering**: Uses `SpectreHelpers.Success()` for the success message
  (which escapes internally) and `SpectreHelpers.Error()` for errors.
  The "Nothing to compact" message is a raw `AnsiConsole.MarkupLine` with
  no special formatting.

## Edge Cases

- **Already compact**: If the conversation is already below the target token
  count, `CompactAsync` may return the same messages unchanged. The success
  message will show "0 message(s) removed."

- **Single message**: A conversation with 1 message may not compact further.
  The compactor decides what to evict based on its strategy.

- **Non-interactive/piped terminal**: Renders normally. No prompts involved.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | Section 1 | Success and error messages |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| HandleCompactAsync | `Commands/ContextSlashCommand.cs` | `HandleCompactAsync` | 397-426 |
| Target token calculation | `Commands/ContextSlashCommand.cs` | `HandleCompactAsync` | 413-416 |
| Compaction + message replacement | `Commands/ContextSlashCommand.cs` | `HandleCompactAsync` | 418-421 |
| Success message | `Commands/ContextSlashCommand.cs` | `HandleCompactAsync` | 423-424 |
| Logging | `Commands/ContextSlashCommand.cs` | `HandleCompactAsync` | 425 |
| No session guard | `Commands/ContextSlashCommand.cs` | `HandleCompactAsync` | 399-404 |
| Empty conversation guard | `Commands/ContextSlashCommand.cs` | `HandleCompactAsync` | 407-411 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
