# Screen: /help

## Overview

The help screen displays a reference panel of all available slash commands
and their subcommands. It is built dynamically from the `ISlashCommandRegistry`,
so it always reflects the currently registered commands. Content is assembled
via `StringBuilder` with `PadRight(24)` alignment for the command column and
rendered inside a `ShowModal` Terminal.Gui `Window` overlay with a `TextView`
for scrollable content.

**Screen ID**: HELP-01

## Trigger

- User types `/help` during an active session.
- Handled by `HelpSlashCommand.TryHandleAsync()`.

## Layout (80 columns)

```
+-- Help ------------------------------------------------------------------+
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
|  Esc to dismiss                                                           |
|                                                                           |
+---------------------------------------------------------------------------+
```

### Anatomy

1. **Modal window** -- `ShowModal("Help", content)` opens a Terminal.Gui
   `Window` overlay with the title "Help" in the window header. The content
   is displayed in a read-only `TextView` inside the window.
2. **Content string** -- Built via `StringBuilder`. Each row is formatted with
   `PadRight(24)` for the command column, followed by the description. No
   markup is used inside the content string; alignment is achieved with
   fixed-width padding only.
3. **Horizontal padding** -- The `TextView` is offset by 1 character from each
   side of the window border (`X = 1`, `Width = Dim.Fill(1)`), giving 2
   characters of total horizontal padding (1 from the window border + 1 from
   the offset).
4. **Built-in /quit** -- `/quit` is hardcoded as the first entry with the
   description "Exit the session (also: /exit)". `/exit` does not appear as
   a separate row.
5. **Registered commands** -- Iterated from `ISlashCommandRegistry
   .GetAllDescriptors()`. Each descriptor renders as a top-level row
   (prefix + description), followed by indented subcommand rows.
6. **Subcommand indentation** -- Subcommand rows use 2-space indent for the
   command name and an additional 2-space indent for the description.
7. **/help rendered last** -- When iterating the registry, `/help` is skipped.
   After all other registered commands, `/help` is appended manually as the
   final entry. This ensures it always appears at the bottom regardless of
   registration order.
8. **Dismiss hint** -- `ShowModal` appends "Esc to dismiss" at the bottom of
   every modal's content. This hint is visible when scrolled to the end.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Normal | Always | Full window with all registered commands |

There is only one state. The help window always renders and always includes
all registered commands.

## Style Tokens

Uses the Modal Overlay pattern (07-component-patterns.md, Section 11) which
references 06-style-tokens.md for border style (rounded), border color
(accent/blue), and padding (2 horizontal, 1 vertical).

No markup tokens are used inside the content string itself. Alignment
is achieved with `PadRight(24)` string padding.

## Interactive Elements

None. This is a read-only reference window. Esc dismisses the modal.

## Behavior

- **Dynamic content**: The window is rebuilt on every invocation from the
  current registry state. If commands are registered or unregistered at
  runtime (not currently possible), the help window reflects the change.

- **Command ordering**: `/quit` always appears first (hardcoded). Registered
  commands appear in the order they were registered, which is determined by
  the DI registration order in `Program.cs`. `/help` always appears last
  (appended manually after registry iteration).

- **Rendering**: `ShowModal("Help", content)` is called with the assembled
  string. The modal opens centered on screen, sized to fit the content, with
  a blue rounded border (see Modal Overlay pattern, Section 11). The window
  is modeless -- the agent continues working in the background. Esc
  dismisses the window.

## Edge Cases

- **Narrow terminal (< 60 columns)**: The `PadRight(24)` padding on the
  command column may cause lines to wrap. The visual result can be hard to
  read at very narrow widths, but content remains correct.

- **Many commands**: The window uses a scrollable `TextView`, so content
  longer than the visible area can be scrolled. With the current command
  set (~9 top-level commands, ~30 subcommands), this fits comfortably on
  most terminals.

- **Non-interactive/piped terminal**: Not applicable. Modals are only
  shown during an active Terminal.Gui session.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Modal Overlay | Section 11 | Terminal.Gui Window overlay for the full help content |

Note: The help window intentionally deviates from the `SimpleTable` pattern
used elsewhere. The `ShowModal` approach with a plain-text content string
provides a visually distinct reference panel and avoids the column-header
overhead of a table widget.
