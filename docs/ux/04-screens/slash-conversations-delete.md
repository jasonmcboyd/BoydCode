# Screen: /conversations delete

## Overview

The conversations delete screen provides a confirmation-gated deletion flow for
saved conversations. It uses a `MessageBox.Query` (component pattern #15, Dialog
approach) showing the session's key metadata (ID, message count, project) and
asks for explicit confirmation before deleting. The active session cannot be
deleted.

**Screen IDs**: SESS-07, SESS-08, SESS-09, SESS-10, SESS-11, SESS-12

## Trigger

- User types `/conversations delete <id>` during an active session.
- Handled by `ConversationsSlashCommand.HandleDeleteAsync()`.

## Layout (80 columns)

### Confirmation MessageBox

```
+-- Delete Conversation ------------------------------------+
|                                                            |
|  Delete conversation 'abc12345'?                           |
|                                                            |
|    * Messages: 24                                          |
|    * Project: my-project                                   |
|                                                            |
|                            [ Cancel ]  [ Delete ]          |
|                                                            |
+------------------------------------------------------------+
```

The "Cancel" button is pre-focused (safe default). The "Delete" button uses
`Theme.Semantic.Error` (bright red) styling to indicate a destructive action.
The session ID is drawn bold. Detail bullets use the `*` character.

### Confirmation -- No Project

```
|    * Messages: 24                                          |
|    * Project: (none)                                       |
```

When the session has no associated project, "(none)" is shown in
`Theme.Semantic.Muted` (dim).

### Confirmation -- With Name

When the session has a user-assigned name:

```
+-- Delete Conversation ------------------------------------+
|                                                            |
|  Delete conversation 'Auth work' (abc12345)?               |
|                                                            |
|    * Messages: 24                                          |
|    * Project: my-project                                   |
|                                                            |
|                            [ Cancel ]  [ Delete ]          |
|                                                            |
+------------------------------------------------------------+
```

### Success

After confirming deletion, the MessageBox closes and a success message is
rendered in the conversation view:

```
  v Session abc12345 deleted.
```

### Cancelled

After clicking Cancel or pressing Esc, the MessageBox closes:

```
  Cancelled.
```

### Active Session Error

```
  Error: Cannot delete the current active session.
```

### Not Found

```
  Error: Session abc12345 not found.
```

### Usage (no ID)

```
  Usage: /conversations delete <id>
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Confirmation | Session found, not active, interactive | MessageBox with detail + Cancel/Delete buttons |
| Success | User clicks Delete | MessageBox closes; green checkmark + message in conversation |
| Cancelled | User clicks Cancel or presses Esc | MessageBox closes; dim "Cancelled." in conversation |
| Active session error | Attempting to delete the current session | Red error in conversation, no MessageBox |
| Not found | Session ID doesn't match any saved session | Red error with bold session ID |
| Usage | No ID argument provided | Yellow "Usage:" hint |
| Non-interactive bypass | Non-interactive terminal | Skips confirmation, deletes directly |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Modal.BorderScheme` (blue border for the
MessageBox dialog), `Theme.Semantic.Error` (bright red "Delete" button and
"Error:" prefix), `Theme.Semantic.Default` with `TextStyle.Bold` (bold
session ID in confirmation message), `Theme.Semantic.Success` with
`Theme.Symbols.Check` (green checkmark success prefix),
`Theme.Semantic.Muted` (dim "Cancelled." message, "(none)" project label),
`Theme.Semantic.Warning` (yellow "Usage:" prefix).

All interaction occurs within a Terminal.Gui MessageBox. No Terminal.Gui
suspension or Spectre prompts are needed.

## Interactive Elements

| Element | Type | Default | Condition |
|---|---|---|---|
| Delete confirmation | MessageBox.Query (pattern #15) | Cancel (pre-focused) | Only shown when `_ui.IsInteractive` is true |

## Keyboard

| Key | Action |
|---|---|
| Enter | Confirm focused button (Cancel by default) |
| Tab | Switch between Cancel and Delete buttons |
| Esc | Cancel (same as clicking Cancel) |

## Behavior

- **Active session guard**: Before loading the session, the method checks if
  `_activeSession.Session?.Id` matches the requested ID. If so, the error
  is returned immediately without loading from the repository.

- **Session loading**: The session is loaded from `ISessionRepository.LoadAsync()`
  to verify existence and display metadata in the confirmation MessageBox.

- **Confirmation flow**: In interactive mode, a `MessageBox.Query` is shown
  with the title "Delete Conversation" and a detail message showing the
  session ID (bold), message count, and project name (or "(none)").
  The "Cancel" button is pre-focused to prevent accidental deletion.

- **Non-interactive bypass**: When `_ui.IsInteractive` is false, the
  confirmation MessageBox is skipped and deletion proceeds directly. This
  enables scripted/CI usage.

- **Deletion**: Delegates to `ISessionRepository.DeleteAsync()`. The
  repository handles file removal.

- **Success message**: Rendered in the conversation view. The session ID is
  escaped before being embedded in the message.

## Edge Cases

- **Session ID with special characters**: The ID is escaped in all rendered
  contexts (confirmation message, success message, error message).

- **Concurrent deletion**: If the session is deleted by another process
  between the `LoadAsync` and `DeleteAsync` calls, `DeleteAsync` may silently
  succeed or throw, depending on the repository implementation.

- **Non-interactive/piped terminal**: Confirmation is skipped; deletion
  proceeds immediately after the guard checks pass.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Delete Confirmation | #15 (Dialog approach) | MessageBox with Cancel/Delete, Cancel pre-focused |
| Status Message | #7 | Success, error, cancelled, and usage messages |
