# Screen: Execution Window (Prescriptive)

## Overview

The execution window manages the visual lifecycle of a tool call from the moment
the LLM decides to invoke a tool through to the collapsed result summary. It
renders entirely within the Content region of the chat loop Layout, using
Spectre.Console renderables updated via the Live display context. No raw ANSI
escape sequences, no manual cursor control, no 5-line scrolling window.

This spec is PRESCRIPTIVE -- it describes what the screen SHOULD look like.

---

## Lifecycle

A complete tool execution follows five phases:

```
1. Tool Preview    -> Panel with command/arguments displayed in Content
2. Waiting         -> Indicator bar: "@ Executing... (0.0s)" -- no output yet
3. Streaming       -> Indicator bar: "@ Executing... (N.Ns)" -- output buffered
4. Result          -> Badge in Content: checkmark/cross + line count + duration
5. Expand          -> (on demand) Full output replayed in Content via /expand
```

---

## Layout (120 columns) -- Phase 1: Tool Preview

When the LLM emits a `ToolUseBlock`, the tool preview panel renders in the
Content region. This happens BEFORE execution starts.

```
  I'll start by reading the current implementation.

  4,521 in / 245 out / 4,766 total

  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs -TotalCount 100                                                  |
  +------------------------------------------------------------------------------------------------------------+

----------------------------------------------------------------------------------------------------------------------------
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Panel Design

The tool preview is a `Panel` with:
- `BoxBorder.Rounded` border
- `Color.Grey` border color
- `[dim]` header text showing the tool name
- `Padding(1, 0)` internal padding
- `.Expand()` to fill the Content region width
- Content is the formatted command, `Markup.Escape`d

### Command Formatting

| Tool Name | Format |
|-----------|--------|
| Shell (command) | Command text. Multi-statement commands split on `; ` into separate lines. |
| Shell (with known tool hints) | Tool-specific formatting based on argument structure (Read -> file path + line range, Write -> path + char count, Edit -> path + diff preview, Glob -> pattern + path, Grep -> pattern + path + glob). |
| Unknown | Key-value pairs from JSON arguments. |

### Spectre.Console Implementation

```csharp
var preview = FormatToolPreview(toolName, argumentsJson);
var panel = new Panel(Markup.Escape(preview))
    .Header($"[dim]{Markup.Escape(toolName)}[/]")
    .Border(BoxBorder.Rounded)
    .BorderColor(Color.Grey)
    .Padding(1, 0)
    .Expand();
```

The panel is added to the conversation view as part of the current assistant
turn. It persists in the Content region after execution -- it does not disappear.

---

## Layout (80 columns) -- Phase 1: Tool Preview

```
  I'll start by reading the current code.

  4,521 in / 245 out / 4,766 total

  +- Shell -------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs         |
  |   -TotalCount 100                                 |
  +---------------------------------------------------+

------------------------------------------------------------
> _
Gemini | gemini-2.5-pro | my-project    /help
```

At 80 columns, the Panel content wraps. Long commands may span multiple lines
within the panel. The Panel's Expand() makes it fill the available width.

---

## Layout (120 columns) -- Phase 2: Waiting (Spinner)

After the tool preview renders, execution starts. The Indicator bar shows
the executing state:

```
  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs -TotalCount 100                                                  |
  +------------------------------------------------------------------------------------------------------------+

@ Executing... (0.3s)
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Indicator Bar

The Indicator bar shows `@ Executing... ({elapsed})` in blue text. The elapsed
time updates on each render cycle (~60fps). Format:

| Elapsed | Display |
|---------|---------|
| < 10s | `{seconds:F1}s` (e.g., `0.3s`, `9.8s`) |
| 10-59s | `{seconds:F0}s` (e.g., `12s`, `45s`) |
| >= 60s | `{minutes}m {seconds}s` (e.g., `1m 30s`) |

### Content Region

During execution, the Content region shows the conversation up to and including
the tool preview panel. No execution output is shown in the Content region
during execution -- output is buffered. This is a deliberate design choice:

**Rationale**: Showing live output in the Content region would require frequent
re-rendering of the entire conversation view (since Content is a single
renderable updated via Layout). Buffering the output and showing a compact
result badge on completion is simpler, faster, and provides a better experience
(the user sees the final result without scrolling through intermediate output).

The user can always see full output via `/expand` after execution.

---

## Layout (120 columns) -- Phase 3: Streaming Output (Buffered)

As the execution engine produces output lines, they are buffered in memory.
The Indicator bar continues showing the executing state with updated elapsed time.

```
@ Executing... (2.3s)
```

The Content region is unchanged. Output lines accumulate in a buffer (maximum
10,000 lines). The buffer is used for:
1. The line count in the result badge
2. The `/expand` command
3. The tool result sent back to the LLM

No visual change occurs in the Content region during output streaming. This is
the key simplification over v1's 5-line scrolling window with raw ANSI cursor
control.

---

## Layout (120 columns) -- Phase 4: Result Badge

When execution completes, the result badge appears in the Content region below
the tool preview panel.

### Success with output (> 5 lines)

```
  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs -TotalCount 100                                                  |
  +------------------------------------------------------------------------------------------------------------+
  \u2713 Shell  42 lines | 0.3s
  /expand to show full output
```

### Success with output (<= 5 lines)

```
  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs -TotalCount 5                                                    |
  +------------------------------------------------------------------------------------------------------------+
  \u2713 Shell  3 lines | 0.1s
```

No expand hint because the output is small. The full output IS visible via
`/expand` but the hint is omitted to reduce noise for trivial output.

### Success with no output

```
  +- Shell ---------------------------------------------------------------------------------------------------+
  | New-Item -ItemType Directory -Path src/Auth/Exceptions                                                     |
  +------------------------------------------------------------------------------------------------------------+
  \u2713 Shell  Command completed successfully.
```

### Error with output

```
  +- Shell ---------------------------------------------------------------------------------------------------+
  | dotnet build                                                                                               |
  +------------------------------------------------------------------------------------------------------------+
  \u2717 Shell  28 lines | 4.2s
  /expand to show full output
```

### Error with no output

```
  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Magic -Path src/                                                                                       |
  +------------------------------------------------------------------------------------------------------------+
  \u2717 Shell  'Get-Magic' is not recognized as a command.
```

### Badge Markup

```csharp
// Success with lines
$"  [green]\u2713[/] [dim]{Markup.Escape(toolName)}  {lineCount} lines | {duration}[/]"

// Error with lines
$"  [red]\u2717[/] [dim]{Markup.Escape(toolName)}  {lineCount} lines | {duration}[/]"

// Success no output
$"  [green]\u2713[/] [dim]{Markup.Escape(toolName)}  {Markup.Escape(truncatedResult)}[/]"

// Error no output
$"  [red]\u2717[/] [dim]{Markup.Escape(toolName)}  {Markup.Escape(truncatedError)}[/]"

// Expand hint (when lineCount > 5)
$"  [dim italic]/expand to show full output[/]"
```

### Style Tokens

- `[green]` + checkmark `\u2713` for success
- `[red]` + cross `\u2717` for error
- `[dim]` for tool name, metadata, result text
- `[dim italic]` for expand hint
- 2-space indent

---

## Layout (80 columns) -- Phase 4: Result Badge

```
  +- Shell -------------------------------------------+
  | dotnet test --no-build --logger                    |
  |   "console;verbosity=minimal"                     |
  +---------------------------------------------------+
  \u2713 Shell  28 lines | 4.2s
  /expand to show full output
```

Same structure, wraps at 80 columns.

---

## Layout (120 columns) -- Phase 5: Expanded Output (/expand)

When the user types `/expand`, the buffered output is shown as a modal overlay:

```
+-- Shell Output (42 lines) -------------------------------------------------------------------------------+
|                                                                                                          |
|  using System;                                                                                           |
|  using System.Threading.Tasks;                                                                           |
|  using Microsoft.Extensions.Logging;                                                                     |
|                                                                                                          |
|  namespace BoydCode.Auth;                                                                                |
|                                                                                                          |
|  public sealed class AuthService                                                                         |
|  {                                                                                                       |
|      private readonly ILogger<AuthService> _logger;                                                      |
|      ...                                                                                                 |
|      (remaining lines)                                                                                   |
|                                                                                                          |
|  Esc to dismiss                                                                                          |
|                                                                                                          |
+----------------------------------------------------------------------------------------------------------+
Esc to dismiss
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Behavior

`/expand` opens as a modal overlay (07-component-patterns.md #11). The Content
region is replaced with a Panel showing the buffered output. Esc dismisses the
modal and restores the conversation view.

If no output is buffered: the modal shows `No tool output to expand.`
If already expanded in this modal: no restriction (the user can reopen).

Only the most recent tool's output is available. A new tool execution
overwrites the previous buffer.

---

## Layout (120 columns) -- Cancelled Execution

When the user double-presses Esc during execution:

```
  +- Shell ---------------------------------------------------------------------------------------------------+
  | dotnet test --no-build                                                                                     |
  +------------------------------------------------------------------------------------------------------------+
  \u2717 Shell  Command cancelled.

----------------------------------------------------------------------------------------------------------------------------
> _
```

The execution engine receives a `CancellationToken` cancellation. The result
badge shows the cross symbol with "Command cancelled." The Indicator bar
returns to idle.

---

## Layout (120 columns) -- Consecutive Tool Calls

When a single agentic turn involves multiple tool calls:

```
  I'll need to read two files and then make changes.

  4,521 in / 200 out / 4,721 total

  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs                                                                  |
  +------------------------------------------------------------------------------------------------------------+
  \u2713 Shell  42 lines | 0.3s

  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/TokenValidator.cs                                                               |
  +------------------------------------------------------------------------------------------------------------+
  \u2713 Shell  28 lines | 0.2s

  Now I can see both files. Let me apply the error handling changes...

@ Streaming...
> _
```

Tool calls within the same turn flow continuously. No turn separator between
them. Each tool call gets its own preview panel and result badge.

---

## States

| State | Indicator Bar | Content Region |
|-------|---------------|----------------|
| Tool preview | Prior state (may be streaming or idle) | Panel with command |
| Waiting | `@ Executing... (0.0s)` (blue) | Unchanged (panel visible) |
| Streaming output | `@ Executing... (N.Ns)` (blue) | Unchanged (output buffered) |
| Result (success) | Idle (dim rule) or next state | Badge: checkmark + metadata |
| Result (error) | Idle (dim rule) or next state | Badge: cross + metadata |
| Cancelled | Idle (dim rule) | Badge: cross + "cancelled" |
| Expand (modal) | `Esc to dismiss` (dim) | Modal with buffered output |
| Cancel hint | `Press Esc again to cancel` (yellow) | Unchanged |

---

## Output Buffer Management

- Buffer is a `Queue<string>` or `List<string>` with maximum 10,000 lines.
- When the buffer exceeds 10,000 lines, the oldest line is dropped.
- After `RenderToolResult`, the buffer is saved for `/expand`, then cleared
  for the next tool call.
- Only one tool's output is saved at a time. A new tool execution overwrites
  the previous buffer.
- The buffer is also used to construct the `ToolResultBlock` sent back to
  the LLM (the full output, truncated per the conversation logger's limits).

---

## Edge Cases

- **Narrow terminal (< 80 columns)**: Tool preview panel wraps its content.
  Result badge wraps if very long. Modal overlay fills available width.

- **Very wide terminal (> 200 columns)**: Tool preview panel expands. Extra
  space is empty (text is left-aligned).

- **No output produced**: Execution stays in Waiting state. Result badge shows
  truncated result text instead of line count.

- **Extremely rapid output (>1000 lines/sec)**: Lines buffer in memory. No
  visual impact since output is not rendered during execution.

- **Very long output lines**: Full lines stored in buffer. `/expand` modal
  wraps long lines via Spectre.Console Panel word wrapping.

- **Buffer overflow (>10,000 lines)**: Oldest lines silently dropped. `/expand`
  shows the truncated buffer. A note could be added to the expand modal header
  indicating truncation.

- **JSON parse failure in tool preview**: The formatter catches all exceptions
  and falls back to showing raw JSON, truncated to 200 characters.

- **Non-interactive/piped environment**: No Layout. Tool preview renders as a
  standard Panel to stdout. Output lines print to stdout with 2-space indent.
  Result badge prints to stdout. No buffering, no collapse, no `/expand`.

- **Consecutive tool calls**: Each tool call goes through the full lifecycle.
  Only the most recent tool's output is available for `/expand`.

---

## Accessibility

### Screen Reader

- Tool preview panel is announced with the tool name and command text.
- During execution, "Executing" is announced once (not repeatedly).
- On completion, the result badge text is announced.
- `/expand` modal content is announced sequentially.

### NO_COLOR

- Tool preview panel loses grey border color but retains border structure.
- Result badges lose green/red but retain checkmark/cross symbols.
- Expand hint text remains readable.

### Accessible Mode

- Tool preview renders without borders (plain text with "Shell:" prefix).
- Result badge renders as text: `[OK] Shell 42 lines 0.3s` or `[ERR] Shell...`.
- No `@` character in indicator -- uses `[Executing...]` instead.

---

## Component Patterns Used

| Pattern | Reference | Usage |
|---------|-----------|-------|
| Tool Call Badge | 07-component-patterns.md #4 | Command preview panel |
| Tool Result Badge | 07-component-patterns.md #5 | Completion summary |
| Execution Progress | 07-component-patterns.md #6 | Indicator bar during execution |
| Modal Overlay | 07-component-patterns.md #11 | /expand output display |
| Cancel Hint | 07-component-patterns.md #20 | Double-press cancellation |
| Indicator Bar | 07-component-patterns.md #26 | Executing state display |
