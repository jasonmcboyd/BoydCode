# Screen: /conversations rename

## Overview

The conversations rename screen attaches a human-readable label to a saved
conversation. The name appears in the Name column of `/conversations list` and
in the Name row of `/conversations show`. When a name is provided as an inline
argument, the rename is immediate. When no name is provided and the terminal is
interactive, the user is prompted to enter one. In non-interactive mode without
an inline name, a usage hint is displayed instead.

**Screen IDs**: CONV-01

## Trigger

- User types `/conversations rename <id> [name]` during an active session.
- Handled by `ConversationsSlashCommand.HandleRenameAsync()`.

## Layout (80 columns)

### Success

```
v Session 'abc12345' renamed to 'Auth work'.
```

### Not Found

```
Error: Session abc12345 not found.
```

### Usage (no ID)

```
Usage: /conversations rename <id> [name]
```

### Interactive Prompt (no inline name, interactive terminal)

```
  Name: _
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Success | Session found, name saved | Green "v" + session ID and new name in single quotes |
| Not found | Session ID doesn't match any saved session | Red "Error:" + bold session ID |
| Usage | No ID argument provided | Yellow "Usage:" hint |
| Interactive prompt | ID provided, no name, interactive terminal | `SpectreHelpers.PromptNonEmpty` text prompt with "  Name: " label |
| Non-interactive fallback | ID provided, no name, non-interactive terminal | Yellow "Usage:" hint |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green]✓[/]` | success-green + success indicator (1.1, 3.1) | Success prefix |
| `[red]Error:[/]` | error-red (1.1) | Error prefix |
| `[yellow]Usage:[/]` | warning-yellow (1.1) | Usage hint prefix |
| `[bold]` | bold (2.2) | Session ID in not-found error message |

## Interactive Elements

| Element | Type | Default | Condition |
|---|---|---|---|
| Name prompt | `SpectreHelpers.PromptNonEmpty("  Name: ")` | (none) | Only when `_ui.IsInteractive` is true and no inline name argument was supplied |

## Behavior

- **Argument parsing**: The command expects `<id>` as the first argument and
  treats everything after it as the optional `[name]` argument.

- **Session loading**: The session is loaded from `ISessionRepository.LoadAsync()`.
  If the repository returns null, the not-found error is displayed.

- **Inline name**: When a name argument is supplied on the command line, it is
  used directly without prompting.

- **Interactive prompt**: When no name argument is supplied and
  `_ui.IsInteractive` is true, `SpectreHelpers.PromptNonEmpty("  Name: ")`
  is called to collect a non-empty string from the user.

- **Non-interactive fallback**: When no name argument is supplied and
  `_ui.IsInteractive` is false, a usage hint is displayed and the command
  exits without renaming.

- **Save**: Sets `session.Name` and calls `ISessionRepository.SaveAsync()` to
  persist the change.

- **Success message**: Uses `SpectreHelpers.Success()`. Both the session ID and
  the new name are wrapped in single quotes for readability.

## Edge Cases

- **Session ID with special characters**: The ID is `Markup.Escape`d in all
  rendered contexts (success message, error message).

- **Name with special markup characters**: The name supplied by the user is
  `Markup.Escape`d before being rendered in the success message.

- **Renaming the active session**: Allowed. The active session's name is
  updated in the repository immediately; the in-memory `ActiveSession` object
  reflects the change after save.

- **Non-interactive/piped terminal**: Usage hint is shown instead of prompting.
  Inline name argument can be used to rename without interaction.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | Section 1 | Success, error, and usage messages |
| Text Prompt | Section 7 | Non-empty name collection via `PromptNonEmpty` |

