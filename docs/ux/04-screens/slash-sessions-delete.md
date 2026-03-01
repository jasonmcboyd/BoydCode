# Screen: /sessions delete

## Overview

The sessions delete screen provides a confirmation-gated deletion flow for
saved sessions. It shows the session's key metadata (ID, message count,
project) and asks for explicit confirmation before deleting. The active
session cannot be deleted.

**Screen IDs**: SESS-07, SESS-08, SESS-09, SESS-10, SESS-11, SESS-12

## Trigger

- User types `/sessions delete <id>` during an active session.
- Handled by `SessionsSlashCommand.HandleDeleteAsync()`.

## Layout (80 columns)

### Confirmation Prompt

```
  Delete session abc12345 (24 messages, project: my-project)?
  Delete? [y/N]
```

### Success

```
v Session abc12345 deleted.
```

### Cancelled

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
Usage: /sessions delete <id>
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Confirmation | Session found, not active, interactive | Detail summary + confirm prompt (default: No) |
| Success | User confirms deletion | Green "v" + "Session {id} deleted." with bold ID |
| Cancelled | User declines confirmation | Dim "Cancelled." |
| Active session error | Attempting to delete the current session | Red error: "Cannot delete the current active session." |
| Not found | Session ID doesn't match any saved session | Red error with bold session ID |
| Usage | No ID argument provided | Yellow "Usage:" hint |
| Non-interactive bypass | Non-interactive terminal | Skips confirmation, deletes directly |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | Session ID in confirmation message and success/error messages |
| `[green]v[/]` | success-green + success indicator (1.1, 3.1) | Success prefix |
| `[red]Error:[/]` | error-red (1.1) | Error prefix for active session and not-found |
| `[dim]Cancelled.[/]` | dim (2.2) | Cancellation message |
| `[yellow]Usage:[/]` | warning-yellow (1.1) | Usage hint prefix |

## Interactive Elements

| Element | Type | Default | Condition |
|---|---|---|---|
| Delete confirmation | `SpectreHelpers.Confirm` | No (`false`) | Only shown when `_ui.IsInteractive` is true |

## Behavior

- **Active session guard**: Before loading the session, the method checks if
  `_activeSession.Session?.Id` matches the requested ID. If so, the error
  is returned immediately without loading from the repository.

- **Session loading**: The session is loaded from `ISessionRepository.LoadAsync()`
  to verify existence and display metadata in the confirmation.

- **Confirmation flow**: In interactive mode, the confirmation message shows
  the session ID (bold), message count, and project name (or "(none)").
  The confirm prompt defaults to "No" to prevent accidental deletion.

- **Non-interactive bypass**: When `_ui.IsInteractive` is false, the
  confirmation prompt is skipped and deletion proceeds directly. This
  enables scripted/CI usage.

- **Deletion**: Delegates to `ISessionRepository.DeleteAsync()`. The
  repository handles file removal.

- **Success message**: Uses raw `AnsiConsole.MarkupLine` (not
  `SpectreHelpers.Success`) to allow bold markup on the session ID within
  the message.

## Edge Cases

- **Session ID with special characters**: The ID is `Markup.Escape`d in all
  rendered contexts (confirmation message, success message, error message).

- **Concurrent deletion**: If the session is deleted by another process
  between the `LoadAsync` and `DeleteAsync` calls, `DeleteAsync` may silently
  succeed or throw, depending on the repository implementation.

- **Non-interactive/piped terminal**: Confirmation is skipped; deletion
  proceeds immediately after the guard checks pass.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Confirmation Prompt | Section 8 | Delete confirmation with default No |
| Status Message | Section 1 | Success, error, cancelled, and usage messages |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| HandleDeleteAsync | `Commands/SessionsSlashCommand.cs` | `HandleDeleteAsync` | 178-218 |
| Usage guard | `Commands/SessionsSlashCommand.cs` | `HandleDeleteAsync` | 180-184 |
| Active session guard | `Commands/SessionsSlashCommand.cs` | `HandleDeleteAsync` | 188-192 |
| Not found guard | `Commands/SessionsSlashCommand.cs` | `HandleDeleteAsync` | 194-199 |
| Confirmation prompt | `Commands/SessionsSlashCommand.cs` | `HandleDeleteAsync` | 201-213 |
| Deletion | `Commands/SessionsSlashCommand.cs` | `HandleDeleteAsync` | 216 |
| Success message | `Commands/SessionsSlashCommand.cs` | `HandleDeleteAsync` | 217 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
