# Screen: /conversations show

## Overview

Displays detailed metadata about a specific saved conversation in a modeless
Detail Modal window (component pattern #11, Variant C). Content includes an
info grid with key properties and a preview of recent messages with
role-colored labels. When the session carries a name, an additional Name row
appears in the info grid. A resumption hint is shown at the bottom.

All content is drawn using Terminal.Gui native drawing (`SetAttribute`,
`Move`, `AddStr`) with structured key-value layout.

**Screen IDs**: SESS-04, SESS-05, SESS-06

## Trigger

- User types `/conversations show <id>` during an active session.
- Handled by `ConversationsSlashCommand.HandleShowAsync()`.

## Layout (80 columns)

### Detail View (with name)

```
+-- Conversation: abc12345 ---------------------------------+
|                                                            |
|  Session    abc12345                                       |
|  Name       Auth implementation work                       |
|  Created    2026-02-27 14:30    Last used  2026-02-27      |
|  Project    my-project          Messages   24              |
|  Directory  C:\Users\jason\source\repos\my-project         |
|                                                            |
|  -- Recent messages ---                                    |
|  user: Implement the new execution engine for Docker       |
|  assistant: I'll help you implement the Docker container   |
|  user: Great, now let's add the volume mount builder       |
|  assistant: I'll create the VolumeMountBuilder class...    |
|  user: Can you also add tests for the mount builder?       |
|                                                            |
|  ... 19 more message(s)                                    |
|                                                            |
|  Resume with: boydcode --resume abc12345                   |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

### Detail View (no name)

```
+-- Conversation: abc12345 ---------------------------------+
|                                                            |
|  Session    abc12345                                       |
|  Created    2026-02-27 14:30    Last used  2026-02-27      |
|  Project    my-project          Messages   24              |
|  Directory  C:\Users\jason\source\repos\my-project         |
|                                                            |
|  -- Recent messages ---                                    |
|  ...                                                       |
+------------------------------------------------------------+
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

1. **Window**: Modeless `Window` via `ShowDetailModal`, titled
   `"Conversation: {id}"`, blue border (`Theme.Modal.BorderScheme`).

2. **Info grid** -- Native drawing using the Info Grid pattern (pattern #9):
   - Row 1: `Session` / ID (single value row).
   - Row 1a (conditional): `Name` / `session.Name` -- only present when
     `session.Name is not null`.
   - Row 2: `Created` / datetime, `Last used` / datetime (paired row).
   - Row 3: `Project` / name or "(none)", `Messages` / count (paired row).
   - Row 4: `Directory` / working directory path (single value row).

3. **Section divider** -- "Recent messages" using Section Divider pattern
   (pattern #8) in `Theme.Semantic.Muted`.

4. **Message preview** -- First 5 messages, each on one line at 2-char
   indent. Role label is color-coded: `Theme.Semantic.Accent` (blue) for
   "user", `Theme.Semantic.Success` (green) for "assistant",
   `Theme.Semantic.Muted` (dim) for "system". Text is truncated to fit the
   window width.

5. **Overflow indicator** -- If more than 5 messages exist: 2-char indent,
   `Theme.Semantic.Muted` (dim): `... N more message(s)`.

6. **Resume hint** -- 2-char indent, `Theme.Semantic.Muted` (dim):
   `Resume with:` followed by `boydcode --resume {id}` in
   `Theme.Semantic.Default`.

7. **Dismiss hint** -- "Esc to dismiss" in `Theme.Semantic.Muted`.

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

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

- `Theme.Modal.BorderScheme` -- blue border on the modeless window
- `Theme.Semantic.Muted` -- dim info grid labels ("Session", "Name",
  "Created", "Last used", "Project", "Messages", "Directory"), section
  divider rule, "Recent messages" title, overflow count, resume hint label,
  "Esc to dismiss", system role label
- `Theme.Semantic.Info` -- cyan info grid values (session ID, name, dates,
  project name, message count, directory path)
- `Theme.Semantic.Accent` -- blue "user" role label
- `Theme.Semantic.Success` -- green "assistant" role label
- `Theme.Semantic.Error` -- red "Error:" prefix
- `Theme.Semantic.Warning` -- yellow "Usage:" prefix
- `Theme.Semantic.Default` -- white message text, resume command text

## Interactive Elements

None. This is a read-only detail view.

## Behavior

- **Session loading**: The session is loaded from `ISessionRepository.LoadAsync()`.
  If the repository returns null, the not-found error is displayed.

- **Window construction**: `ShowDetailModal` opens a Terminal.Gui modeless
  `Window` with a blue border. The window title is `"Conversation: {id}"`.

- **Native drawing**: The window's inner `View` overrides
  `OnDrawingContent` to draw the structured layout using `SetAttribute`,
  `Move`, `AddStr`.

- **Name row**: The Name row is conditionally drawn in the info grid only when
  `session.Name is not null`. Sessions without a name omit the row entirely
  rather than showing a placeholder.

- **Date formatting**: Both `CreatedAt` and `LastAccessedAt` are converted
  to local time and formatted as `yyyy-MM-dd HH:mm` using
  `CultureInfo.InvariantCulture`.

- **Project display**: Shows `session.ProjectName` or `"(none)"` in
  `Theme.Semantic.Muted` if null.

- **Message text extraction**: `GetMessageText()` extracts text from the
  first `TextBlock` in a message's content. If no text block exists, it
  falls back to `[tool: {name}]` for tool use blocks, `[tool result]` for
  tool result blocks, or `--` for empty messages. Text has line endings
  replaced with spaces and is truncated to fit the window width with `...`.

- **Role coloring**: Message roles use `Theme.Semantic.Accent` (blue) for
  "user" and `Theme.Semantic.Success` (green) for "assistant".

- **Dismiss**: Esc key closes the window. The `ActivityBarView` transitions
  to `ActivityState.Modal` while the window is open.

## Edge Cases

- **Very long working directory**: The directory path in the info grid wraps
  within the window's available width. Native drawing handles this with
  word wrapping.

- **Messages with only tool blocks**: The preview shows `[tool: Shell]` or
  `[tool result]` instead of text content.

- **Session ID with special characters**: The ID is rendered as plain text
  via `AddStr` -- no markup interpretation occurs.

- **Non-interactive/piped terminal**: Renders normally as plain text without
  opening a window. No prompts involved.

## Non-TUI Fallback

When running in non-interactive/piped mode (no Terminal.Gui), the
conversation detail is rendered as plain text to stdout. Info grid uses
string-padded columns. Role labels are rendered without color.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Modal Overlay (Detail Modal) | #11, Variant C | Modeless window with native drawing layout |
| Info Grid | #9 | Session metadata key-value display |
| Section Divider | #8 | "Recent messages" heading |
| Status Message | #7 | Error and usage messages |
