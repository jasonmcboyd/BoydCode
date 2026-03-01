# Screen: /help

## Overview

The help screen displays a reference panel of all available slash commands
and their subcommands. It is built dynamically from the `ISlashCommandRegistry`,
so it always reflects the currently registered commands. Content is assembled
via `StringBuilder` with `PadRight(24)` alignment for the command column and
rendered inside a `ShowModal` panel with a rounded blue border.

**Screen ID**: HELP-01

## Trigger

- User types `/help` during an active session.
- Handled by `HelpSlashCommand.TryHandleAsync()`.

## Layout (80 columns)

```
╭── Help ──────────────────────────────────────────────────────────────────────╮
│ /quit                   Exit the session (also: /exit)                       │
│ /project                Manage named projects                                │
│   create <name>           Create a new project                               │
│   list                    List all projects                                   │
│   show [name]             Show project details                               │
│   edit [name]             Edit project settings                              │
│   delete [name]           Delete a project                                   │
│ /provider               Manage LLM providers                                 │
│   list                    List configured providers                          │
│   setup [name]            Configure a provider                               │
│   show                    Show active provider details                       │
│   remove [name]           Remove a provider                                  │
│ /jea                    Manage JEA profiles                                  │
│   ...                                                                        │
│ /context                View and manage conversation context                 │
│   show                    Show detailed context breakdown with chart          │
│   summarize [topic]       Summarize conversation using LLM                   │
│   refresh                 Refresh session context (project, dirs, engine)     │
│ /conversations          Manage conversations and sessions                    │
│   list                    List recent conversations                          │
│   show [id]               Show conversation details                          │
│   rename [id] [name]      Rename a conversation                              │
│   delete [id]             Delete a saved conversation                        │
│   clear                   Clear conversation history                         │
│ /expand                 Show full output from the last tool execution        │
│ /help                   Show available commands                               │
╰──────────────────────────────────────────────────────────────────────────────╯
```

### Anatomy

1. **Modal panel** -- `ShowModal("Help", content)` wraps a plain-text string
   in a `Panel` with `BoxBorder.Rounded` and `Color.Blue` border. The "Help"
   title is rendered bold in the panel header. This is NOT a Spectre `Table`
   widget.
2. **Content string** -- Built via `StringBuilder`. Each row is formatted with
   `PadRight(24)` for the command column, followed by the description. No
   Spectre markup is used inside the content string; alignment is achieved
   with fixed-width padding only.
3. **Built-in /quit** -- `/quit` is hardcoded as the first entry with the
   description "Exit the session (also: /exit)". `/exit` does not appear as
   a separate row.
4. **Registered commands** -- Iterated from `ISlashCommandRegistry
   .GetAllDescriptors()`. Each descriptor renders as a top-level row
   (prefix + description), followed by indented subcommand rows.
5. **Subcommand indentation** -- Subcommand rows use 2-space indent for the
   command name and an additional 2-space indent for the description.
6. **/help rendered last** -- When iterating the registry, `/help` is skipped.
   After all other registered commands, `/help` is appended manually as the
   final entry. This ensures it always appears at the bottom regardless of
   registration order.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Normal | Always | Full panel with all registered commands |

There is only one state. The help panel always renders and always includes
all registered commands.

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `Color.Blue` | Color.Blue (1.5) | Panel border color |
| `BoxBorder.Rounded` | (border style) | Panel border shape |
| `[bold]` | bold (2.2) | Panel header title "Help" (applied by ShowModal) |

No Spectre markup tokens are used inside the content string itself. Alignment
is achieved with `PadRight(24)` string padding.

## Interactive Elements

None. This is a read-only reference panel.

## Behavior

- **Dynamic content**: The panel is rebuilt on every invocation from the
  current registry state. If commands are registered or unregistered at
  runtime (not currently possible), the help panel reflects the change.

- **Command ordering**: `/quit` always appears first (hardcoded). Registered
  commands appear in the order they were registered, which is determined by
  the DI registration order in `Program.cs`. `/help` always appears last
  (appended manually after registry iteration).

- **Rendering**: `ShowModal("Help", content)` is called with the assembled
  string. `ShowModal` wraps content in a `Panel` with `BoxBorder.Rounded`
  and `Color.Blue`, then routes output through the layout-aware
  `SpectreHelpers.OutputRenderable`.

## Edge Cases

- **Narrow terminal (< 60 columns)**: The `PadRight(24)` padding on the
  command column may cause lines to wrap. The visual result can be hard to
  read at very narrow widths, but content remains correct.

- **Many commands**: The panel grows vertically with no pagination. All
  commands are shown in a single panel. With the current command set
  (~8 top-level commands, ~25 subcommands), this fits comfortably on
  most terminals.

- **Non-interactive/piped terminal**: Renders normally. Spectre converts
  the panel borders to ASCII when the terminal does not support Unicode.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| ShowModal | Section (Modal) | Rounded blue-bordered panel for the full help content |

Note: The help panel intentionally deviates from the `SimpleTable` pattern
used elsewhere. The `ShowModal` approach with a plain-text content string
provides a visually distinct reference panel and avoids the column-header
overhead of a `Table` widget.

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| TryHandleAsync | `Commands/HelpSlashCommand.cs` | `TryHandleAsync` | 23-57 |
| StringBuilder construction | `Commands/HelpSlashCommand.cs` | `TryHandleAsync` | 31 |
| Built-in /quit | `Commands/HelpSlashCommand.cs` | `TryHandleAsync` | 34 |
| Registry iteration (skip /help) | `Commands/HelpSlashCommand.cs` | `TryHandleAsync` | 37-50 |
| /help rendered last | `Commands/HelpSlashCommand.cs` | `TryHandleAsync` | 53 |
| ShowModal call | `Commands/HelpSlashCommand.cs` | `TryHandleAsync` | 55 |
| AppendCommand helper | `Commands/HelpSlashCommand.cs` | `AppendCommand` | 59-63 |
| AppendSubcommand helper | `Commands/HelpSlashCommand.cs` | `AppendSubcommand` | 65-70 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
