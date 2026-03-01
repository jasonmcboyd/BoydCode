# Screen: /conversations show

## Overview

The conversations show screen displays detailed metadata about a specific saved
conversation, including an info grid with key properties and a preview of the
first 5 messages with role-colored labels. When the session carries a name, an
additional Name row appears in the info grid between the Session and Created
rows. A resumption hint showing the CLI command to resume the session is shown
at the bottom.

**Screen IDs**: SESS-04, SESS-05, SESS-06

## Trigger

- User types `/conversations show <id>` during an active session.
- Handled by `ConversationsSlashCommand.HandleShowAsync()`.

## Layout (80 columns)

### Detail View (with name)

```
(blank line)
  Session   abc12345
  Name      Auth implementation work
  Created   2026-02-27 14:30     Last used   2026-02-27 16:45
  Project   my-project           Messages    24
  Directory C:\Users\jason\source\repos\my-project
(blank line)
  Recent messages
(blank line)
    user: Implement the new execution engine for Docker containers
    assistant: I'll help you implement the Docker container execution engine...
    user: Great, now let's add the volume mount builder
    assistant: I'll create the VolumeMountBuilder class. Let me start by...
    user: Can you also add tests for the mount builder?
(blank line)
    ... 19 more message(s)
(blank line)
  Resume with: boydcode --resume abc12345
(blank line)
```

### Detail View (no name)

```
(blank line)
  Session   abc12345
  Created   2026-02-27 14:30     Last used   2026-02-27 16:45
  Project   my-project           Messages    24
  Directory C:\Users\jason\source\repos\my-project
(blank line)
  Recent messages
...
```

### Not Found

```
Error: Session abc12345 not found.
```

### Usage (no ID)

```
Usage: /conversations show <id>
```

### Anatomy

1. **Blank line**.
2. **Info grid** -- `SpectreHelpers.InfoGrid()` with 4 columns.
   - Row 1: `Session` / ID (single value row).
   - Row 1a (conditional): `Name` / `session.Name` -- only present when
     `session.Name is not null`.
   - Row 2: `Created` / datetime, `Last used` / datetime (paired row).
   - Row 3: `Project` / name or "(none)", `Messages` / count (paired row).
   - Row 4: `Directory` / working directory path (single value row).
3. **Blank line**.
4. **Recent messages heading** -- 2-space indent, dim: "Recent messages".
5. **Blank line**.
6. **Message preview** -- First 5 messages, each on one line with 4-space
   indent. Role label is color-coded: `[blue]user[/]`, `[green]assistant[/]`,
   `[dim]system[/]`. Text is escaped and truncated to 120 characters.
7. **Overflow indicator** -- If more than 5 messages exist: 4-space indent,
   dim: `... N more message(s)`.
8. **Blank line**.
9. **Resume hint** -- 2-space indent, dim: `Resume with:` followed by the
   CLI command `boydcode --resume {id}`.
10. **Blank line**.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Detail view (with name) | Session found, has a name | Info grid includes Name row between Session and Created |
| Detail view (no name) | Session found, no name set | Info grid omits Name row |
| Detail view (with messages) | Session found, has messages | Full info grid + message preview + resume hint |
| Detail view (no messages) | Session found, 0 messages | Info grid only, no "Recent messages" section |
| Detail view (many messages) | Session found, > 5 messages | Message preview shows first 5 + dim "...N more" overflow |
| Not found | Session ID doesn't match any saved session | Red error with bold session ID |
| Usage | No ID argument provided | Yellow "Usage:" hint |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[dim]` | dim (2.2) | Info grid labels, "Recent messages" heading, overflow count, resume hint prefix |
| `[cyan]` | info-cyan (1.1) | Info grid values (via `SpectreHelpers.AddInfoRow`) |
| `[blue]` | info-blue (1.1) | User role label in message preview |
| `[green]` | success-green (1.1) | Assistant role label in message preview |
| `[dim]` | dim (2.2) | System role label in message preview |
| `[bold]` | bold (2.2) | Session ID in not-found error, table headers in info grid |
| `[red]Error:[/]` | error-red (1.1) | Error prefix |
| `[yellow]Usage:[/]` | warning-yellow (1.1) | Usage hint prefix |

## Interactive Elements

None. This is a read-only detail view.

## Behavior

- **Session loading**: The session is loaded from `ISessionRepository.LoadAsync()`.
  If the repository returns null, the not-found error is displayed.

- **Name row**: The Name row is conditionally added to the info grid only when
  `session.Name is not null`. Sessions without a name omit the row entirely
  rather than showing a placeholder.

- **Date formatting**: Both `CreatedAt` and `LastAccessedAt` are converted
  to local time and formatted as `yyyy-MM-dd HH:mm` using
  `CultureInfo.InvariantCulture`.

- **Project display**: Shows `session.ProjectName` or `"(none)"` if null.

- **Message text extraction**: `GetMessageText()` extracts text from the
  first `TextBlock` in a message's content. If no text block exists, it
  falls back to `[tool: {name}]` for tool use blocks, `[tool result]` for
  tool result blocks, or `--` for empty messages. Text has line endings
  replaced with spaces and is truncated to `maxLength` (120) with `...`.

- **Role coloring**: Message roles use non-standard color assignments:
  `[blue]user[/]` and `[green]assistant[/]`. This is noted in the style
  tokens audit (11.13) as a potential inconsistency with semantic color
  usage elsewhere.

## Edge Cases

- **Very long working directory**: The directory path in the info grid wraps
  within its grid column. Spectre's Grid handles this gracefully.

- **Messages with only tool blocks**: The preview shows `[tool: Shell]` or
  `[tool result]` instead of text content.

- **Session ID with special characters**: The ID is `Markup.Escape`d in all
  rendering contexts.

- **Non-interactive/piped terminal**: Renders normally. No prompts involved.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Info Grid | Section 3 | Session metadata display |
| Status Message | Section 1 | Error and usage messages |

