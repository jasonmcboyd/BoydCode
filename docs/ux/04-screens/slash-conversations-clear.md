# Screen: /conversations clear

## Overview

The conversations clear screen removes all messages from the active session's
conversation history. It is an immediate, non-confirmable operation -- messages
are cleared, the session is auto-saved, and a success message is displayed.
This is the simplest subcommand of `/conversations`.

**Screen IDs**: CLEAR-01, CLEAR-02

## Trigger

- User types `/conversations clear` during an active session.
- Handled by `ConversationsSlashCommand.HandleClearAsync()`.

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

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Semantic.Success` with `Theme.Symbols.Check`
(green `✓` success prefix), `Theme.Semantic.Error` (red "Error:" prefix).

## Interactive Elements

None. No confirmation prompt. The operation is immediate.

## Behavior

- **Clear**: `session.Conversation.Clear()` removes all messages and returns
  the count of messages that were removed.

- **Logging**: The clear event is logged via `IConversationLogger
  .LogContextClearAsync()` with the cleared message count.

- **Auto-save**: The session is immediately saved via `ISessionRepository
  .SaveAsync()`. This persists the now-empty conversation to disk.

- **No confirmation**: Unlike `/conversations delete`, this command does not
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
| Status Message | #7 | Success and error messages |

