# Flow: Cancellation

## Overview

The cancellation flow handles the user's intent to abort a long-running
operation (LLM streaming or tool execution) without terminating the entire
session. It uses a deliberate double-press pattern: the first press of
Esc or Ctrl+C shows a hint, and a second press within a 1-second window
performs the actual cancellation. This prevents accidental cancellation
while keeping the escape hatch easily accessible.

There are two independent cancellation systems that share the same UX
pattern: the `CancellationMonitor` (standalone, used in non-layout mode)
and the `AsyncInputReader`'s integrated cancellation (used in layout mode).
Both produce identical user-facing behavior.

## Preconditions

- An operation is in progress that supports cancellation:
  - Tool execution (a `BeginCancellationMonitor` scope is active)
  - Streaming LLM response (indirectly, via the same monitor)
- The user is NOT at the input prompt (cancellation at the input prompt is
  handled differently -- Esc clears the line buffer, Ctrl+C is intercepted
  but no monitor is active).

## Flow Diagram

```
    [Operation in progress]
    (tool executing or LLM streaming)
    [CancellationMonitor or AsyncInputReader scope active]
         |
         v
    [User presses Esc or Ctrl+C]
    (first press)
         |
         v
    [HandlePress / HandleCancelPress]
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
- **System response**: The `HandlePress` / `HandleCancelPress` method
  runs (depending on which system is active):
  1. Acquires the press/cancel lock.
  2. Checks `(now - _lastPressTime).TotalMilliseconds <= 1000`:
     - This is the first press (elapsed > 1000ms or no previous press).
  3. Records `_lastPressTime = DateTimeOffset.UtcNow`.
  4. Sets `shouldShowHint = true`.
  5. Disposes any existing reset timer.
  6. Creates a new `Timer` that fires after 1000ms to reset the state.
  7. Outside the lock, invokes `RenderCancelHint()` (via callback).

  **Layout mode specifics** (`AsyncInputReader.HandleCancelPress`):
  - The Esc key is intercepted in the `ProcessKey` method.
  - Ctrl+C is intercepted via the `Console.CancelKeyPress` event handler.
  - Callbacks are invoked outside the `_cancelLock` to prevent deadlock
    with the `_consoleLock`.

  **Non-layout mode specifics** (`CancellationMonitor.HandlePress`):
  - Ctrl+C is intercepted via `Console.CancelKeyPress` with
    `e.Cancel = true` to prevent process termination.
  - Esc is detected by a background polling task (`PollEscapeKeyAsync`)
    that checks `Console.KeyAvailable` every 50ms.
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
  1. `HandlePress` / `HandleCancelPress` detects the second press within
     the 1000ms window.
  2. The reset timer is disposed.
  3. `_lastPressTime` is reset to `DateTimeOffset.MinValue`.
  4. `shouldCancel = true` is set.
  5. Outside the lock, the `_onCancelRequested` callback is invoked,
     which calls `executionCts.Cancel()`.
  6. The `ExecuteAsync` call in `ProcessToolCallsAsync` throws
     `OperationCanceledException`.
  7. The catch block (guarded by `!ct.IsCancellationRequested` to
     distinguish per-execution cancellation from session cancellation):
     - Calls `_ui.RenderExecutingStop()`.
     - Renders the "Command cancelled." result.
     - Logs the cancellation.
     - Adds a `ToolResultBlock` to the conversation.
  8. The `CancellationScope` is disposed when the `using` block exits,
     which calls `DetachCancellation()` to clear all callbacks and
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
  1. Acquires the press/cancel lock.
  2. Resets `_lastPressTime = DateTimeOffset.MinValue`.
  3. Invokes `_onCancelHintCleared` callback (via the captured reference,
     outside the lock in the `AsyncInputReader` variant).
  4. `SpectreUserInterface.ClearCancelHint()`:
     - Sets `_cancelHintShowing = false`.
     - In non-layout mode: writes spaces to clear the hint line.
     - If `_isExecuting` is true: calls
       `_executionWindow.RestoreAfterCancelHint()`, which restarts the
       spinner if the execution window was in `Waiting` state.
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
via `BeginCancellationMonitor`). During pure LLM streaming without tool
calls, there is no cancellation monitor -- Esc/Ctrl+C have their normal
terminal behavior. For layout mode, Esc does nothing during streaming;
Ctrl+C is intercepted by the `AsyncInputReader` but without a cancel
scope active, it does not trigger cancellation.

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

### Layout Mode (`AsyncInputReader` Integration)

```
[Engine.ExecuteAsync called]
  |
  v
BeginCancellationMonitor(onCancelRequested)
  |
  v
CancellationScope created
  -> AttachCancellation(onCancel, onHint, onClear)
  -> Sets _onCancelRequested, _onCancelHintRequested, _onCancelHintCleared
  -> Resets _lastCancelPressTime
  |
  v
[Execution runs]
[Esc/Ctrl+C handled by AsyncInputReader.HandleCancelPress]
  |
  v
[Execution completes or is cancelled]
  |
  v
CancellationScope.Dispose()
  -> DetachCancellation()
  -> Disposes _cancelResetTimer
  -> Clears all callbacks
  -> Resets _lastCancelPressTime
```

The `AsyncInputReader` is always running (reading keys for the input
buffer). When a `CancellationScope` is attached, Esc/Ctrl+C presses are
routed through the cancel logic. When no scope is attached,
`_onCancelRequested` is null and presses are ignored by the cancel path.

### Non-Layout Mode (`CancellationMonitor` Standalone)

```
[Engine.ExecuteAsync called]
  |
  v
BeginCancellationMonitor(onCancelRequested)
  |
  v
CancellationMonitor created
  -> Subscribes to Console.CancelKeyPress
  -> Starts PollEscapeKeyAsync background task (50ms polling)
  |
  v
[Execution runs]
[Ctrl+C -> OnCancelKeyPress (e.Cancel = true)]
[Esc -> Detected by PollEscapeKeyAsync]
  |
  v
[Execution completes or is cancelled]
  |
  v
CancellationMonitor.Dispose()
  -> Unsubscribes from Console.CancelKeyPress
  -> Cancels and waits for poll task (200ms timeout)
  -> Disposes timer and CTS
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
|    |                     | No monitor attached | Press ignored (no _onCancelRequested) |
| D5 | Cancellation target | Per-execution CTS cancelled, session CTS not | Graceful: tool result added |
|    |                     | Session CTS cancelled | Session-level: propagates up, session ends |
| D6 | Layout mode | Layout active + AsyncInputReader | Integrated cancel via key reader |
|    |             | Non-layout | Standalone CancellationMonitor |

## Error Paths

### E1: Race Condition -- Timer Fires During Second Press

The timer callback and the second key press can race. Both paths acquire
the lock (`_pressLock` or `_cancelLock`):
- If the timer fires first: `_lastPressTime` is reset to
  `DateTimeOffset.MinValue`. The next press starts a new first-press cycle.
- If the second press wins: The timer is disposed before it fires. The
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

If the user presses Esc/Ctrl+C exactly while the `CancellationMonitor` is
being disposed:
- The `_disposed` flag is checked at the start of `HandlePress`. If true,
  the press is ignored.
- In the `AsyncInputReader` variant, `DetachCancellation` nulls the
  callbacks under the lock. A concurrent press will find
  `_onCancelRequested == null` and return.

### E4: Output Lines Arrive During Hint Display

If tool output arrives while the cancel hint is showing:
- `RenderOutputLine` checks `_cancelHintShowing`. If true, it clears the
  hint text (in non-layout mode, overwrites with spaces) and resets the
  flag.
- The output line is then processed normally by the execution window.
- This means the hint may be briefly visible and then overwritten by output.

### E5: Ctrl+C at Input Prompt (No Monitor Active)

When the user is at the input prompt with no operation running:
- **Layout mode**: Ctrl+C is intercepted by `AsyncInputReader.OnCancelKeyPress`
  with `e.Cancel = true`. Since `_onCancelRequested` is null (no scope),
  the handler returns without action. The terminal does not exit.
- **Non-layout mode**: There is no `CancellationMonitor` active. Ctrl+C
  would reach the default handler. In practice, the `Console.CancelKeyPress`
  event is only subscribed when a monitor is active, so it falls through
  to .NET's default behavior (which may throw `OperationCanceledException`
  caught by `ChatCommand`).

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
