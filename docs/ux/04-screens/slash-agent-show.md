# Screen: /agent show

## Overview

Displays a detailed view of a single agent definition in a bordered panel,
showing name, description, scope, model override, max turns, source file path,
and the full system prompt/instructions. The instructions are the body of the
agent's markdown file and may be lengthy.

This screen opens as a modeless window floating over the conversation view.

**Screen IDs**: AGENT-03, AGENT-04, AGENT-05

## Trigger

`/agent show <name>`

- The `name` argument is required.
- If `name` is omitted, a usage hint is shown.

## Layout (80 columns)

### Full Agent Detail

```
╭── Agent: senior-developer ──────────────────────────────────────────────────╮
│                                                                              │
│  Name           senior-developer                                             │
│  Description    Implement features and fixes from design specs               │
│  Scope          Project                                                      │
│  Model          default                                                      │
│  Max Turns      25                                                           │
│  Source         C:\Users\jason\source\repos\MyProject\.boydcode\agents\      │
│                 senior-developer.md                                           │
│  Instructions   You are an expert .NET developer. You implement features     │
│                 from design specs produced by the software architect.         │
│                 Follow Clean Architecture patterns. Use sealed classes...     │
│                                                                              │
╰──────────────────────────────────────────────────────────────────────────────╯
```

### Agent with Model Override

```
╭── Agent: code-reviewer ─────────────────────────────────────────────────────╮
│                                                                              │
│  Name           code-reviewer                                                │
│  Description    Review code for quality and correctness                      │
│  Scope          User                                                         │
│  Model          claude-sonnet-4                                              │
│  Max Turns      10                                                           │
│  Source         C:\Users\jason\.boydcode\agents\code-reviewer.md             │
│  Instructions   You are a senior code reviewer. Focus on correctness,        │
│                 readability, and maintainability. Flag potential bugs...      │
│                                                                              │
╰──────────────────────────────────────────────────────────────────────────────╯
```

### Not Found

```
Error: Agent 'nonexistent' not found.
```

### No Name Provided

```
Usage: /agent show <name>
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Full detail | Agent exists | Panel with all fields including full instructions |
| Not found | Name does not match any registered agent | Red error with escaped agent name |
| No name | Name argument omitted | Red usage hint |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | Panel header "Agent: {name}"; row labels (Name, Description, Scope, Model, Max Turns, Source, Instructions) |
| `[red]` | error-red (1.1) | Error prefix for "not found" and usage messages |
| `BoxBorder.Rounded` | (border style) | Panel border shape |

## Interactive Elements

None. This is a read-only detail view.

## Behavior

- **Name resolution**: The name is taken from the trailing arguments after
  `/agent show`. If empty, a red usage message is rendered and the command
  returns.

- **Agent lookup**: `_agentRegistry.GetByName(name)` performs a
  case-insensitive lookup. Returns null if the agent is not registered.

- **Panel construction**: A `Grid` with two columns (label, value) is
  constructed with one row per field. The grid is wrapped in a `Panel` with
  `BoxBorder.Rounded` and the header `Agent: {name}` in bold.

- **Instructions display**: The full `Instructions` text from the agent
  definition is shown. In the current implementation, instructions longer
  than 500 characters are truncated with "..." appended. The spec defines
  showing the full instructions in a scrollable window; the truncation is an
  implementation limitation to be addressed when migrating to Terminal.Gui
  windows.

- **Model display**: Shows `ModelOverride` if set. If null, shows "default"
  (indicating the agent uses the session's active model).

- **Max Turns display**: Shows `MaxTurns` if set. If null, shows
  "default (25)" indicating the `AgentDefaults.DefaultMaxTurns` value.

- **Source path**: Shows `SourcePath` -- the full filesystem path to the
  agent's markdown file.

- **Escape handling**: Agent name, description, instructions, and source
  path are all escaped via `Markup.Escape()` before rendering.

- **Window type**: Modeless window. The agent continues processing in the
  background while the window is open.

- **Dismiss**: Esc key closes the window.

## Edge Cases

- **Very long instructions**: The instructions field may contain hundreds or
  thousands of characters. In the current Spectre-only implementation,
  instructions are truncated at 500 characters. When migrated to a
  Terminal.Gui modeless window, the window content should be scrollable to
  show the full instructions without truncation.

- **Instructions with special characters**: Instructions may contain
  characters that are meaningful in Spectre markup (brackets, etc.). All
  content is escaped via `Markup.Escape()` before rendering.

- **Narrow terminal**: The panel's rounded border adds 4 characters of
  overhead. Long values (source path, instructions) wrap naturally within
  the available width.

- **Agent name with spaces**: Agent names are derived from filenames and
  should not contain spaces. If they do, the name is escaped for rendering.

- **Non-interactive/piped terminal**: Renders normally. Spectre converts
  panel borders to ASCII when the terminal does not support Unicode.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | Section 1 | Error and usage messages |
| Modeless Window | (windowing model) | Floating window over conversation |

The detail panel uses a Grid inside a Panel -- a one-off layout similar to
the `/jea show` screen.
