# Screen: /agent list

## Overview

The agent list screen opens an interactive list in a modeless Window titled
"Agents", showing all available agent definitions with their description,
scope, and model override. Users can navigate the list and view detailed
agent information.

Agents are loaded from markdown files in `~/.boydcode/agents/` (user-scoped)
and `.boydcode/agents/` (project-scoped). Project-scoped agents override
user-scoped agents of the same name.

**Screen IDs**: AGENT-01, AGENT-02

## Trigger

- User types `/agent list` or `/agent` (default subcommand) during an active
  session.
- Handled by `AgentSlashCommand.HandleList()`.

## Route

Opens a modeless `Window` via the Interactive List pattern (component pattern
#28). The window floats over the conversation view. The agent continues working
in the background. The user dismisses with Esc.

## Layout (80 columns)

### With Agents

```
+-- Agents -------------------------------------------------+
|                                                            |
|  Name              Description                    Scope    |
|  ▶ senior-developer Implement features and fixes  Project  |
|    qa-expert        Write tests and QA review     Project  |
|    code-reviewer    Review code for quality       User     |
|    bug-hunter       Diagnose root causes          User     |
|    tui-ux-expert    Design terminal interfaces    Project  |
|                                                            |
|  Enter: Show  Esc: Close                                   |
|                                                            |
+------------------------------------------------------------+
```

The highlighted row (first row by default) uses `Theme.List.SelectedBackground`
(blue) with `Theme.List.SelectedText` (white). The `▶` arrow indicator marks
the focused row.

### Empty State

```
+-- Agents -------------------------------------------------+
|                                                            |
|                                                            |
|        No agents found.                                    |
|        Add agent definitions as markdown files:            |
|          User:    ~/.boydcode/agents/<name>.md             |
|          Project: .boydcode/agents/<name>.md               |
|                                                            |
|                                                            |
|  Esc: Close                                                |
|                                                            |
+------------------------------------------------------------+
```

When the list is empty, the empty message is centered and drawn with
`Theme.Semantic.Warning` (yellow) for the "No agents found." line, followed
by file path hints in `Theme.Semantic.Muted` (dark gray). The Action Bar
shows only `Esc: Close`.

### Anatomy

1. **Window** -- Modeless `Window` with `Theme.Modal.BorderScheme` (blue border),
   title "Agents", rounded border style, centered at 80% width / 70% height.

2. **Column Header** -- Static `Label` showing column names. Drawn with
   `Theme.Semantic.Muted` (dark gray). Columns:
   - **Name** -- left-aligned, agent name (filename without `.md`)
   - **Description** -- left-aligned, agent description from frontmatter
   - **Scope** -- left-aligned, `User` or `Project`

3. **List View** -- `ListView` with one row per agent. Scrollable when items
   exceed viewport height. The focused row uses
   `Theme.List.SelectedBackground` and `Theme.List.SelectedText`. The `▶`
   arrow indicator (`\u25b6`) marks the focused row in column 2.

4. **Row Content** --
   - **Name cell**: Agent name (the markdown filename without extension).
   - **Description cell**: Description string from the agent's frontmatter.
     Truncated with `...` if it exceeds the column width.
   - **Scope cell**: `User` or `Project` indicating where the agent definition
     file lives.

5. **Action Bar** (component pattern #29) -- Positioned at `Y = Pos.AnchorEnd(2)`.
   Shows available keyboard shortcuts. Agents are read-only definitions loaded
   from the filesystem, so the only actions are Show and Close:
   1. `Esc: Close` (always shown)
   2. `Enter: Show` (always shown)

## States

| State | Condition | Visual Difference |
|---|---|---|
| With agents | At least one agent is registered | List with one row per agent |
| Empty | No agents found | Yellow "No agents found." + dim file path hints |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

| Element | Token | Notes |
|---|---|---|
| Window border | `Theme.Modal.BorderScheme` | Blue border, rounded style |
| Selected row background | `Theme.List.SelectedBackground` | Accent blue |
| Selected row text | `Theme.List.SelectedText` | White on blue |
| Action bar text | `Theme.List.ActionBar` | Delegates to `Theme.Semantic.Muted` |
| Column headers | `Theme.Semantic.Muted` | Dark gray |
| Empty state "No agents found." | `Theme.Semantic.Warning` | Yellow |
| Empty state file path hints | `Theme.Semantic.Muted` | Dark gray |
| Data cell text | `Theme.Semantic.Default` | White |
| Row indicator | `\u25b6` (arrow) | Marks focused row |

## Interactive Elements

### Keyboard

| Key | Action |
|---|---|
| Up / k | Move selection up |
| Down / j | Move selection down |
| Enter | Show agent detail (opens detail modal -- see `/agent show`) |
| Esc | Close the window |

Single-letter hotkeys are handled in the window's `OnKeyDown` override and fire
only when the `ListView` has focus (not when a sub-dialog is open).

### Actions

- **Enter (Show)**: Opens a detail modal window showing the full agent
  definition (name, description, scope, model override, max turns, and the
  full system prompt / instructions). See `/agent show` screen spec.

## Behavior

- **Agent enumeration**: `_agentRegistry.GetAll()` returns all registered
  agents. Project-scoped agents that override user-scoped agents of the same
  name appear only once (the project-scoped version).

- **Column layout**: Three columns: Name, Description, Scope. All left-aligned.

- **Escape handling**: Agent names and descriptions are escaped before
  rendering to prevent interpretation of special characters.

- **Default subcommand**: When the user types `/agent` with no subcommand,
  `list` is used as the default.

- **Sorting**: Agents are listed alphabetically by name.

- **Window type**: Modeless window. The agent continues processing in the
  background while the window is open.

- **Dismiss**: Esc key closes the window. The conversation view is revealed
  underneath.

## Edge Cases

- **No agents at all**: The empty state shows guidance on where to place
  agent definition files. This occurs when neither `~/.boydcode/agents/`
  nor `.boydcode/agents/` contain any `.md` files.

- **Many agents (> viewport height)**: `ListView` scrolls natively. Practical
  agent counts are expected to be small (under 20).

- **Long descriptions**: Truncated with `...` at the column width boundary.
  The full description is visible in the detail view (Enter).

- **Long agent names**: Truncated with `...` at the column width boundary.
  Practical agent names are short (filesystem-friendly).

- **Narrow terminal (< 60 columns)**: Columns are dropped right-to-left to
  fit: Scope is dropped first, then Description. Name is always shown.
  Action bar drops less-important hints per pattern #29.

- **Non-interactive/piped terminal**: Falls back to column-aligned plain text
  output to stdout. No window, no interactivity. Colors are omitted. Format:

  ```
  Name              Description                    Scope
  senior-developer  Implement features and fixes   Project
  qa-expert         Write tests and QA review      Project
  code-reviewer     Review code for quality        User
  bug-hunter        Diagnose root causes           User
  ```

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Interactive List | #28 | ListView with keyboard navigation |
| Action Bar | #29 | Shortcut hints at bottom of window |
| Modal Overlay (List variant) | #11 | Modeless window over conversation |
| Empty State | #21 | "No agents found" + file path hints |
