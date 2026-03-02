# Screen: /help

## Overview

The help screen displays a reference panel of all available slash commands
and their subcommands. It is built dynamically from the `ISlashCommandRegistry`,
so it always reflects the currently registered commands. Content is assembled
via `StringBuilder` with `PadRight(24)` alignment for the command column and
rendered inside a `ShowModal` Terminal.Gui `Window` overlay.

The window contains a `TextField` at the top for real-time filtering and a
read-only `TextView` below it for scrollable command content. Each keystroke
in the filter field narrows the visible commands using case-insensitive
substring matching on both command names and descriptions.

**Screen ID**: HELP-01

## Trigger

- User types `/help` during an active session.
- Handled by `HelpSlashCommand.TryHandleAsync()`.

## Layout (80 columns) -- Unfiltered

```
+-- Help ------------------------------------------------------------------+
|                                                                           |
|  [Type to filter...                                                    ]  |
|                                                                           |
|  /quit                   Exit the session (also: /exit)                   |
|  /project                Manage projects                                  |
|    create [name]           Create a new project                           |
|    list                    List all projects                              |
|    show [name]             Show project details                           |
|    edit [name]             Edit project settings                          |
|    delete [name]           Delete a project                               |
|  /provider               Manage LLM providers                             |
|    list                    List all providers and their status             |
|    setup [name]            Configure a provider (API key, model)           |
|    show                    Show active provider details                   |
|    remove [name]           Remove a provider configuration                |
|  /jea                    Manage JEA profiles                              |
|    list                    List all JEA profiles                          |
|    show [name]             Show profile details                           |
|    create [name]           Create a new profile                           |
|    edit [name]             Edit an existing profile                       |
|    delete [name]           Delete a profile                               |
|    effective               Show effective config for current session       |
|    assign [name]           Assign a profile to current project            |
|    unassign [name]         Remove a profile from current project          |
|  /context                View and manage conversation context             |
|    show                    Show detailed context breakdown with chart     |
|    summarize [topic]       Summarize conversation using LLM               |
|    prune                   Prune older topics to free context space        |
|    refresh                 Refresh session context (project, dirs, engine) |
|  /conversations          Manage conversations and sessions                |
|    list                    List recent conversations                      |
|    show [id]               Show conversation details                      |
|    rename [id] [name]      Rename a conversation                          |
|    delete [id]             Delete a saved conversation                    |
|    clear                   Clear conversation history                     |
|  /expand                 Show full output from the last tool execution    |
|  /agent                  Manage agent definitions                         |
|    list                    List available agents                          |
|    show <name>             Show agent details                             |
|  /help                   Show available commands                          |
|                                                                           |
|  Esc to dismiss                                                       35% |
|                                                                           |
+---------------------------------------------------------------------------+
```

## Layout (80 columns) -- Filtered (user typed "con")

```
+-- Help ------------------------------------------------------------------+
|                                                                           |
|  [con                                                                  ]  |
|                                                                           |
|  /context                View and manage conversation context             |
|    show                    Show detailed context breakdown with chart     |
|    summarize [topic]       Summarize conversation using LLM               |
|    prune                   Prune older topics to free context space        |
|    refresh                 Refresh session context (project, dirs, engine) |
|  /conversations          Manage conversations and sessions                |
|    list                    List recent conversations                      |
|    show [id]               Show conversation details                      |
|    rename [id] [name]      Rename a conversation                          |
|    delete [id]             Delete a saved conversation                    |
|    clear                   Clear conversation history                     |
|                                                                           |
|                                                                           |
|                                                                           |
|  Esc: clear filter  Esc Esc: dismiss                                      |
|                                                                           |
+---------------------------------------------------------------------------+
```

When the filter field has text, matching is performed against top-level
command names (e.g., `/context`) and their descriptions. If a top-level
command matches, all of its subcommands are shown. If a subcommand name or
description matches, its parent command and all sibling subcommands are shown
(the entire command group is kept together for context).

## Layout (80 columns) -- No Matches

```
+-- Help ------------------------------------------------------------------+
|                                                                           |
|  [xyzzy                                                                ]  |
|                                                                           |
|                                                                           |
|                                                                           |
|                 No matching commands.                                      |
|                                                                           |
|                                                                           |
|                                                                           |
|  Esc: clear filter  Esc Esc: dismiss                                      |
|                                                                           |
+---------------------------------------------------------------------------+
```

When the filter produces no matches, a centered message "No matching commands."
is displayed in `Theme.Semantic.Muted` (dark gray). The dismiss hint updates
to show the two-press Esc behavior.

### Anatomy

1. **Modal window** -- `ShowModal("Help", ...)` opens a Terminal.Gui
   `Window` overlay with the title "Help" in the window header.
2. **Filter field** -- A `TextField` positioned at `X = 1, Y = 1` inside the
   window, `Width = Dim.Fill(1)`, `Height = 1`. When empty and unfocused, it
   displays placeholder text "Type to filter..." in
   `Theme.Semantic.Muted` (dark gray, italic). The filter field receives
   focus by default when the window opens, so the user can start typing
   immediately.
3. **Content view** -- A read-only `TextView` positioned below the filter
   field (`Y = Pos.Bottom(filterField) + 1`), filling the remaining window
   space. Contains the command listing built via `StringBuilder` with
   `PadRight(24)` alignment.
4. **Horizontal padding** -- Both the filter field and content view are
   offset by 1 character from each side of the window border (`X = 1`,
   `Width = Dim.Fill(1)`), giving 2 characters of total horizontal padding.
5. **Built-in /quit** -- `/quit` is hardcoded as the first entry with the
   description "Exit the session (also: /exit)". `/exit` does not appear as
   a separate row.
6. **Registered commands** -- Iterated from `ISlashCommandRegistry
   .GetAllDescriptors()`. Each descriptor renders as a top-level row
   (prefix + description), followed by indented subcommand rows.
7. **Subcommand indentation** -- Subcommand rows use 2-space indent for the
   command name and an additional 2-space indent for the description.
8. **/help rendered last** -- When iterating the registry, `/help` is skipped.
   After all other registered commands, `/help` is appended manually as the
   final entry. This ensures it always appears at the bottom regardless of
   registration order.
9. **Dismiss hint** -- The bottom of the content shows "Esc to dismiss" when
   the filter is empty, or "Esc: clear filter  Esc Esc: dismiss" when the
   filter has text. This hint is visible when scrolled to the end.
10. **Scroll position indicator** -- When the command list exceeds the
    viewport, a percentage indicator (e.g., `35%`) appears in the
    bottom-right corner of the content view (see pattern #33).

## States

| State | Condition | Visual Difference |
|---|---|---|
| Unfiltered | Filter field empty | Full list of all registered commands |
| Filtered (matches) | Filter has text, matches found | Only matching command groups visible |
| Filtered (no matches) | Filter has text, no matches | "No matching commands." centered in muted text |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Modal.BorderScheme` (blue border on the modal
window), `Theme.Layout.CommandPad` (24-character command column width),
`Theme.Semantic.Muted` (filter placeholder text and "No matching commands."
empty state), `Theme.Input.Text` (filter field text color).

No color attributes are applied inside the command listing content itself.
Alignment is achieved with `PadRight(Theme.Layout.CommandPad)` string padding.

## Interactive Elements

### Filter Field

The filter field provides real-time search across all command names and
descriptions. See Search/Filter Field (pattern #30) for the general pattern.

**Matching rules:**
- Case-insensitive substring match
- Matched against both the command/subcommand name and its description
- When a top-level command matches, all its subcommands are included
- When any subcommand matches, the entire parent command group is included
  (parent + all siblings) to preserve navigational context
- `/quit` is included in filtering (matches "quit", "exit", "session", etc.)
- `/help` is included in filtering (matches "help", "commands", etc.)

### Keyboard

| Key | Context | Action |
|---|---|---|
| Any printable | Filter focused | Add character, filter list in real-time |
| Backspace | Filter focused | Remove character, update filter |
| Esc | Filter has text | Clear filter text, restore full list |
| Esc | Filter empty | Dismiss the help window |
| Down | Filter focused | Move focus to content view |
| Up/Down | Content focused | Scroll content |
| Tab | Any | Toggle focus between filter and content |

## Behavior

- **Dynamic content**: The full command list is built on every invocation
  from the current registry state. If commands are registered or unregistered
  at runtime (not currently possible), the help window reflects the change.

- **Real-time filtering**: Each keystroke in the filter field rebuilds the
  visible content. The content view is updated synchronously on each
  `TextChanged` event. When the filter is cleared (manually or via Esc),
  the full command list is restored.

- **Command ordering**: `/quit` always appears first (hardcoded). Registered
  commands appear in the order they were registered, which is determined by
  the DI registration order in `Program.cs`. `/help` always appears last
  (appended manually after registry iteration). This ordering is preserved
  in filtered results.

- **Rendering**: The modal opens centered on screen, sized to fit the
  content, with a blue rounded border (see Modal Overlay pattern, Section
  11). The window is modeless -- the agent continues working in the
  background.

- **Filter persistence**: The filter text is not preserved between
  invocations. Each `/help` invocation opens with an empty filter showing
  the full command list.

## Edge Cases

- **Narrow terminal (< 60 columns)**: The `PadRight(24)` padding on the
  command column may cause lines to wrap. The visual result can be hard to
  read at very narrow widths, but content remains correct. The filter field
  adapts to the available width via `Dim.Fill(1)`.

- **Many commands**: The content view is scrollable, so content longer than
  the visible area can be scrolled. With the current command set (~9
  top-level commands, ~30 subcommands), the unfiltered list fits comfortably
  on most terminals. A scroll position indicator (pattern #33) appears when
  content exceeds the viewport.

- **Filter matches only subcommands**: If "delete" is typed, command groups
  containing a "delete" subcommand are shown in full (parent command + all
  subcommands), not just the matching subcommand rows. This preserves
  context.

- **Non-interactive/piped terminal**: Not applicable. Modals are only
  shown during an active Terminal.Gui session.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Modal Overlay | #11 | Terminal.Gui Window overlay for the help content |
| Search/Filter Field | #30 | Real-time filtering of command list |
| Scroll Position Indicator | #33 | Percentage indicator when content exceeds viewport |

Note: The help window intentionally deviates from the `SimpleTable` pattern
used elsewhere. The `ShowModal` approach with a filter field + plain-text
content provides a visually distinct reference panel and avoids the
column-header overhead of a table widget. Unlike the Interactive List
(pattern #28), the help window does not support row-level actions -- it is
purely a read-only filtered reference.
