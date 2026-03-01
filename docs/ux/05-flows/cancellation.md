# Flow: Cancellation

## Overview

The cancellation flow handles the user's intent to abort a long-running
operation (LLM streaming or tool execution) without terminating the entire
session. It uses a deliberate double-press pattern: the first press of
Esc or Ctrl+C shows a hint, and a second press within a 1-second window
performs the actual cancellation. This prevents accidental cancellation
while keeping the escape hatch easily accessible.

There are two independent cancellation systems that share the same UX
pattern: a standalone cancellation monitor (used in non-layout mode)
and the input handler's integrated cancellation (used in layout mode).
Both produce identical user-facing behavior.

## Preconditions

- An operation is in progress that supports cancellation:
  - Tool execution (a cancellation monitoring scope is active)
  - Streaming LLM response (indirectly, via the same monitor)
- The user is NOT at the input prompt (cancellation at the input prompt is
  handled differently -- Esc clears the line buffer, Ctrl+C is intercepted
  but no monitor is active).

## Flow Diagram

```
    [Operation in progress]
    (tool executing or LLM streaming)
    [Cancellation monitoring scope active]
         |
         v
    [User presses Esc or Ctrl+C]
    (first press)
         |
         v
    [Cancel handler invoked]
    Check time since last press
         |
    +----+----+
    |         |
    v         v
  [First    [Second press
  press]    within 1000ms]
    |         |
    v         |
  [Record    |
  timestamp] |
  [Start     |
  1s timer]  |
    |         |
    v         |
  CANCEL-01   |
  Hint shown  |
    |         |
    |         |
    |   [Wait for second press or timer]
    |         |
    |    +----+----+
    |    |         |
    |    v         v
    |  [Second   [Timer
    |  press     fires
    |  within    after
    |  1000ms]   1000ms]
    |    |         |
    |    v         v
    |  [Dispose  [Reset
    |  timer]    timestamp]
    |    |         |
    |    v         v
    |  [Invoke   CANCEL-02
    |  cancel    Hint
    |  callback] cleared
    |    |         |
    |    v         v
    |  [execution [Operation
    |  CTS        resumes
    |  cancelled] normally]
    |    |
    |    v
    |  OperationCanceledException
    |  caught in ProcessToolCallsAsync
    |    |
    |    v
    |  RenderExecutingStop()
    |    |
    |    v
    |  EXEC-14: "[Shell] Command cancelled."
    |  Tool result added to conversation
    |    |
    |    v
    |  [Continue session]
    |  (next tool call or next LLM round)
    |
    v
  [If during streaming: same pattern but
   cancels the streaming enumeration,
   StreamResponseAsync's finally block
   handles cleanup]
```

## Steps (Detailed)

### Step 1: First Press (Esc or Ctrl+C)

- **Screen**: CANCEL-01
- **User sees**: A hint line appears:
  ```
    Press Esc or Ctrl+C again to cancel
  ```
  The text is styled as `[dim italic yellow]`. Its placement depends on
  the execution state:
  - **During tool execution with spinner**: The spinner's "Executing..."
    text is cleared first via `ClearForCancelHint()`, then the hint is
    rendered on the same line (non-layout) or as a new output line
    (layout mode).
  - **During tool execution with streaming output**: Any residual spinner
    or progress text is cleared, and the hint appears below.
  - **During LLM streaming**: The hint appears below the streaming text.
- **User action**: Reads the hint and decides whether to press again.
- **System response**: The cancel handler runs:
  1. Acquires a thread-safety lock.
  2. Checks whether this is a first press (> 1000ms since last press or
     no previous press).
  3. Records the press timestamp.
  4. Starts a 1-second timer to reset the state.
  5. Shows the cancel hint to the user.

  **Layout mode specifics**:
  - The Esc key is intercepted by the input handler.
  - Ctrl+C is intercepted via the process cancel event.

  **Non-layout mode specifics**:
  - Ctrl+C is intercepted via the process cancel event (preventing
    process termination).
  - Esc is detected by a background polling task.
- **Transitions to**: Step 2a or Step 2b

### Step 2a: Second Press Within Window (Cancellation)

- **Screen**: Execution stops, EXEC-14 or streaming aborts
- **User sees**: The operation stops. Depending on context:
  - **Tool execution**: The spinner/output ceases. After a brief moment:
    ```
      [Shell] Command cancelled.
    ```
    "[Shell]" in green. Then the session continues (either the next tool
    call in the batch, or the next LLM round).
  - **LLM streaming**: Token output stops mid-stream. The
    `StreamResponseAsync` `finally` block clears the thinking indicator
    (if still showing) and calls `RenderStreamingComplete` (if text had
    started). The exception propagates to `RunAgentTurnAsync`'s catch
    block in `RunSessionAsync`, where the user message is removed from
    the conversation.
- **User action**: None -- the cancellation takes effect automatically.
- **System response**:
  1. The cancel handler detects the second press within the 1000ms window.
  2. The reset timer is disposed and the press timestamp is reset.
  3. The cancellation callback is invoked, cancelling the per-execution
     cancellation token.
  4. The command execution throws a cancellation exception.
  5. The exception is caught (distinguished from session-level
     cancellation):
     - The execution display is stopped.
     - The "Command cancelled." result is rendered.
     - The cancellation is logged.
     - A tool result is added to the conversation.
  6. The cancellation scope is disposed, clearing all callbacks and
     timers.
- **Transitions to**: Next tool call or next LLM round

### Step 2b: Timer Expires (No Cancellation)

- **Screen**: CANCEL-02 (hint cleared)
- **User sees**: The hint text disappears. The operation continues
  normally.
  - **Non-layout mode**: The hint line is overwritten with spaces:
    ```
    \r                                                  \r
    ```
  - **Layout mode**: The hint was written as scrolling output and does
    not need explicit clearing. However, if the execution window was in
    `Waiting` state (spinner), the spinner is restored via
    `RestoreAfterCancelHint()`.
- **User action**: None.
- **System response**: The timer callback fires after 1000ms:
  1. Acquires the thread-safety lock.
  2. Resets the press timestamp.
  3. Invokes the hint-cleared callback.
  4. The user interface clears the cancel hint:
     - In non-layout mode: writes spaces to clear the hint line.
     - If execution is in progress: restarts the spinner if it was in
       the waiting state.
- **Transitions to**: Operation continues normally

## Cancellation During Specific Operations

### During Tool Execution (Spinner Phase)

The most common cancellation scenario. The user sees the tool preview panel
and the spinner, decides the command is taking too long or is wrong, and
cancels.

```
+-- Shell --------------------------------+
| Get-ChildItem -Recurse C:\             |
+-----------------------------------------+
  . Executing... (5.2s)

  Press Esc or Ctrl+C again to cancel        <-- first press

(user presses Esc again)

  [Shell] Command cancelled.                 <-- result
```

The spinner is stopped by `ClearForCancelHint()` before the hint is shown,
then restarted by `RestoreAfterCancelHint()` if the timer expires without
a second press.

### During Tool Execution (Output Streaming Phase)

The execution is producing output. The user cancels because they see
unexpected or excessive output.

```
+-- Shell --------------------------------+
| npm install                              |
+-----------------------------------------+
  added 1,234 packages in 45s
  npm warn deprecated package@1.0...
  npm warn deprecated other@2.0...

  Press Esc or Ctrl+C again to cancel        <-- first press

(user presses Ctrl+C again)

  [Shell] Command cancelled.                 <-- result
```

The output lines may still be partially visible when the hint appears.
In non-layout mode, the hint overwrites the current cursor position. In
layout mode, the hint is written as a new output line.

### During LLM Streaming

The LLM is streaming tokens and the user wants to stop it (e.g., the
response is going in the wrong direction). Note: cancellation during LLM
streaming cancels the entire agent turn, not just the streaming. This is
because the streaming cancellation propagates as an
`OperationCanceledException` through `RunAgentTurnAsync`.

```
  The approach I would take is to refactor the entire module by first
  identifying all the dependencies and then creating new interfaces for

  Press Esc or Ctrl+C again to cancel        <-- first press

(user presses Esc again)

  (streaming stops, partial text remains in scrollback)
  Error: {provider-specific cancellation message}
    Suggestion: ...
```

The user message is removed from the conversation by the error handler
in `RunSessionAsync`, keeping conversation state consistent. The user
returns to the input prompt and can rephrase their message.

**Important**: The cancellation monitor during LLM streaming is only
active when tool execution is in progress (it is created per-tool-call
via the cancellation monitor). During pure LLM streaming without tool
calls, there is no cancellation monitor -- Esc/Ctrl+C have their normal
terminal behavior. For layout mode, Esc does nothing during streaming;
Ctrl+C is intercepted by the input handler but without a cancel scope
active, it does not trigger cancellation.

### During Tool Execution Cancellation with Multiple Tool Calls

When the LLM emitted multiple tool calls and the user cancels one:

```
Tool 1: Completes successfully
  [Shell] 15 lines | 2.3s

Tool 2: User cancels
+-- Shell --------------------------------+
| long-running-analysis                    |
+-----------------------------------------+
  . Executing... (8.1s)
  Press Esc or Ctrl+C again to cancel
  (second press)
  [Shell] Command cancelled.

Tool 3: Still executes (next in the batch)
+-- Shell --------------------------------+
| Get-Content result.json                  |
+-----------------------------------------+
  ...
```

Cancellation of one tool call does NOT cancel the remaining tool calls in
the batch. Each tool call has its own `CancellationTokenSource`. After the
cancelled tool's result is added to the conversation, the loop continues
to the next `ToolUseBlock`.

## The Cancellation Monitor Lifecycle

### Layout Mode (Input Handler Integration)

```
[Command execution starts]
  |
  v
[Cancellation monitoring scope created]
  -> Attaches cancel, hint, and clear callbacks to the input handler
  -> Resets press timestamp
  |
  v
[Execution runs]
[Esc/Ctrl+C handled by the input handler's cancel logic]
  |
  v
[Execution completes or is cancelled]
  |
  v
[Cancellation scope disposed]
  -> Detaches all callbacks
  -> Disposes reset timer
  -> Resets press timestamp
```

The input handler is always running (reading keys for the input
buffer). When a cancellation scope is attached, Esc/Ctrl+C presses are
routed through the cancel logic. When no scope is attached,
presses are ignored by the cancel path.

### Non-Layout Mode (Standalone Monitor)

```
[Command execution starts]
  |
  v
[Cancellation monitor created]
  -> Subscribes to the process cancel event
  -> Starts Esc key polling background task (50ms interval)
  |
  v
[Execution runs]
[Ctrl+C -> cancel event intercepted]
[Esc -> detected by polling task]
  |
  v
[Execution completes or is cancelled]
  |
  v
[Cancellation monitor disposed]
  -> Unsubscribes from the process cancel event
  -> Stops polling task (200ms timeout)
  -> Disposes timer and cancellation token
```

## Decision Points

| # | Decision Point | Condition | Outcome |
|---|---|---|---|
| D1 | Key press type | Esc key | Route to HandlePress/HandleCancelPress |
|    |                | Ctrl+C | Route to HandlePress via CancelKeyPress event |
|    |                | Any other key | Ignored by cancel handler |
| D2 | Press timing | First press (> 1000ms since last) | Show hint, start timer |
|    |              | Second press (<= 1000ms since last) | Execute cancellation |
| D3 | Timer expiry | Timer fires before second press | Clear hint, reset state |
|    |              | Second press before timer fires | Cancel, dispose timer |
| D4 | Cancel scope active | Monitor/scope attached | Cancel callbacks invoked |
|    |                     | No monitor attached | Press ignored |
| D5 | Cancellation target | Per-execution token cancelled, session token not | Graceful: tool result added |
|    |                     | Session token cancelled | Session-level: propagates up, session ends |
| D6 | Layout mode | Layout active with input handler | Integrated cancel via input handler |
|    |             | Non-layout | Standalone cancellation monitor |

## Error Paths

### E1: Race Condition -- Timer Fires During Second Press

The timer callback and the second key press can race. Both paths acquire
a thread-safety lock:
- If the timer fires first: the press timestamp is reset. The next press
  starts a new first-press cycle.
- If the second press wins: the timer is disposed before it fires. The
  cancellation proceeds.
- The lock ensures mutual exclusion; no double-cancel or double-clear can
  occur.

### E2: Rapid Triple Press

Pressing three times rapidly:
- Press 1: Shows hint, starts timer.
- Press 2 (within 1s): Cancels. Timer disposed. `_lastPressTime` reset.
- Press 3: No monitor is active (scope disposed after cancellation). The
  press is either ignored (layout mode, no `_onCancelRequested`) or has
  no effect (non-layout mode, monitor disposed).

### E3: Cancel During Monitor Disposal

If the user presses Esc/Ctrl+C exactly while the cancellation monitor is
being disposed:
- A disposed flag is checked at the start of the press handler. If true,
  the press is ignored.
- In the input handler variant, detaching cancellation nulls the
  callbacks under the lock. A concurrent press will find no cancel
  callback registered and return.

### E4: Output Lines Arrive During Hint Display

If tool output arrives while the cancel hint is showing:
- The output rendering checks whether the cancel hint is showing. If so,
  it clears the hint text (in non-layout mode, overwrites with spaces)
  and resets the flag.
- The output line is then processed normally by the execution display.
- This means the hint may be briefly visible and then overwritten by output.

### E5: Ctrl+C at Input Prompt (No Monitor Active)

When the user is at the input prompt with no operation running:
- **Layout mode**: Ctrl+C is intercepted by the input handler. Since no
  cancellation scope is attached, the handler returns without action.
  The terminal does not exit.
- **Non-layout mode**: There is no cancellation monitor active. Ctrl+C
  would reach the default handler. In practice, the process cancel event
  is only subscribed when a monitor is active, so it falls through to
  default behavior (which may throw a cancellation exception caught by
  the application).

## Screen Sequence

### Cancel during tool execution (full cycle):

1. EXEC-01 -- Tool preview panel
2. EXEC-02 -- Spinner running
3. CANCEL-01 -- Hint appears (first Esc/Ctrl+C)
4. (second press within 1s)
5. CANCEL-02 -- Hint cleared (implicit)
6. EXEC-14 -- "Command cancelled." result
7. LAYOUT-02 -- Input prompt (or next tool call)

### Cancel hint expires (no cancellation):

1. EXEC-01 -- Tool preview panel
2. EXEC-02 -- Spinner running
3. CANCEL-01 -- Hint appears (first Esc/Ctrl+C)
4. (1 second passes, no second press)
5. CANCEL-02 -- Hint cleared
6. EXEC-02 -- Spinner resumes (if was in Waiting state)
7. (execution continues normally)

### Cancel during output streaming:

1. EXEC-01 -- Tool preview panel
2. EXEC-02 -- Spinner
3. EXEC-04/05 -- Output lines streaming
4. CANCEL-01 -- Hint appears (first Esc/Ctrl+C)
5. (second press within 1s)
6. EXEC-14 -- "Command cancelled." result

## Timing Summary

| Event | Duration |
|---|---|
| Hint display after first press | < 50ms |
| Cancel window | 1000ms (1 second) |
| Hint auto-clear after timer | 1000ms from first press |
| Spinner stop on `ClearForCancelHint` | < 200ms (wait for spinner task) |
| Spinner restart on `RestoreAfterCancelHint` | Immediate (new task started) |
| Monitor dispose timeout | 200ms (best-effort wait for poll task) |
| Esc key polling interval | 50ms (non-layout mode only) |
| Key reader polling interval | 16ms (layout mode) |
