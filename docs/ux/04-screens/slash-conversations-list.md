# Screen: /conversations list

## Overview

The conversations list screen displays a table of saved conversations sorted by
last access time, showing the most recent 20 sessions. The current active
session is highlighted with a green ID and asterisk marker. A Name column
appears between ID and Project; when a session carries a name, the Preview
column shows `[dim]--[/]` because the name already serves as the identifier, so
the preview is redundant. It provides a quick inventory for users to find
conversations they want to resume, rename, or delete.

**Screen IDs**: SESS-02, SESS-03

## Trigger

- User types `/conversations list` during an active session.
- Handled by `ConversationsSlashCommand.HandleListAsync()`.

## Layout (80 columns)

### With Sessions

```

  ID            Name         Project      Messages  Last accessed     Preview

  abc12345 *    Auth work    my-project          24  2026-02-27 14:30  --
  def67890      --           api-server          12  2026-02-26 09:15  Fix the auth bug...
  ghi24680      --           --                   3  2026-02-25 18:00  --
  jkl13579      Refactoring  my-project          48  2026-02-24 11:45  --


  * = current session
```

### Empty

```
No saved conversations found.
```

### Anatomy

1. **Table** -- `SpectreHelpers.SimpleTable` with Simple border style.
   Six columns: ID, Name, Project, Messages (right-aligned), Last accessed,
   Preview.
2. **ID column** -- For the current session: green text + dim asterisk
   suffix ` *`. For other sessions: plain escaped text.
3. **Name column** -- `Markup.Escape(session.Name)` for named sessions,
   otherwise `[dim]--[/]`.
4. **Project column** -- Project name if set, otherwise `[dim]--[/]`.
5. **Messages column** -- Integer count, right-aligned (column index `[3]`).
6. **Last accessed column** -- `yyyy-MM-dd HH:mm` format in local time.
7. **Preview column** -- When the session has a name, always `[dim]--[/]`
   (name is already the identifier). When the session has no name, shows the
   first user message text, truncated to 60 characters, rendered in dim.
   If no user message exists: `[dim]--[/]`.
8. **Footer** -- Only shown when there is an active session. Blank line
   followed by dim hint: `  * = current session`.

## States

| State | Condition | Visual Difference |
|---|---|---|
| With sessions (has current) | Sessions exist and one matches active | Table with green-highlighted current session ID + asterisk; footer with legend |
| With sessions (no current) | Sessions exist but none is active | Table with all plain IDs; no footer |
| Empty | No saved sessions | Plain text: "No saved conversations found." |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green]` | success-green (1.1) | Current session ID |
| `[dim]*[/]` | dim + active indicator (2.2, 3.1) | Asterisk marking current session |
| `[dim]--[/]` | dim + no-data indicator (2.2, 3.1) | Missing name, project name, or message preview |
| `[dim]` | dim (2.2) | Preview text, footer legend text |
| `[bold]` | bold (2.2) | Table column headers (via SimpleTable) |

## Interactive Elements

None. This is a read-only table display.

## Behavior

- **Sorting**: Sessions are sorted by `LastAccessedAt` descending (most
  recent first).

- **Limit**: Maximum 20 sessions displayed. No pagination or "show more"
  mechanism exists.

- **Preview text**: Extracted by `GetFirstMessagePreview()`, which finds the
  first message with `Role == User`, extracts the text from its first
  `TextBlock`, replaces line endings with spaces, trims, and truncates to
  60 characters with `...` suffix. If no user message exists, returns
  `[dim]--[/]`. If the message contains only tool use/result blocks,
  returns `[tool: {name}]` or `[tool result]`.

- **Preview suppression**: When `session.Name is not null`, the Preview column
  always renders `[dim]--[/]` regardless of message content. The name field
  already gives the session a human-readable label.

- **Current session detection**: Compares `_activeSession.Session?.Id` with
  each listed session's ID.

## Edge Cases

- **Very long session IDs**: IDs are typically short (8+ chars), but the
  table column expands to fit. At 80 columns, very long IDs may compress
  other columns.

- **Many sessions (> 20)**: Only the 20 most recently accessed are shown.
  Older sessions are silently omitted. No indication is given that sessions
  were truncated.

- **Non-interactive/piped terminal**: Renders normally. Spectre strips markup
  from piped output but the table structure is preserved in plain text.

- **Narrow terminal**: The SimpleTable border and 6 columns may wrap at
  terminals narrower than ~80 columns, breaking alignment. The Preview and
  Name columns are the most compressible.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Simple Table | Section 4 | Conversation list table |
| Empty State | Section 13 | "No saved conversations found." |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| HandleListAsync | `Commands/ConversationsSlashCommand.cs` | `HandleListAsync` | 76-129 |
| Empty state | `Commands/ConversationsSlashCommand.cs` | `HandleListAsync` | 80-84 |
| Sort + limit | `Commands/ConversationsSlashCommand.cs` | `HandleListAsync` | 86-89 |
| Table construction | `Commands/ConversationsSlashCommand.cs` | `HandleListAsync` | 91 |
| Row population | `Commands/ConversationsSlashCommand.cs` | `HandleListAsync` | 94-119 |
| Current session footer | `Commands/ConversationsSlashCommand.cs` | `HandleListAsync` | 124-128 |
| Preview extraction | `Commands/ConversationsSlashCommand.cs` | `GetFirstMessagePreview` | 294-306 |
| Message text extraction | `Commands/ConversationsSlashCommand.cs` | `GetMessageText` | 308-335 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
