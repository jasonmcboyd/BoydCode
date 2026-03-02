# Screen: /conversations rename

## Overview

The conversations rename screen attaches a human-readable label to a saved
conversation. When a name is provided as an inline argument, the rename is
immediate (no dialog needed). When no name is provided and the terminal is
interactive, a Form Dialog (component pattern #31) prompts for the new name.
In non-interactive mode without an inline name, a usage hint is displayed
instead.

The name appears in the Name column of `/conversations list` and in the Name
row of `/conversations show`.

**Screen IDs**: CONV-01

## Trigger

- User types `/conversations rename <id> [name]` during an active session.
- Handled by `ConversationsSlashCommand.HandleRenameAsync()`.

## Layout (80 columns)

### Rename Dialog (no inline name)

```
+-- Rename Conversation ------------------------------------+
|                                                            |
|  Name:  [Auth work                                      ]  |
|                                                            |
|                              [ Cancel ]  [ Ok ]            |
|                                                            |
+------------------------------------------------------------+
```

The `TextField` is pre-filled with the current session name (if one exists).
Ok is `IsDefault = true`. The dialog title is "Rename Conversation".

### Rename Dialog -- Pre-filled with Current Name

When the session already has a name, the TextField shows it:

```
|  Name:  [Auth work                                      ]  |
```

When the session has no name, the TextField is empty:

```
|  Name:  [                                               ]  |
```

### Rename Dialog -- Validation Error

```
+-- Rename Conversation ------------------------------------+
|                                                            |
|  Name:  [                                               ]  |
|         Name cannot be empty.                              |
|                                                            |
|                              [ Cancel ]  [ Ok ]            |
|                                                            |
+------------------------------------------------------------+
```

The Ok button validates non-empty input. The error message uses
`Theme.Semantic.Error` (bright red).

### Success

After confirming or providing an inline name:

```
  v Session 'abc12345' renamed to 'Auth work'.
```

### Cancelled

After clicking Cancel or pressing Esc:

```
  Cancelled.
```

### Not Found

```
  Error: Session abc12345 not found.
```

### Usage (no ID)

```
  Usage: /conversations rename <id> [name]
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Success | Session found, name saved | Green checkmark + session ID and new name in single quotes |
| Not found | Session ID doesn't match any saved session | Red "Error:" + bold session ID |
| Usage | No ID argument provided | Yellow "Usage:" hint |
| Rename dialog | ID provided, no name, interactive terminal | Form Dialog with Name TextField |
| Rename validation | Empty name in dialog | Red error below Name field |
| Cancelled | User clicks Cancel or Esc in dialog | Dim "Cancelled." |
| Non-interactive fallback | ID provided, no name, non-interactive terminal | Yellow "Usage:" hint |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Modal.BorderScheme` (blue border for the
rename dialog), `Theme.Semantic.Default` with `TextStyle.Bold` (bold "Name:"
label), `Theme.Input.Text` (white text in TextField),
`Theme.Semantic.Success` with `Theme.Symbols.Check` (green checkmark success
prefix), `Theme.Semantic.Error` (red validation error and "Error:" prefix),
`Theme.Semantic.Muted` (dim "Cancelled." message),
`Theme.Semantic.Warning` (yellow "Usage:" prefix).

All interaction occurs within a Terminal.Gui Dialog. No Terminal.Gui
suspension or Spectre prompts are needed.

## Interactive Elements

| Element | Type | Default | Condition |
|---|---|---|---|
| Name input | Form Dialog with TextField (pattern #31) | Pre-filled with current name | Only when `_ui.IsInteractive` is true and no inline name argument |

## Keyboard

| Key | Action |
|---|---|
| Enter | Confirm (Ok button, IsDefault) |
| Esc | Cancel and close dialog |
| Tab | Move between TextField and buttons |
| Shift+Tab | Move backward |

## Behavior

- **Argument parsing**: The command expects `<id>` as the first argument and
  treats everything after it as the optional `[name]` argument.

- **Session loading**: The session is loaded from `ISessionRepository.LoadAsync()`.
  If the repository returns null, the not-found error is displayed.

- **Inline name**: When a name argument is supplied on the command line, it is
  used directly without opening a dialog.

- **Dialog prompt**: When no name argument is supplied and
  `_ui.IsInteractive` is true, a Form Dialog opens with a single TextField
  labeled "Name:", pre-filled with the session's current name (if any). The
  Ok button validates non-empty input.

- **Non-interactive fallback**: When no name argument is supplied and
  `_ui.IsInteractive` is false, a usage hint is displayed and the command
  exits without renaming.

- **Save**: Sets `session.Name` and calls `ISessionRepository.SaveAsync()` to
  persist the change.

- **Success message**: Rendered in the conversation view. Both the session ID
  and the new name are wrapped in single quotes for readability.

## Edge Cases

- **Session ID with special characters**: The ID is escaped in all
  rendered contexts (success message, error message).

- **Name with special characters**: The name supplied by the user is escaped
  before being rendered in the success message.

- **Renaming the active session**: Allowed. The active session's name is
  updated in the repository immediately; the in-memory `ActiveSession` object
  reflects the change after save.

- **Non-interactive/piped terminal**: Usage hint is shown instead of opening
  a dialog. Inline name argument can be used to rename without interaction.

- **Pre-fill with current name**: If the session already has a name, the
  TextField pre-fills with it, allowing the user to edit rather than retype.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Form Dialog | #31 | Single-field dialog with Name TextField |
| Status Message | #7 | Success, error, cancelled, and usage messages |
