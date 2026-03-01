# Flow: Tool Execution

## Overview

The complete tool execution sub-flow within an agent turn. When the LLM
responds with `stop_reason == "tool_use"`, the response contains one or more
`ToolUseBlock` content blocks. Each block is processed sequentially: its
command is previewed, executed, and the result is added to the conversation
for the next LLM round.

This flow covers the full lifecycle of a single tool call, from detection
through result rendering, as well as the handling of multiple sequential
tool calls, cancellation during execution, and error paths.

## Preconditions

- An agent turn is in progress (`RunAgentTurnAsync` is running).
- The LLM response has `HasToolUse == true`.
- The execution engine is initialized (`_activeEngine.IsInitialized`).
- `SetAgentBusy(true)` has been called.

## Flow Diagram

```
    [LLM response with tool_use blocks]
    response.ToolUseCalls enumerated
         |
         v
    FOR EACH toolCall in toolCalls:
         |
         v
    [Is tool name "Shell"?]
         |
    +----+----+
    |         |
    v         v
  [Yes]     [No: unknown tool]
    |         |
    |         v
    |    EXEC-16: "Error: Unknown tool '{name}'. Use the Shell tool."
    |    Added as tool result to conversation (no visual render)
    |    [Continue to next tool call]
    |
    v
    [Is engine initialized?]
         |
    +----+----+
    |         |
    v         v
  [Yes]     [No]
    |         |
    |         v
    |    EXEC-17: "Error: Execution engine not initialized."
    |    Added as tool result to conversation (no visual render)
    |    [Continue to next tool call]
    |
    v
    [Log tool call]
    [Parse command from ArgumentsJson]
         |
         v
    EXEC-01: Tool call preview panel
    Grey-bordered panel with dim "Shell" header
    Body shows the command text
         |
         v
    [Start execution]
    _ui.RenderExecutingStart()
         |
         v
    [Create cancellation monitor]
    BeginCancellationMonitor links Esc/Ctrl+C to executionCts
         |
         v
    EXEC-02: Waiting spinner
    Braille animation: "Executing... (0.0s)"
         |
         v
    [ExecuteAsync with onOutputLine callback]
         |
    +----+----+----+----+
    |    |    |         |
    v    v    v         v
  [Output  [No     [Cancel   [Exception]
  lines    output]  pressed]
  arrive]    |       |         |
    |        |       v         v
    v        |     EXEC-14   EXEC-15
  EXEC-02    |     "Command  "Error
  -> 03/04   |     cancelled" executing
  /05        |       |       command: ..."
  (streaming |       |         |
  output)    |       |         |
    |        |       |         |
    +--------+---+---+---------+
                 |
                 v
           _ui.RenderExecutingStop()
                 |
                 v
           [Format output for LLM]
           Append error output if present
           Append available commands if "not recognized"
           Truncate to 30,000 chars
                 |
                 v
           EXEC-06..13: Tool result rendered
           (varies by line count, error status, ANSI capability)
                 |
                 v
           [Log tool result]
           [Add ToolResultBlock to conversation]
                 |
                 v
           [END FOR EACH]
                 |
                 v
           [Return to agent turn loop]
           (next LLM request with tool results)
```

## Steps (Detailed)

### Step 1: Tool Call Validation

- **Screen**: EXEC-16 or EXEC-17 (only on validation failure)
- **User sees**: Nothing on validation success. On failure:
  - Unknown tool: No visual output. The error is added silently to the
    conversation as a `ToolResultBlock` with `isError: true`. The LLM
    will see: `"Error: Unknown tool 'Bash'. Use the Shell tool."`
  - Engine not initialized: Same pattern. Error message:
    `"Error: Execution engine not initialized."`
- **System response**: Each `ToolUseBlock` is checked:
  1. Name must be "Shell" (case-insensitive). The LLM occasionally
     hallucinates other tool names; the error message redirects it.
  2. `_activeEngine.IsInitialized` must be true.
  If either check fails, a tool result is added to the conversation
  and the loop continues to the next tool call without executing.
- **Transitions to**: Step 2 (on success) or next tool call (on failure)

### Step 2: Tool Call Preview

- **Screen**: EXEC-01
- **User sees**: A grey-bordered panel with the tool name in dim text as
  the header, and the command text as the body:
  ```
  +-- Shell ------------------------------------------+
  | Get-ChildItem -Path C:\Users\jason\source -Recurse |
  +----------------------------------------------------+
  ```
  The preview body is formatted by `FormatToolPreview`, which parses the
  `ArgumentsJson` and extracts the `command` property. For the Shell tool,
  multi-statement commands (separated by `; `) are displayed on separate
  lines. The panel body is escaped with `Markup.Escape` to prevent markup
  injection from LLM-generated content.
- **User action**: Reads the command about to be executed.
- **System response**: `_ui.RenderToolExecution(toolName, argumentsJson)`
  renders the panel. In layout mode, it is written to the output scroll
  region via `_layout.WriteRenderable`. In non-layout mode, it is written
  via `AnsiConsole.Write`.
- **Transitions to**: Step 3

### Step 3: Execution Start

- **Screen**: EXEC-02 (waiting spinner)
- **User sees**: An animated braille spinner with elapsed time:
  ```
    . Executing... (0.0s)
  ```
  The spinner uses 8 braille pattern characters cycling at 100ms intervals.
  The elapsed time updates continuously.
- **User action**: Waits for execution to complete, or presses Esc/Ctrl+C
  to initiate cancellation.
- **System response**:
  1. `_ui.RenderExecutingStart()` sets `_isExecuting = true`, resets the
     execution window state, and starts the spinner background task.
  2. The `ArgumentsJson` is parsed to extract the `command` string.
  3. A linked `CancellationTokenSource` is created, combining the session's
     cancellation token with a per-execution token.
  4. `BeginCancellationMonitor` sets up the double-press cancellation
     handler (see cancellation.md).
  5. `_activeEngine.Engine.ExecuteAsync` is called with the command, working
     directory, output callback, and linked cancellation token.
- **Transitions to**: Step 4a, 4b, 4c, or 4d

### Step 4a: Output Lines Arrive (Layout Mode)

- **Screen**: EXEC-03
- **User sees**: The spinner stops. A single updating line replaces it:
  ```
    Executing... [15 lines | 2.3s]
  ```
  The line count and elapsed time update as new output arrives. Individual
  output lines are buffered but not shown (they are available via `/expand`
  after execution completes).
- **System response**: `ExecutionWindow.AddOutputLine` transitions from
  `Waiting` to `Streaming` state on the first output line (stopping the
  spinner). In layout mode, lines are buffered in `_outputBuffer` and a
  progress indicator is written to the output scroll region. Lines are
  capped at 10,000 in the buffer.
- **Transitions to**: Step 5

### Step 4b: Output Lines Arrive (Non-Layout, Filling Phase)

- **Screen**: EXEC-04
- **User sees**: Output lines appear one at a time with 2-space indent,
  up to the first 5 lines:
  ```
    Line 1 of output
    Line 2 of output
    Line 3 of output
  ```
- **System response**: When `_outputBuffer.Count <= WindowSize (5)`,
  `RedrawWindow` writes each new line directly to the console. Lines are
  truncated to fit terminal width minus 6 characters.
- **Transitions to**: Step 4c (when > 5 lines) or Step 5 (execution ends)

### Step 4c: Output Lines Arrive (Non-Layout, Scrolling Phase)

- **Screen**: EXEC-05
- **User sees**: A 5-line sliding window that updates in place. The first
  line shows a counter with elapsed time:
  ```
    Line 12 of output              [15 lines | 2.3s]
    Line 13 of output
    Line 14 of output
    Line 15 of output
    Line 16 of output
  ```
  New lines push older lines out of the window. The counter updates with
  each new line. Redraws are throttled to 50ms intervals to prevent
  flicker.
- **System response**: `RedrawWindow` uses ANSI cursor-up sequences to
  rewrite the visible window. The last 5 lines from `_outputBuffer` are
  displayed. Lines that exceed terminal width are truncated with "...".
  A `_redrawPending` flag handles throttled redraws.
- **Transitions to**: Step 5

### Step 4d: No Output

- **Screen**: EXEC-02 continues (spinner keeps running)
- **User sees**: The spinner animation continues until execution completes.
  Some commands produce no stdout/stderr output (e.g., `Set-Content`,
  `New-Item -ItemType Directory`).
- **System response**: The `onOutputLine` callback is never called. The
  `outputStreamed` flag remains false.
- **Transitions to**: Step 5

### Step 5: Execution Complete

- **Screen**: EXEC-06 through EXEC-13 (varies by conditions)
- **User sees**: The execution window stops (spinner cleared, final
  redraw if needed), and a tool result summary line appears. The exact
  rendering depends on several factors:

  **Success, > 5 lines, ANSI terminal** (EXEC-06):
  ```
    [Shell] 42 lines | 3.2s  (/expand to show full output)
  ```
  "[Shell]" in green. The visible output lines are collapsed (cursor-up
  and clear). The "/expand" hint appears in dim italic.

  **Success, 1-5 lines** (EXEC-07):
  ```
    [Shell] 3 lines | 0.8s
  ```
  "[Shell]" in green. Output lines remain visible above the summary.

  **Success, 0 lines** (EXEC-08):
  ```
    [Shell] Command completed successfully.
  ```
  "[Shell]" in green. The result text (from the engine) is shown dim,
  truncated to 200 characters.

  **Error, > 5 lines, ANSI terminal** (EXEC-09):
  ```
    [Shell error] 12 lines | 1.5s  (/expand to show full output)
  ```
  "[Shell error]" in red.

  **Error, 1-5 lines** (EXEC-10):
  ```
    [Shell error] 3 lines | 0.4s
  ```

  **Error, 0 lines** (EXEC-11):
  ```
    [Shell error] The term 'foo' is not recognized as a cmdlet...
  ```
  Error text shown directly, truncated to 500 characters.

  **Non-ANSI terminal** (EXEC-12, EXEC-13):
  Summary with truncated result text. No collapse/expand capability.

- **System response**:
  1. `_ui.RenderExecutingStop()` calls `ExecutionWindow.Stop()`, which
     stops the spinner, completes any pending redraws, and clears residual
     text.
  2. The output is formatted for the LLM:
     - If the command had errors and error output, it is appended with
       an "Errors:" section.
     - If the error contains "is not recognized", the list of available
       commands is appended to help the LLM self-correct.
     - Output exceeding 30,000 characters is truncated with "...".
  3. `_ui.RenderToolResult` calls `ExecutionWindow.RenderToolResult` with
     the tool name, display output, and error flag.
  4. The output buffer is saved for `/expand` (if ANSI mode).
  5. The tool result is logged and added to the conversation as a
     `ToolResultBlock` keyed by the `ToolUseBlock.Id` for correlation.
- **Transitions to**: Next tool call (if more in the batch) or return to
  agent turn loop.

## Multiple Tool Calls in Sequence

When the LLM emits multiple `tool_use` blocks in a single response, they
are processed sequentially in the order they appear:

```
LLM Response:
  TextBlock: "I'll check the file structure and read the config..."
  ToolUseBlock: { name: "Shell", args: { command: "Get-ChildItem src/" } }
  ToolUseBlock: { name: "Shell", args: { command: "Get-Content config.json" } }

Execution sequence:
  1. EXEC-01: Panel for "Get-ChildItem src/"
  2. EXEC-02..05: Execute and stream output
  3. EXEC-06..13: Tool result for first command
  4. EXEC-01: Panel for "Get-Content config.json"
  5. EXEC-02..05: Execute and stream output
  6. EXEC-06..13: Tool result for second command
  7. Both results added to conversation
  8. Return to agent turn loop for next LLM request
```

Each tool call gets its own preview panel, execution window, and result
summary. The cancellation monitor is per-execution (each call gets its
own linked CancellationTokenSource).

The `/expand` command only expands the last tool call's output (the most
recent `_lastOutputBuffer`). Previous tool outputs are not retained for
expansion.

## Cancellation During Execution

When the user presses Esc or Ctrl+C during tool execution:

```
    [Execution in progress]
         |
    [First Esc/Ctrl+C press]
         |
         v
    CANCEL-01: "Press Esc or Ctrl+C again to cancel"
    (1-second window starts)
         |
    +----+----+
    |         |
    v         v
  [Second   [Timer
  press     expires]
  within      |
  1s]         v
    |       CANCEL-02: Hint cleared
    |       Execution continues
    |       (spinner or output resumes)
    v
  [executionCts.Cancel()]
    |
    v
  OperationCanceledException caught
  (only if session ct is NOT cancelled)
    |
    v
  _ui.RenderExecutingStop()
    |
    v
  EXEC-14: "[Shell] Command cancelled."
  Tool result: "Command was cancelled by the user."
  Added to conversation (isError: false)
    |
    v
  [Continue to next tool call or next LLM round]
```

Key details:
- Cancellation only cancels the per-execution CTS, not the session CTS.
  The session continues running.
- The cancelled result is added to the conversation so the LLM knows the
  command was cancelled (it may retry or take a different approach).
- The `isError` flag is `false` for cancellation (it is a user action, not
  a tool failure).
- If the session-level cancellation token is cancelled (e.g., Ctrl+C
  without an active cancellation monitor), the `OperationCanceledException`
  propagates up and ends the session.

## Decision Points

| # | Decision Point | Condition | Outcome |
|---|---|---|---|
| D1 | Tool name | "Shell" (case-insensitive) | Proceed to execution |
|    |           | Any other name | EXEC-16: error result, skip execution |
| D2 | Engine initialized | `_activeEngine.IsInitialized == true` | Proceed |
|    |                    | `== false` | EXEC-17: error result, skip execution |
| D3 | Output mode | Interactive + ANSI capable | Contained output (buffered, windowed) |
|    |             | Otherwise | Non-contained (direct write) |
| D4 | Layout mode | Layout active | Progress indicator line (EXEC-03) |
|    |             | Layout not active | Filling/scrolling window (EXEC-04/05) |
| D5 | Output line count | 0 lines | EXEC-08/11/12/13 (no output variants) |
|    |                   | 1-5 lines | EXEC-07/10 (short, lines stay visible) |
|    |                   | > 5 lines, ANSI | EXEC-06/09 (collapsed with /expand) |
|    |                   | > 5 lines, non-ANSI | EXEC-12/13 (truncated summary) |
| D6 | Execution result | Success | Green "[Shell]" badge |
|    |                  | Error | Red "[Shell error]" badge |
| D7 | Error contains "not recognized" | Yes | Append available commands list |
|    |                                 | No | Standard error output |
| D8 | Output length | <= 30,000 chars | Full output sent to LLM |
|    |               | > 30,000 chars | Truncated with "..." |
| D9 | Cancellation | First press within window | Execute cancellation |
|    |              | Timer expires | Continue execution |

## Error Paths

### E1: Unknown Tool Name

- **Screen**: EXEC-16 (no visual render -- silent conversation entry)
- **Conversation impact**: A `ToolResultBlock` with `isError: true` and
  message `"Error: Unknown tool 'Bash'. Use the Shell tool."` is added.
  The LLM sees this in the next round and should self-correct.
- **Recovery**: Automatic -- the LLM usually switches to the Shell tool.

### E2: Engine Not Initialized

- **Screen**: EXEC-17 (no visual render)
- **Conversation impact**: `ToolResultBlock` with `isError: true` and
  message `"Error: Execution engine not initialized."`.
- **Recovery**: Unlikely during normal operation. May occur if the engine
  failed to initialize during startup.

### E3: Command Parse Failure

If `ArgumentsJson` cannot be parsed or lacks a `command` property:

- **Screen**: EXEC-15
- **User sees**:
  ```
    [Shell error] Error executing command: command is required
  ```
- **Conversation impact**: `ToolResultBlock` with the error message and
  `isError: true`.
- **Recovery**: The LLM sees the error and should provide valid arguments.

### E4: Execution Exception

If `ExecuteAsync` throws an exception (other than `OperationCanceledException`):

- **Screen**: EXEC-15
- **User sees**:
  ```
    [Shell error] Error executing command: {exception message}
  ```
- **System response**: `RenderExecutingStop` clears the execution window.
  The error message is rendered as a tool result. The exception is logged.
- **Conversation impact**: `ToolResultBlock` with `isError: true`.
- **Recovery**: The LLM sees the error and may try a different command.

### E5: Cancellation During Execution

- **Screen**: EXEC-14
- **User sees**:
  ```
    [Shell] Command cancelled.
  ```
  "[Shell]" in green (not treated as an error).
- **System response**: The per-execution CTS is cancelled.
  `OperationCanceledException` is caught (only when the session token is
  NOT cancelled). The execution window is stopped. A neutral result is
  added to the conversation.
- **Conversation impact**: `ToolResultBlock` with `"Command was cancelled
  by the user."` and `isError: false`.
- **Recovery**: The LLM knows the command was cancelled and may retry or
  adjust its approach.

## Screen Sequence

### Single tool call, success, with output > 5 lines:

1. EXEC-01 -- Tool call preview panel
2. EXEC-02 -- Waiting spinner
3. EXEC-03/04/05 -- Streaming output (filling then scrolling)
4. EXEC-06 -- Success result (collapsed, /expand available)

### Single tool call, success, with 1-3 output lines:

1. EXEC-01 -- Tool call preview panel
2. EXEC-02 -- Waiting spinner
3. EXEC-04 -- Streaming output (filling phase only)
4. EXEC-07 -- Success result (short, lines visible)

### Single tool call, no output:

1. EXEC-01 -- Tool call preview panel
2. EXEC-02 -- Waiting spinner (runs until execution completes)
3. EXEC-08 -- Success result (truncated text summary)

### Single tool call, error:

1. EXEC-01 -- Tool call preview panel
2. EXEC-02 -- Waiting spinner
3. EXEC-04/05 -- Error output streaming (if any)
4. EXEC-09/10/11 -- Error result

### Tool call cancelled by user:

1. EXEC-01 -- Tool call preview panel
2. EXEC-02 -- Waiting spinner
3. CANCEL-01 -- Cancel hint (on first Esc/Ctrl+C)
4. (second press within 1s)
5. EXEC-14 -- "Command cancelled." result

### Multiple tool calls:

1. EXEC-01 -- Preview panel (tool 1)
2. EXEC-02..05 -- Execute tool 1
3. EXEC-06..13 -- Result for tool 1
4. EXEC-01 -- Preview panel (tool 2)
5. EXEC-02..05 -- Execute tool 2
6. EXEC-06..13 -- Result for tool 2
7. (return to agent turn loop)

### Unknown tool name:

1. (No visual output)
2. EXEC-16 -- Error added to conversation silently
3. (continue to next tool call)

## Post-Execution: The `/expand` Command

After a tool call completes with > 5 output lines (collapsed result), the
user can type `/expand` to see the full output:

- **Screen**: EXPAND-01 (output shown), EXPAND-02 (nothing to expand),
  or EXPAND-03 (already expanded)
- **Behavior**: Each output line is printed with 2-space indent. The
  `_lastOutputExpanded` flag prevents double-expansion.
- **Limitation**: Only the most recent tool call's output is retained.
  If multiple tool calls occurred, only the last one can be expanded.
