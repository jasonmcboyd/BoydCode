# Screen: /expand

## Overview

The expand screen replays the full buffered output from the most recent tool
execution. During normal execution, tool output that exceeds 5 lines is
collapsed to a single summary line with an "/expand to show full output" hint.
This command restores the full output into the scrollback.

**Screen IDs**: EXPAND-01, EXPAND-02, EXPAND-03

## Trigger

- User types `/expand` during an active session, after a tool execution has
  completed with collapsed output.
- Handled by `ExpandSlashCommand.TryHandleAsync()`, which delegates to
  `IUserInterface.ExpandLastToolOutput()`, which delegates to
  `ExecutionWindow.ExpandLastToolOutput()`.

## Layout (80 columns)

### Expanded Output

```
  Line 1 of output
  Line 2 of output
  Line 3 of output
  ...
  Line 42 of output
```

Each buffered line is written with a 2-space indent prefix. Lines are written
sequentially with no truncation (the full original line content is preserved
in the buffer, unlike the scrolling window which truncates to terminal width).

### No Output Available

```
No tool output to expand.
```

### Already Expanded

```
Output already expanded.
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Expanded | Buffered output exists and not yet expanded | Full output replayed with 2-space indent per line |
| No output | No tool has run, or last tool produced no output | Dim: "No tool output to expand." |
| Already expanded | `/expand` called a second time for the same tool | Dim: "Output already expanded." |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[dim]` | dim (2.2) | "No tool output to expand." and "Output already expanded." messages |

## Interactive Elements

None. This is a non-interactive output replay.

## Behavior

- **Buffer source**: The output buffer is captured by `ExecutionWindow
  .RenderToolResult()` when a tool execution completes in contained output
  mode. The buffer is a snapshot of all output lines received during
  execution (up to the 10,000 line buffer limit).

- **Single-use**: The `_lastOutputExpanded` flag is set to `true` after the
  first expansion. Subsequent `/expand` calls return the "already expanded"
  message. The flag resets when a new tool execution completes.

- **One tool at a time**: Only the most recent tool's output is buffered.
  When a new tool execution begins, the previous buffer is overwritten.

- **No buffer in non-ANSI mode**: When `_useContainedOutput` is false (non-
  ANSI terminal), the buffer is not saved (`_lastOutputBuffer = null`).
  In this mode, tool output is displayed inline without collapsing, so
  `/expand` always shows "No tool output to expand."

- **Output routing**: In layout mode, expanded lines are written to the
  output scroll region. In non-layout mode, they are written to stdout
  directly.

## Edge Cases

- **Very large output (10,000 lines)**: All buffered lines are replayed.
  This produces significant scrollback output. No pagination exists.

- **Buffer truncation**: If the execution produced more than 10,000 lines,
  the oldest lines were dropped during buffering. The expanded output
  reflects the truncated buffer (most recent 10,000 lines).

- **No previous execution**: At session start, before any tool has run,
  the buffer is null. "/expand" shows "No tool output to expand."

- **Non-interactive/piped terminal**: Renders normally. Output lines are
  written sequentially to stdout.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Execution Window | Section 10 | Expand functionality is part of the execution window lifecycle |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| ExpandSlashCommand | `Commands/ExpandSlashCommand.cs` | `TryHandleAsync` | 20-30 |
| UI delegation | `SpectreUserInterface.cs` | `ExpandLastToolOutput` | 345 |
| ExpandLastToolOutput | `Terminal/ExecutionWindow.cs` | `ExpandLastToolOutput` | 250-270 |
| Buffer capture | `Terminal/ExecutionWindow.cs` | `RenderToolResult` | 172-173 |
| Output replay loop | `Terminal/ExecutionWindow.cs` | `ExpandLastToolOutput` | 264-269 |
| Already expanded guard | `Terminal/ExecutionWindow.cs` | `ExpandLastToolOutput` | 258-262 |
| No buffer guard | `Terminal/ExecutionWindow.cs` | `ExpandLastToolOutput` | 252-256 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
