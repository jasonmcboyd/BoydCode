# Screen: /help

## Overview

The help screen displays a reference table of all available slash commands
and their subcommands. It uses a visually distinct rounded blue-bordered
table to differentiate it from data tables elsewhere in the app. The table
is built dynamically from the `ISlashCommandRegistry`, so it always reflects
the currently registered commands.

**Screen ID**: HELP-01

## Trigger

- User types `/help` during an active session.
- Handled by `HelpSlashCommand.TryHandleAsync()`.

## Layout (80 columns)

```
╭────────────────────────────────────────────────────────────────────────────╮
│ Command               Description                                         │
├────────────────────────────────────────────────────────────────────────────┤
│ /quit                 Exit the session                                     │
│ /exit                 Exit the session                                     │
│ /project              Manage named projects                                │
│   create <name>         Create a new project                               │
│   list                  List all projects                                   │
│   show [name]           Show project details                               │
│   edit [name]           Edit project settings                              │
│   delete [name]         Delete a project                                   │
│ /help                 Show available commands                               │
│ /provider             Manage LLM providers                                 │
│   list                  List configured providers                          │
│   setup [name]          Configure a provider                               │
│   show                  Show active provider details                       │
│   remove [name]         Remove a provider                                  │
│ /jea                  Manage JEA profiles                                  │
│   list                  List JEA profiles                                  │
│   show [name]           Show profile details                               │
│   create [name]         Create a new profile                               │
│   edit [name]           Edit a profile                                     │
│   delete [name]         Delete a profile                                   │
│   effective             Show effective profile for current session          │
│   assign [name]         Assign profile to current project                  │
│   unassign [name]       Unassign profile from current project              │
│ /context              View and manage conversation context                 │
│   show                  Show detailed context breakdown with chart          │
│   compact               Manually trigger context compaction                │
│   summarize [topic]     Summarize conversation using LLM                   │
│ /refresh              Refresh session context (project, directories, ...)  │
│ /sessions             Manage saved sessions                                │
│   list                  List recent sessions                               │
│   show [id]             Show session details                               │
│   delete [id]           Delete a saved session                             │
│ /clear                Clear conversation history                           │
│ /expand               Show full output from the last tool execution        │
╰────────────────────────────────────────────────────────────────────────────╯
```

### Anatomy

1. **Table** -- Spectre.Console `Table` with `TableBorder.Rounded` and
   `Color.Blue` border color. Two columns: Command, Description.
2. **Built-in commands** -- `/quit` and `/exit` are hardcoded first (not in
   the registry because they are handled directly by the session loop).
3. **Registered commands** -- Iterated from `ISlashCommandRegistry
   .GetAllDescriptors()`. Each descriptor renders as a top-level row
   (prefix + description), followed by subcommand rows.
4. **Subcommand indentation** -- Subcommand rows have 2-space indent and
   dim text in both columns: `  [dim]{usage}[/]` and `  [dim]{description}[/]`.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Normal | Always | Full table with all registered commands |

There is only one state. The help table always renders and always includes
all registered commands.

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `Color.Blue` | Color.Blue (1.5) | Table border color |
| `[bold]` | bold (2.2) | Column headers: "Command", "Description" |
| `[dim]` | dim (2.2) | Subcommand rows (both usage and description) |

## Interactive Elements

None. This is a read-only reference table.

## Behavior

- **Dynamic content**: The table is rebuilt on every invocation from the
  current registry state. If commands are registered or unregistered at
  runtime (not currently possible), the help table reflects the change.

- **Command ordering**: Built-in commands (`/quit`, `/exit`) always appear
  first. Registered commands appear in the order they were registered,
  which is determined by the DI registration order in `Program.cs`.

- **Subcommand escaping**: Both the `Usage` and `Description` fields of
  each subcommand descriptor are `Markup.Escape`d to prevent markup
  injection from command descriptions.

- **Rendering**: The complete table is written with a single
  `AnsiConsole.Write(table)` call.

## Edge Cases

- **Narrow terminal (< 60 columns)**: The rounded table border adds overhead.
  Command names and descriptions may wrap within their cells. Spectre's table
  renderer handles this by wrapping text within columns, but the visual result
  can be hard to read at very narrow widths.

- **Many commands**: The table grows vertically with no pagination. All
  commands are shown in a single table. With the current command set
  (~10 top-level commands, ~25 subcommands), this is approximately 35 rows,
  which fits comfortably on most terminals.

- **Non-interactive/piped terminal**: Renders normally. Spectre converts
  the table borders to ASCII when the terminal does not support Unicode.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| (Custom table) | Not a SimpleTable -- uses Rounded border + Blue color | Visually distinct reference table |

Note: The help table intentionally deviates from the `SimpleTable` pattern
used elsewhere. The rounded blue border visually distinguishes it as a
reference/documentation table rather than a data table.

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| TryHandleAsync | `Commands/HelpSlashCommand.cs` | `TryHandleAsync` | 21-56 |
| Table construction | `Commands/HelpSlashCommand.cs` | `TryHandleAsync` | 29-33 |
| Built-in commands | `Commands/HelpSlashCommand.cs` | `TryHandleAsync` | 36-37 |
| Registry iteration | `Commands/HelpSlashCommand.cs` | `TryHandleAsync` | 40-52 |
| Subcommand rendering | `Commands/HelpSlashCommand.cs` | `TryHandleAsync` | 46-51 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
