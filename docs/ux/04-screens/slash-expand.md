# Screen: /expand

## Overview

The expand screen replays the full buffered output from the most recent tool
execution. During normal execution, tool output that exceeds 5 lines is
collapsed to a single summary line with an "/expand to show full output" hint.
This command opens the full output in a modeless Terminal.Gui `Window` overlay
titled "Shell Output (N lines)" where N is the line count.

**Screen IDs**: EXPAND-01, EXPAND-02, EXPAND-03

## Trigger

- User types `/expand` during an active session, after a tool execution has
  completed with collapsed output.
- Handled by `ExpandSlashCommand.TryHandleAsync()`, which delegates to
  `IUserInterface.ExpandLastToolOutput()`.

## Layout (80 columns)

### Expanded Output (modeless Window)

```
+-- Shell Output (42 lines) -------------------------------------------+
|                                                                       |
|  Line 1 of output                                                     |
|  Line 2 of output                                                     |
|  Line 3 of output                                                     |
|  Line 4 of output                                                     |
|  Line 5 of output                                                     |
|  Line 6 of output                                                     |
|  Line 7 of output                                                     |
|  Line 8 of output                                                     |
|  Line 9 of output                                                     |
|  Line 10 of output                                                    |
|  ...                                                                  |
|  Line 42 of output                                                    |
|                                                                       |
|  Esc to dismiss                                                   35% |
|                                                                       |
+-----------------------------------------------------------------------+
```

The buffered lines are displayed inside a read-only `TextView` within the
modal window. The full original line content is preserved in the buffer
(unlike the execution window which truncates to terminal width). When
content exceeds the viewport, a scroll position indicator (percentage
format) appears in the bottom-right corner (pattern #33).

### No Output Available (conversation view)

```
No tool output to expand.
```

Rendered as a `StatusMessageBlock` with `MessageKind.Hint` in the
conversation view. This is a status message, not a modal -- it belongs
in the conversation alongside other feedback.

### Already Expanded (conversation view)

```
Output already expanded.
```

Rendered as a `StatusMessageBlock` with `MessageKind.Hint` in the
conversation view.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Expanded | Buffered output exists and not yet expanded | Modeless Window with full output, scrollable |
| No output | No tool has run, or last tool produced no output | Dim status message in conversation view: "No tool output to expand." |
| Already expanded | `/expand` called a second time for the same tool | Dim status message in conversation view: "Output already expanded." |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

- `Theme.Modal.BorderScheme` -- blue border on the modal window
- `Theme.Semantic.Muted` -- dim styling for "No tool output to expand."
  and "Output already expanded." status messages, dismiss hint text,
  scroll position indicator

## Interactive Elements

### Keyboard

| Key | Action |
|---|---|
| Up/Down | Scroll content within the modal window |
| Esc | Dismiss the modal window |

## Behavior

- **Buffer source**: The output buffer is captured by `ExecutionWindow
  .RenderToolResult()` when a tool execution completes in contained output
  mode. The buffer is a snapshot of all output lines received during
  execution (up to the 10,000 line buffer limit).

- **Window title**: The modal title includes the line count:
  `"Shell Output (N lines)"` where N is the number of buffered lines.

- **Modeless window**: The expanded output opens in a modeless Window via
  `ShowModal`. The agent continues working in the background. Esc dismisses
  the window and restores focus to the conversation.

- **Single-use**: The `_lastOutputExpanded` flag is set to `true` after the
  first expansion. Subsequent `/expand` calls return the "already expanded"
  status message in the conversation view. The flag resets when a new tool
  execution completes.

- **One tool at a time**: Only the most recent tool's output is buffered.
  When a new tool execution begins, the previous buffer is overwritten.

- **No buffer in non-TUI mode**: When the application is not running in
  Terminal.Gui mode, the buffer is not saved. In this mode, tool output is
  displayed inline without collapsing, so `/expand` always shows "No tool
  output to expand."

## Edge Cases

- **Very large output (10,000 lines)**: All buffered lines are displayed
  in the scrollable modal window. The scroll position indicator helps the
  user orient within the content.

- **Buffer truncation**: If the execution produced more than 10,000 lines,
  the oldest lines were dropped during buffering. The expanded output
  reflects the truncated buffer (most recent 10,000 lines).

- **No previous execution**: At session start, before any tool has run,
  the buffer is null. `/expand` shows "No tool output to expand." in the
  conversation view.

- **Non-interactive/piped terminal**: Not applicable. The modal window is
  only shown during an active Terminal.Gui session. In non-TUI mode, tool
  output is displayed inline without collapsing.

- **Content exceeds viewport**: The modal window is scrollable. A
  percentage-format scroll position indicator (e.g., `35%`) appears in
  the bottom-right corner when content exceeds the viewport height
  (pattern #33).

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Modal Overlay (Text variant) | #11 | Modeless Window for the expanded output |
| Status Message | #7 | "No tool output" and "Already expanded" hints |
| Scroll Position Indicator | #33 | Percentage indicator when content exceeds viewport |

See also: [execution-window.md](execution-window.md) for the buffer source and expand lifecycle.
