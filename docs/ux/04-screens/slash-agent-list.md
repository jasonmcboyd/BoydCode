# Screen: /agent list

## Overview

Lists all available agent definitions in a tabular format showing name,
description, scope (User or Project), and model override. Agents are loaded
from markdown files in `~/.boydcode/agents/` (user-scoped) and
`.boydcode/agents/` (project-scoped). Project-scoped agents override
user-scoped agents of the same name.

This screen opens as a modeless window floating over the conversation view.
The agent continues working in the background and the user can dismiss the
window with Esc to see the updated conversation underneath.

**Screen IDs**: AGENT-01, AGENT-02

## Trigger

- User types `/agent list` or `/agent` (default subcommand) during an active
  session.
- Handled by `AgentSlashCommand.HandleList()`.

## Layout (80 columns)

### With Agents

```
╭──────────────────────────────────────────────────────────────────────────────╮
│                                                                              │
│  Name             Description                    Scope     Model             │
│  ─────────────────────────────────────────────────────────────────────────    │
│  senior-developer  Implement features and fixes  Project   default           │
│  qa-expert         Write tests and QA review     Project   default           │
│  code-reviewer     Review code for quality       User      claude-sonnet-4   │
│  bug-hunter        Diagnose root causes          User      default           │
│                                                                              │
│                                              Esc to close                    │
╰──────────────────────────────────────────────────────────────────────────────╯
```

### Empty State

```
╭──────────────────────────────────────────────────────────────────────────────╮
│                                                                              │
│  No agents found.                                                            │
│  Add agent definitions as markdown files:                                    │
│    User:    ~/.boydcode/agents/<name>.md                                     │
│    Project: .boydcode/agents/<name>.md                                       │
│                                                                              │
│                                              Esc to close                    │
╰──────────────────────────────────────────────────────────────────────────────╯
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Table with agents | At least one agent is registered | Table with one row per agent |
| Empty | No agents found | Yellow "No agents found." + dim file path hints |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | Table column headers (via SimpleTable pattern) |
| `[dim]` | dim (2.2) | "default" model placeholder when no model override; empty state hint lines; file path examples |
| `[yellow]` | warning-yellow (1.1) | "No agents found." message |
| `BoxBorder.Rounded` | (border style) | Window border |

## Interactive Elements

None. This is a read-only reference window.

## Behavior

- **Agent enumeration**: `_agentRegistry.GetAll()` returns all registered
  agents. Project-scoped agents that override user-scoped agents of the same
  name appear only once (the project-scoped version).

- **Column layout**: Four columns: Name, Description, Scope, Model. Name and
  Description are left-aligned. Scope shows the `AgentScope` enum value
  ("User" or "Project"). Model shows `ModelOverride` if set, otherwise
  "default" in dim.

- **Escape handling**: Agent names and descriptions are escaped via
  `Markup.Escape()` before rendering.

- **Default subcommand**: When the user types `/agent` with no subcommand,
  `list` is used as the default.

- **Window type**: Modeless window. The agent continues processing in the
  background while the window is open.

- **Dismiss**: Esc key or clicking outside the window closes it. The
  conversation view is revealed underneath.

## Edge Cases

- **No agents at all**: The empty state shows guidance on where to place
  agent definition files. This occurs when neither `~/.boydcode/agents/`
  nor `.boydcode/agents/` contain any `.md` files.

- **Many agents**: All agents are rendered in the table with no pagination.
  Practical agent counts are expected to be small (under 20). If the table
  exceeds the terminal height, the window should scroll.

- **Long descriptions**: Descriptions wrap within the table cell. Spectre's
  table handles word wrapping at the column boundary.

- **Long agent names**: Names wrap within the Name column. Practical agent
  names are short (filesystem-friendly).

- **Narrow terminal (< 60 columns)**: The table columns compress. The
  Description column absorbs most of the compression. At very narrow widths,
  content may wrap but remains readable.

- **Non-interactive/piped terminal**: Renders normally. Spectre converts
  table borders to ASCII when the terminal does not support Unicode.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Simple Table | Section 4 | Agent list table |
| Empty State | Section 13 | "No agents found" + file path hints |
| Modeless Window | (windowing model) | Floating window over conversation |
