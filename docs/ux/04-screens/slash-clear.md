# Screen: /clear

## Overview

The clear screen removes all messages from the active session's conversation
history. It is an immediate, non-confirmable operation -- messages are cleared,
the session is auto-saved, and a success message is displayed. This is the
simplest slash command in the application.

**Screen IDs**: CLEAR-01, CLEAR-02

## Trigger

- User types `/clear` during an active session.
- Handled by `ClearSlashCommand.TryHandleAsync()`.

## Layout (80 columns)

### Success

```
  v Cleared 24 message(s) from conversation history.
```

### No Session

```
Error: No active session.
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Success | Session exists | Green "v" + message count cleared |
| Success (empty) | Session exists but has 0 messages | Green "v" + "Cleared 0 message(s) from conversation history." |
| No session | Session is null | Red "Error:" + "No active session." |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green]v[/]` | success-green + success indicator (1.1, 3.1) | Success prefix |
| `[red]Error:[/]` | error-red (1.1) | Error prefix |

## Interactive Elements

None. No confirmation prompt. The operation is immediate.

## Behavior

- **Clear**: `session.Conversation.Clear()` removes all messages and returns
  the count of messages that were removed.

- **Logging**: The clear event is logged via `IConversationLogger
  .LogContextClearAsync()` with the cleared message count.

- **Auto-save**: The session is immediately saved via `ISessionRepository
  .SaveAsync()`. This persists the now-empty conversation to disk.

- **No confirmation**: Unlike `/sessions delete`, this command does not
  prompt for confirmation. This is intentional -- clearing conversation
  history is recoverable (the user can continue the conversation, and old
  messages are in the JSONL log file), while session deletion is permanent.

- **System prompt preserved**: Only conversation messages are cleared. The
  session's system prompt, project association, and all other session
  metadata are unaffected.

## Edge Cases

- **Already empty conversation**: Clearing an empty conversation returns 0
  and succeeds. The message reads "Cleared 0 message(s)."

- **Non-interactive/piped terminal**: Renders normally. No prompts involved.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | Section 1 | Success and error messages |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| TryHandleAsync | `Commands/ClearSlashCommand.cs` | `TryHandleAsync` | 28-49 |
| No session guard | `Commands/ClearSlashCommand.cs` | `TryHandleAsync` | 36-41 |
| Clear + count | `Commands/ClearSlashCommand.cs` | `TryHandleAsync` | 43 |
| Logging | `Commands/ClearSlashCommand.cs` | `TryHandleAsync` | 44 |
| Auto-save | `Commands/ClearSlashCommand.cs` | `TryHandleAsync` | 45 |
| Success message | `Commands/ClearSlashCommand.cs` | `TryHandleAsync` | 47 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
