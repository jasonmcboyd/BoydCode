# Screen: /conversations list

## Overview

The conversations list screen opens an interactive list in a modeless Window
titled "Conversations", showing saved conversations sorted by last access time.
Users can navigate the list with keyboard arrows, show conversation details,
resume a conversation, rename, or delete -- all without leaving the window.

The current active session is indicated with a green name and a `*` suffix.
A search/filter field allows narrowing the list when many conversations exist.

**Screen IDs**: SESS-02, SESS-03

## Trigger

- User types `/conversations list` or `/conversations` (default subcommand)
  during an active session.
- Handled by `ConversationsSlashCommand.HandleListAsync()`.

## Route

Opens a modeless `Window` via the Interactive List pattern (component pattern
#28). The window floats over the conversation view. The agent continues working
in the background. The user dismisses with Esc.

## Layout (80 columns)

### With Conversations

```
+-- Conversations ------------------------------------------+
|                                                            |
|  [Type to filter...]                                       |
|                                                            |
|  Name                    Msgs  Tokens  Provider   Active   |
|  ▶ Auth work *             24    8.2k  Gemini     Feb 27   |
|    Fix the auth bug...     12    3.1k  Gemini     Feb 26   |
|    Refactoring             48   15.4k  Anthropic  Feb 24   |
|    Docker setup             3    0.9k  Gemini     Feb 25   |
|    Initial project plan    18    5.7k  OpenAi     Feb 20   |
|                                                            |
|  Enter: Show  Space: Resume  r: Rename  d: Delete         |
|  /: Filter  Esc: Close                                     |
|                                                            |
+------------------------------------------------------------+
```

The highlighted row (first row by default) uses `Theme.List.SelectedBackground`
(blue) with `Theme.List.SelectedText` (white). The `▶` arrow indicator marks
the focused row. In the mockup above, the first row is selected.

### With Conversations (no active session)

Same layout, but no row shows the `*` suffix or green styling.

### Empty State

```
+-- Conversations ------------------------------------------+
|                                                            |
|                                                            |
|        No conversations yet.                               |
|        Start chatting to create one.                       |
|                                                            |
|                                                            |
|  Esc: Close                                                |
|                                                            |
+------------------------------------------------------------+
```

When the list is empty, the search/filter field is hidden. The empty message
is centered and drawn with `Theme.Semantic.Muted` (dark gray). The Action Bar
shows only `Esc: Close` since no other actions apply.

### Anatomy

1. **Window** -- Modeless `Window` with `Theme.Modal.BorderScheme` (blue border),
   title "Conversations", rounded border style, centered at 80% width / 70%
   height.

2. **Search/Filter Field** (component pattern #30) -- `TextField` at top of
   window. Shows placeholder "Type to filter..." in muted style when inactive.
   Activated by pressing `/` while the list has focus. Filters conversations
   by name (case-insensitive substring match). Hidden when the list is empty.

3. **Column Header** -- Static `Label` showing column names. Drawn with
   `Theme.Semantic.Muted` (dark gray). Columns:
   - **Name** -- left-aligned, primary column
   - **Msgs** -- right-aligned, message count
   - **Tokens** -- right-aligned, formatted as `X.Xk` for thousands
   - **Provider** -- left-aligned, provider type name
   - **Active** -- left-aligned, `MMM dd` format in local time

4. **List View** -- `ListView` with one row per conversation. Scrollable when
   items exceed viewport height. The focused row uses
   `Theme.List.SelectedBackground` and `Theme.List.SelectedText`. The `▶`
   arrow indicator (`\u25b6`) marks the focused row in column 2.

5. **Row Content** --
   - **Name cell**: For the current session, drawn with `Theme.Semantic.Success`
     (green) and a ` *` suffix. For sessions with a name, shows the name. For
     unnamed sessions, shows the first user message preview (truncated to fit
     column width) with `...` suffix. If no user message exists, shows `--` in
     `Theme.Semantic.Muted`.
   - **Msgs cell**: Integer count, right-aligned.
   - **Tokens cell**: Token count formatted as `X.Xk` (e.g., `8.2k`), or the
     raw number if under 1000. Right-aligned.
   - **Provider cell**: Provider type name (`Gemini`, `Anthropic`, etc.), or
     `--` in muted style if not set.
   - **Active cell**: `MMM dd` format (e.g., `Feb 27`) using local time. If
     the conversation is from a prior year, shows `yyyy-MM-dd`.

6. **Action Bar** (component pattern #29) -- Positioned at `Y = Pos.AnchorEnd(2)`.
   Shows available keyboard shortcuts. Priority order (rightmost dropped first
   at narrow widths):
   1. `Esc: Close` (always shown)
   2. `Enter: Show` (always shown)
   3. `Space: Resume`
   4. `r: Rename`
   5. `d: Delete`
   6. `/: Filter`

## States

| State | Condition | Visual Difference |
|---|---|---|
| With conversations (has current) | Conversations exist and one matches active session | List with green-highlighted current session name + `*` suffix |
| With conversations (no current) | Conversations exist but none is active | List with all plain names |
| Empty | No saved sessions | Centered empty state message, no filter field, minimal action bar |
| Filtered (matches) | Filter text entered, matching items exist | Filtered list, action bar shows `Esc: Clear` before `Esc: Close` |
| Filtered (no matches) | Filter text entered, no matches | Centered "No matching conversations." in muted, action bar shows `Esc: Clear` |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

| Element | Token | Notes |
|---|---|---|
| Window border | `Theme.Modal.BorderScheme` | Blue border, rounded style |
| Selected row background | `Theme.List.SelectedBackground` | Accent blue |
| Selected row text | `Theme.List.SelectedText` | White on blue |
| Action bar text | `Theme.List.ActionBar` | Delegates to `Theme.Semantic.Muted` |
| Current session name | `Theme.Semantic.Success` | Green text + `*` suffix |
| Column headers | `Theme.Semantic.Muted` | Dark gray |
| Empty cells (`--`) | `Theme.Semantic.Muted` | Dark gray em-dash |
| Empty state message | `Theme.Semantic.Muted` | Dark gray centered text |
| Data cell text | `Theme.Semantic.Default` | White |
| Row indicator | `\u25b6` (arrow) | Marks focused row |

## Interactive Elements

### Keyboard

| Key | Action |
|---|---|
| Up / k | Move selection up |
| Down / j | Move selection down |
| Enter | Show conversation detail (opens detail modal) |
| Space | Resume selected conversation (switches active session, dismisses window) |
| r | Rename selected conversation (opens text prompt dialog) |
| d | Delete selected conversation (opens Delete Confirmation dialog, pattern #15) |
| / | Focus the search/filter field (pattern #30) |
| Esc | Close the window (or clear filter if filter is active) |

Single-letter hotkeys are handled in the window's `OnKeyDown` override and fire
only when the `ListView` has focus (not when a sub-dialog is open).

### Actions

- **Enter (Show)**: Opens a detail modal window showing the full conversation
  metadata (ID, name, project, message count, token usage, provider, timestamps,
  preview of recent messages). See `/conversations show` screen spec.

- **Space (Resume)**: Switches the active session to the selected conversation.
  Dismisses the list window. The conversation view updates to show the resumed
  session's history.

- **r (Rename)**: Opens a modal `Dialog` with a `TextField` for the new name.
  Pre-populated with the current name (if any). Enter confirms, Esc cancels.
  On confirm, the list row updates immediately.

- **d (Delete)**: Opens a Delete Confirmation dialog (pattern #15) showing the
  conversation name, message count, and last active date. "Cancel" is
  pre-focused. On confirm, the row is removed from the list. Cannot delete
  the current active session -- the `d` key is ignored or shows a brief
  warning.

## Behavior

- **Sorting**: Conversations are sorted by `LastAccessedAt` descending (most
  recent first).

- **Limit**: Maximum 50 conversations displayed. No pagination. Older
  conversations beyond the limit are silently omitted.

- **Name resolution**: When `session.Name` is set, the Name column shows the
  name. When `session.Name` is null, the Name column shows the first user
  message text (extracted by `GetFirstMessagePreview()`), truncated to fit
  the column width with `...` suffix. If no user message exists, shows `--`.

- **Current session detection**: Compares `_activeSession.Session?.Id` with
  each listed session's ID. The current session row uses green name text and
  a ` *` suffix.

- **Delete guard**: The current active session cannot be deleted from the list.
  Attempting to press `d` on the current session either ignores the keypress
  or shows a brief status message: "Cannot delete the active conversation."

- **Window type**: Modeless window. The agent continues processing in the
  background while the window is open.

- **Dismiss**: Esc key closes the window (or clears the filter first if
  active). The conversation view is revealed underneath.

## Edge Cases

- **Very long conversation names**: Truncated with `...` at the column width
  boundary. The full name is visible in the detail view (Enter).

- **Many conversations (> 50)**: Only the 50 most recently accessed are shown.
  Older conversations are silently omitted. No indication is given that
  conversations were truncated.

- **Many conversations (> viewport height)**: `ListView` scrolls natively.
  The search/filter field helps narrow the list.

- **Narrow terminal (< 60 columns)**: Columns are dropped right-to-left to
  fit: Active is dropped first, then Provider, then Tokens, then Msgs.
  Name is always shown. Action bar drops less-important hints per the
  responsive behavior in pattern #29.

- **Delete current session**: The `d` key is ignored or shows a brief warning.
  The user must start a new conversation first.

- **Resume current session**: Pressing Space on the already-active session
  dismisses the window with no other effect (already active).

- **Non-interactive/piped terminal**: Falls back to column-aligned plain text
  output to stdout. No window, no interactivity. Colors are omitted. Format:

  ```
  Name                    Msgs  Tokens  Provider   Active
  Auth work *               24    8.2k  Gemini     Feb 27
  Fix the auth bug...       12    3.1k  Gemini     Feb 26
  Refactoring               48   15.4k  Anthropic  Feb 24
  ```

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Interactive List | #28 | ListView with keyboard navigation |
| Action Bar | #29 | Shortcut hints at bottom of window |
| Search/Filter Field | #30 | Real-time filtering by name |
| Modal Overlay (List variant) | #11 | Modeless window over conversation |
| Delete Confirmation | #15 | Confirm before deleting a conversation |
| Empty State | #21 | "No conversations yet." message |
