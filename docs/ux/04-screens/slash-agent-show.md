# Screen: /agent show

## Overview

Displays a detailed view of a single agent definition in a modeless Detail
Modal window (component pattern #11, Variant C), showing name, description,
scope, model override, max turns, source file path, and the full system
prompt/instructions. The instructions are the body of the agent's markdown
file and may be lengthy.

All content is drawn using Terminal.Gui native drawing (`SetAttribute`,
`Move`, `AddStr`) with structured key-value layout and a scrollable
instructions section.

**Screen IDs**: AGENT-03, AGENT-04, AGENT-05

## Trigger

`/agent show <name>`

- The `name` argument is required.
- If `name` is omitted, a usage hint is shown.

## Layout (80 columns)

### Full Agent Detail

```
+-- Agent: senior-developer --------------------------------+
|                                                            |
|  Name          senior-developer                            |
|  Description   Implement features and fixes from specs     |
|  Scope         Project                                     |
|  Model         default                                     |
|  Max Turns     25                                          |
|  Source        C:\Users\jason\source\repos\MyProject\      |
|                .boydcode\agents\senior-developer.md         |
|                                                            |
|  -- Instructions ---                                       |
|  You are an expert .NET developer. You implement features  |
|  from design specs produced by the software architect.     |
|  Follow Clean Architecture patterns. Use sealed classes    |
|  for entities. Use immutable collections on public APIs.   |
|  ...                                                       |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

### Agent with Model Override

```
+-- Agent: code-reviewer -----------------------------------+
|                                                            |
|  Name          code-reviewer                               |
|  Description   Review code for quality and correctness     |
|  Scope         User                                        |
|  Model         claude-sonnet-4                             |
|  Max Turns     10                                          |
|  Source        C:\Users\jason\.boydcode\agents\            |
|                code-reviewer.md                             |
|                                                            |
|  -- Instructions ---                                       |
|  You are a senior code reviewer. Focus on correctness,     |
|  readability, and maintainability. Flag potential bugs...  |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
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
| Full detail | Agent exists | Window with header info pairs + instructions section |
| Not found | Name does not match any registered agent | Red error with escaped agent name |
| No name | Name argument omitted | Yellow usage hint |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

- `Theme.Modal.BorderScheme` -- blue border on the modeless window
- `Theme.Semantic.Muted` -- dim labels in info pairs ("Name", "Description",
  "Scope", "Model", "Max Turns", "Source"), section divider rule,
  "Esc to dismiss"
- `Theme.Semantic.Info` -- cyan values in info pairs (agent name, scope,
  model, max turns, source path)
- `Theme.Semantic.Default` -- white description text, instructions text
- `Theme.Semantic.Error` -- red "Error:" prefix for not-found
- `Theme.Semantic.Warning` -- yellow "Usage:" prefix

## Interactive Elements

None. This is a read-only detail view.

## Behavior

- **Name resolution**: The name is taken from the trailing arguments after
  `/agent show`. If empty, a yellow usage message is rendered and the command
  returns.

- **Agent lookup**: `_agentRegistry.GetByName(name)` performs a
  case-insensitive lookup. Returns null if the agent is not registered.

- **Window construction**: `ShowDetailModal` opens a Terminal.Gui modeless
  `Window` with a blue border (`Theme.Modal.BorderScheme`). The window title
  is `"Agent: {name}"`.

- **Native drawing layout**: The window's inner `View` overrides
  `OnDrawingContent` to draw the structured layout:

  - **Header info pairs**: Six rows using the Info Grid pattern (pattern
    #9). Labels ("Name", "Description", "Scope", "Model", "Max Turns",
    "Source") at `x = 2` in `Theme.Semantic.Muted`, values at label pad
    offset in `Theme.Semantic.Info`. The "Description" value uses
    `Theme.Semantic.Default` since it is prose content, not a data
    identifier.

  - **Instructions section**: A Section Divider (pattern #8) labeled
    "Instructions". The full `Instructions` text from the agent definition
    is drawn line by line at 2-char indent in `Theme.Semantic.Default`.
    Text is word-wrapped at the window's inner width.

- **Model display**: Shows `ModelOverride` if set, in `Theme.Semantic.Info`.
  If null, shows "default" in `Theme.Semantic.Muted` (indicating the agent
  uses the session's active model).

- **Max Turns display**: Shows `MaxTurns` if set. If null, shows
  "default (25)" in `Theme.Semantic.Muted` indicating the
  `AgentDefaults.DefaultMaxTurns` value.

- **Source path**: Shows `SourcePath` -- the full filesystem path to the
  agent's markdown file, in `Theme.Semantic.Info`. Long paths wrap within
  the window.

- **Dismiss**: Esc key closes the window. The `ActivityBarView` transitions
  to `ActivityState.Modal` while the window is open.

- **Window type**: Modeless window. The agent continues processing in the
  background while the window is open.

## Edge Cases

- **Very long instructions**: The instructions field may contain hundreds or
  thousands of characters. The window scrolls via Terminal.Gui's built-in
  Viewport scrolling when content exceeds the terminal height (capped at 90%
  per pattern #11 sizing rules). Full instructions are shown without
  truncation.

- **Instructions with special characters**: All content is drawn via
  `AddStr` -- no markup interpretation occurs. Raw text is displayed as-is.

- **Narrow terminal**: The window is sized to fit content up to the terminal
  width (capped at 90%). Long values (source path, instructions) wrap
  naturally within the available width.

- **Agent name with spaces**: Agent names are derived from filenames and
  should not contain spaces. If they do, the name is rendered as-is via
  `AddStr`.

- **Long source paths**: The source path wraps within the window. The label
  pad offset keeps subsequent wrapped lines aligned with the value column.

## Non-TUI Fallback

When running in non-interactive/piped mode (no Terminal.Gui), the agent
detail is rendered as plain text to stdout. Info pairs use string-padded
columns. Section dividers use `--` prefix.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Modal Overlay (Detail Modal) | #11, Variant C | Modeless window with native drawing layout |
| Info Grid | #9 | Header key-value pairs (name, description, scope, model, max turns, source) |
| Section Divider | #8 | "Instructions" heading |
| Status Message | #7 | Error and usage messages |
