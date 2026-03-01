# Interaction Specifications

This document codifies every keyboard binding, animation timing, state machine,
and concurrency contract in the BoydCode terminal UI. It is the authoritative
reference for how the application responds to user input at the millisecond
level.

All file references are relative to `src/BoydCode.Presentation.Console/`.

---

## 1. Keyboard Shortcuts

### 1.1 Input Line Editing (Layout Mode)

Handled by `AsyncInputReader.ProcessKey()` in `Terminal/AsyncInputReader.cs`.

| Key | Action | Guard | Notes |
|---|---|---|---|
| Any printable char | Insert at cursor, advance cursor | `key.KeyChar != '\0' && !char.IsControl(key.KeyChar)` | Unicode-aware; no modifier filtering |
| Enter | Submit line to Channel, clear buffer, reset history index | Line must be non-empty to be submitted | Empty Enter is silently ignored (no submission) |
| Backspace | Delete character before cursor | `_cursorPosition > 0` | No-op at position 0 |
| Delete | Delete character at cursor | `_cursorPosition < _lineBuffer.Length` | No-op at end of buffer |
| Left Arrow | Move cursor left one position | `_cursorPosition > 0` | No-op at position 0 |
| Right Arrow | Move cursor right one position | `_cursorPosition < _lineBuffer.Length` | No-op at end of buffer |
| Home | Move cursor to position 0 | None | Always succeeds |
| End | Move cursor to end of buffer | None | Always succeeds |
| Up Arrow | Navigate to previous history entry | `_history.Count > 0` | Saves current buffer on first press; stops at oldest entry |
| Down Arrow | Navigate to next history entry | `_historyIndex != -1` | Restores saved buffer when past newest entry |
| Escape | Trigger cancellation handler | Cancel scope must be attached | See Section 4 (Cancellation) |
| Tab | Ignored | N/A | No tab completion implemented |
| Ctrl+C | Trigger cancellation handler (via `Console.CancelKeyPress`) | `e.Cancel = true` always set | Process termination is always suppressed |

### 1.2 Input Prompt (Fallback / Non-Layout Mode)

When the layout is not active, input is handled by Spectre.Console's
`TextPrompt<string>` via `SpectreUserInterface.GetUserInputAsync()`.

| Key | Action | Notes |
|---|---|---|
| Enter | Submit input | Empty input allowed (`.AllowEmpty()`) |
| Standard text editing | Handled by Spectre.Console | Backspace, arrow keys, Home, End |

### 1.3 Non-Interactive Terminal (Piped Input)

When `AnsiConsole.Profile.Capabilities.Interactive` is `false`:

| Input | Action | Notes |
|---|---|---|
| `Console.ReadLine()` | Read one line from stdin | Returns `null` on EOF |
| EOF (null) | Converted to `/quit` | Graceful session exit |

### 1.4 Vim-Style Key Remapping in Selection Prompts

Handled by `SpectreHelpers.VimConsoleInput.RemapVimKey()` in `SpectreHelpers.cs`.
Active only during `SelectionPrompt` and `MultiSelectionPrompt` interactions
(routed through the `VimAnsiConsole` wrapper).

| Key | Remapped To | Guard |
|---|---|---|
| j (no modifiers) | Down Arrow | `key.Modifiers == 0` |
| k (no modifiers) | Up Arrow | `key.Modifiers == 0` |
| All other keys | Pass through unchanged | N/A |

The remapping also supports pre-loading Down Arrow keys to set a default
selection index via `VimConsoleInput.PreloadDownArrows(int count)`.

### 1.5 Cancellation Keys

Active only when a `CancellationScope` (layout mode) or `CancellationMonitor`
(non-layout mode) is attached. See Section 4 for the full state machine.

| Key | Handler | Context |
|---|---|---|
| Escape | `AsyncInputReader.HandleCancelPress()` | Layout mode, during `ProcessKey` |
| Ctrl+C | `AsyncInputReader.OnCancelKeyPress()` | Layout mode, via `Console.CancelKeyPress` event |
| Escape | `CancellationMonitor.PollEscapeKeyAsync()` | Non-layout mode, polled every 50ms |
| Ctrl+C | `CancellationMonitor.OnCancelKeyPress()` | Non-layout mode, via `Console.CancelKeyPress` event |

### 1.6 Session Exit Commands

Handled by `AgentOrchestrator.RunSessionAsync()` in
`Application/Services/AgentOrchestrator.cs`. Matched case-insensitively
after trimming.

| Input | Action |
|---|---|
| `/quit` | Break session loop, auto-save, exit |
| `/exit` | Same as `/quit` |
| `quit` | Same as `/quit` (no slash required) |
| `exit` | Same as `/quit` (no slash required) |

### 1.7 Command History

Managed by `AsyncInputReader` in `Terminal/AsyncInputReader.cs`.

| Behavior | Detail |
|---|---|
| Max entries | 100 (`MaxHistorySize`) |
| Deduplication | Consecutive duplicate entries are not added |
| Eviction | Oldest entry removed when limit exceeded |
| Persistence | In-memory only; lost on session exit |
| Saved buffer | Current input is saved when entering history navigation and restored when navigating past the newest entry |

---

## 2. Animation Timing

### 2.1 Execution Spinner

| Parameter | Value | Source |
|---|---|---|
| Frame rate | 100ms per frame | `ExecutionWindow.RunSpinnerAsync`: `Task.Delay(100, ct)` |
| Frame count | 8 Braille pattern characters | `s_spinnerFrames` array |
| Frame sequence | U+283F U+283B U+283D U+283E U+2837 U+282F U+281F U+283E | Note: frame 7 duplicates frame 3 |
| Start trigger | `ExecutionWindow.Start()` called | Begins `Task.Run` for spinner |
| Stop trigger | First output line arrives, or `Stop()` called | `StopSpinner()` with 200ms wait timeout |
| Display format | `  {frame} Executing... ({elapsed})` | Elapsed updates each frame |

### 2.2 Elapsed Time Formatting

| Range | Format | Example |
|---|---|---|
| < 10 seconds | `{seconds:F1}s` | `3.2s` |
| 10 seconds to < 1 minute | `{seconds:F0}s` | `45s` |
| >= 1 minute | `{minutes}m {seconds}s` | `2m 15s` |

Source: `ExecutionWindow.FormatDuration()`.

### 2.3 Output Window Redraw Throttle

| Parameter | Value | Source |
|---|---|---|
| Minimum interval | 50ms between redraws | `ExecutionWindow.RedrawWindow`: `(now - _lastRedraw).TotalMilliseconds < 50` |
| Pending flag | `_redrawPending` set when throttled | Final redraw forced on `Stop()` with `bypassThrottle: true` |

### 2.4 Cancellation Timing

| Parameter | Value | Source |
|---|---|---|
| Cancel window | 1000ms (1 second) | `AsyncInputReader.CancelWindowMs`, `CancellationMonitor.HandlePress` |
| Hint auto-clear | 1000ms after first press | Timer callback in both implementations |
| Esc key poll interval | 50ms (non-layout mode only) | `CancellationMonitor.PollEscapeKeyAsync` |

### 2.5 Key Reader Polling

| Parameter | Value | Source |
|---|---|---|
| Polling interval | 16ms (~60 Hz) | `AsyncInputReader.PollingIntervalMs` |
| Scope | Layout mode only | `AsyncInputReader.ReadKeyLoopAsync` |

### 2.6 Spinner/Task Stop Timeouts

| Operation | Timeout | Source |
|---|---|---|
| Spinner task wait | 200ms | `ExecutionWindow.StopSpinner`: `_spinnerTask?.Wait(TimeSpan.FromMilliseconds(200))` |
| Input reader task wait | 200ms | `AsyncInputReader.Dispose`: `_readerTask?.Wait(TimeSpan.FromMilliseconds(200))` |
| Esc poll task wait | 200ms | `CancellationMonitor.Dispose`: `_pollTask.Wait(TimeSpan.FromMilliseconds(200))` |

---

## 3. State Machines

### 3.1 Terminal Layout

Managed by `TerminalLayout` in `Terminal/TerminalLayout.cs`.

```
                    CanUseLayout() == false
    [Inactive] ─────────────────────────────> [Inactive] (no-op)
         |
         | Activate() + CanUseLayout() == true
         v
      [Active]
         |              |
         | Suspend()    | Deactivate()
         v              v
    [Suspended]    [Inactive]
         |
         | Resume()
         v
      [Active]
```

**Transitions:**

| From | To | Trigger | Guard | Side Effects |
|---|---|---|---|---|
| Inactive | Active | `Activate()` | `CanUseLayout()` returns true | Sets `Current`, enables VT processing, establishes scroll region |
| Inactive | Inactive | `Activate()` | `CanUseLayout()` returns false | `_useLayout = false`; all writes pass through to raw console |
| Active | Suspended | `Suspend()` | `_isActive && !_isSuspended && _useLayout` | Resets scroll region, moves cursor to bottom |
| Suspended | Active | `Resume()` | `_isActive && _isSuspended && _useLayout` | Recaptures terminal size, re-establishes layout |
| Active | Inactive | `Deactivate()` | `_isActive` | Resets scroll region, clears screen, clears `Current` |
| Any | Inactive | `Dispose()` | `!_disposed` | Calls `Deactivate()` |

**`CanUseLayout()` conditions:**

- `Console.IsOutputRedirected == false`
- `Console.IsInputRedirected == false`
- `Console.WindowHeight >= 10` (`MinTerminalHeight`)

### 3.2 Execution Window

Managed by `ExecutionWindow` in `Terminal/ExecutionWindow.cs`.

```
    [Inactive]
         |
         | Start(useContainedOutput)
         v
     [Waiting]  ──────────────> [Inactive]
         |                          ^
         | AddOutputLine()          | Stop()
         v                          |
    [Streaming] ────────────────────'
```

**Transitions:**

| From | To | Trigger | Side Effects |
|---|---|---|---|
| Inactive | Waiting | `Start()` | Clears buffer, restarts stopwatch, starts spinner task |
| Waiting | Streaming | `AddOutputLine()` | Stops spinner, processes output line |
| Waiting | Inactive | `Stop()` | Stops spinner, stops stopwatch, clears residual text |
| Streaming | Inactive | `Stop()` | Stops stopwatch, forces final redraw if pending |

### 3.3 Cancellation Monitor

Two parallel implementations share the same logical state machine.

**Layout mode** (`AsyncInputReader` with `CancellationScope`):

```
    [Idle]
      |
      | Esc or Ctrl+C pressed
      v
    [HintShown]
      |                |
      | 2nd press      | Timer fires (1000ms)
      | <= 1000ms      |
      v                v
    [Cancelled]     [Idle]
```

**Non-layout mode** (`CancellationMonitor`):

Same state machine, different implementation. Uses `Console.CancelKeyPress`
event handler and a background Esc polling task.

**Transitions:**

| From | To | Trigger | Guard | Side Effects |
|---|---|---|---|---|
| Idle | HintShown | First Esc/Ctrl+C | `(now - _lastPressTime) > 1000ms` or no previous press | Records timestamp, starts 1s reset timer, invokes hint callback |
| HintShown | Cancelled | Second Esc/Ctrl+C | `(now - _lastPressTime) <= 1000ms` | Disposes timer, resets timestamp, invokes cancel callback |
| HintShown | Idle | Timer fires | 1000ms elapsed since first press | Resets timestamp, invokes clear callback |

**Lifecycle:**

- Layout mode: `CancellationScope` created per tool execution via
  `BeginCancellationMonitor()`. Attaches callbacks on construction,
  detaches on dispose. The `AsyncInputReader` continues running between
  scopes; key presses without an attached scope are ignored.
- Non-layout mode: `CancellationMonitor` created per tool execution.
  Subscribes to `Console.CancelKeyPress` and starts Esc polling on
  construction. Unsubscribes and stops polling on dispose.

### 3.4 Agent Turn

Managed by `AgentOrchestrator.RunAgentTurnAsync()` in
`Application/Services/AgentOrchestrator.cs`.

```
    [Idle]
      |
      | User submits message
      | SetAgentBusy(true)
      v
    [Busy]
      |
      | CompactIfNeeded
      | Build LlmRequest
      v
    [Streaming]  (or non-streaming SendAsync)
      |
      | Response received
      |
      +──> HasToolUse == false ──> [Idle]
      |                            (SetAgentBusy(false), auto-save)
      v
    [ToolExecution]
      |
      | For each ToolUseBlock:
      |   RenderToolExecution (panel)
      |   RenderExecutingStart (spinner)
      |   ExecuteAsync (with CancellationMonitor)
      |   RenderExecutingStop
      |   RenderToolResult
      |
      | All tool results added to conversation
      v
    [Streaming]  (next round, up to 50 rounds)
```

**Max rounds:** 50 (`maxToolRounds` constant). Exceeding this renders an
error and stops.

### 3.5 Input Reader

Managed by `AsyncInputReader` in `Terminal/AsyncInputReader.cs`.

```
    [Reading]
      |
      | User presses Enter (non-empty buffer)
      v
    [Submitting]
      |
      | Line written to Channel
      | Buffer cleared, history updated
      v
    [Reading]
```

This is a continuous loop. The reader runs on a background task and never
blocks the main thread. Submitted lines are consumed by
`SpectreUserInterface.GetUserInputAsync()` via `ReadLineAsync()`.

---

## 4. Layout Suspension Protocol

### 4.1 When Suspension Occurs

The split-pane layout must be suspended whenever Spectre.Console needs full
terminal control for interactive prompts. This is because Spectre's prompt
widgets write directly to the console and expect to own the cursor position
and screen state.

### 4.2 How Suspension Works

1. **SpectreHelpers wraps every prompt** in `SuspendLayout()` / `ResumeLayout()`:
   ```
   SuspendLayout()    // TerminalLayout.Current?.Suspend()
   try {
     // Spectre prompt runs with full terminal access
     return AnsiConsole.Prompt(...)
   } finally {
     ResumeLayout()   // TerminalLayout.Current?.Resume()
   }
   ```

2. **`Suspend()`** (in `TerminalLayout.cs`):
   - Acquires `_consoleLock`
   - Sets `_isSuspended = true`
   - Resets the scroll region (`\x1b[r`)
   - Moves cursor to the bottom of the screen

3. **`Resume()`** (in `TerminalLayout.cs`):
   - Acquires `_consoleLock`
   - Recaptures terminal size (may have changed during prompt)
   - Re-establishes the scroll region and redraws separator, input, status
   - Sets `_isSuspended = false`

### 4.3 Affected Prompts

Every prompt helper in `SpectreHelpers` performs suspension:

| Method | Prompt Type |
|---|---|
| `PromptNonEmpty` | `TextPrompt<string>` with validation |
| `PromptOptional` | `TextPrompt<string>` with `.AllowEmpty()` |
| `PromptSecret` | `TextPrompt<string>` with `.Secret()` |
| `PromptWithDefault` | `TextPrompt<string>` with `.DefaultValue()` |
| `Ask<T>` | `AnsiConsole.Ask<T>` |
| `Select` | `SelectionPrompt<string>` |
| `Select<T>` | `SelectionPrompt<T>` |
| `MultiSelect<T>` | `MultiSelectionPrompt<T>` |
| `Confirm` | `AnsiConsole.Confirm` |

### 4.4 Prompts Not in SpectreHelpers

Two prompts in `LoginCommand.cs` use raw `AnsiConsole.Prompt()` without
suspension:
- Client Secret prompt (line 142)
- GCP Location prompt (line 156)

These do not cause issues in practice because `LoginCommand` runs before
the layout is activated (it is a separate CLI command, not a slash command).

### 4.5 Write Behavior During Suspension

When the layout is suspended, all `TerminalLayout` write methods
(`WriteToOutput`, `WriteLineToOutput`, `AppendToOutput`, `WriteRenderable`,
`WriteMarkupLine`) fall through to direct console/AnsiConsole writes. This
ensures output from within suspended prompts is not corrupted.

---

## 5. Resize Handling

### 5.1 Detection Mechanism

Terminal resize is detected lazily by `TerminalLayout.CheckForResize()`,
which is called at the beginning of every write operation (`WriteToOutput`,
`WriteLineToOutput`, `AppendToOutput`, `WriteRenderable`, `WriteMarkupLine`).

```csharp
var currentHeight = System.Console.WindowHeight;
var currentWidth = System.Console.WindowWidth;
if (currentHeight != _lastHeight || currentWidth != _lastWidth)
{
    _lastHeight = currentHeight;
    _lastWidth = currentWidth;
    RefreshLayout();
}
```

There is no signal-based resize handler (`SIGWINCH`). Resize is detected on
the next write.

### 5.2 What Happens on Resize

`RefreshLayout()` calls `EstablishLayout(clearScreen: true)`, which:

1. Clears the entire screen (`\x1b[2J\x1b[H`)
2. Recalculates row positions:
   - Scroll region: rows 1 through `height - 3`
   - Separator: row `height - 2`
   - Input line: row `height - 1`
   - Status line: row `height`
3. Sets the new scroll region (`\x1b[1;{scrollBottom}r`)
4. Redraws the separator, input prompt, and status line
5. Positions cursor at the top of the output area

### 5.3 Content Loss on Resize

Resizing clears the screen. Output that was in the scroll region before the
resize is lost from the visible area. Scrollback buffer behavior depends on
the terminal emulator:
- **Windows Terminal**: Scrollback preserved
- **Legacy conhost**: Scrollback may be partially lost
- **Most Linux/macOS terminals**: Scrollback preserved

### 5.4 Resize During Specific States

| State | Behavior |
|---|---|
| Idle (at input prompt) | Resize detected on next write; prompt redrawn |
| Streaming LLM tokens | Resize detected on next `AppendToOutput`; layout refreshed, stream continues |
| Tool execution (spinner) | Resize detected on next spinner frame write; layout refreshed |
| Tool execution (output) | Resize detected on next output line write; layout refreshed |
| Suspended (Spectre prompt) | No detection; Spectre.Console handles its own terminal interaction. Resize is detected on `Resume()` via `CaptureTerminalSize()` |
| Non-layout mode | No resize handling; terminal handles scrolling natively |

### 5.5 Minimum Terminal Height

If the terminal height drops below 10 rows (`MinTerminalHeight`):
- `CanUseLayout()` returns false
- If layout was already active: `EstablishLayout` returns early (no-op)
- The layout effectively degrades but does not deactivate mid-session

---

## 6. Thread Safety

### 6.1 ConsoleLock

`TerminalLayout._consoleLock` is the primary synchronization primitive. It
is exposed as `TerminalLayout.ConsoleLock` and shared with `ExecutionWindow`.

**Operations that acquire ConsoleLock:**

| Operation | Acquirer | Source |
|---|---|---|
| Layout activation/deactivation/suspend/resume | `TerminalLayout` | `Activate()`, `Deactivate()`, `Suspend()`, `Resume()` |
| All output writes | `TerminalLayout` | `WriteToOutput`, `WriteLineToOutput`, `AppendToOutput`, `WriteRenderable`, `WriteMarkupLine` |
| Input line update | `TerminalLayout` | `UpdateInputLine` |
| Status line update | `TerminalLayout` | `UpdateStatusLine` |
| Execution window start/stop | `ExecutionWindow` | `Start()`, `Stop()` |
| Output line addition | `ExecutionWindow` | `AddOutputLine()` |
| Tool result rendering | `ExecutionWindow` | `RenderToolResult()` |
| Spinner frame rendering | `ExecutionWindow` | `RunSpinnerAsync()` (per frame) |
| Cancel hint rendering | `SpectreUserInterface` | `RenderCancelHint()` |
| Cancel hint clearing | `SpectreUserInterface` | `ClearCancelHint()` |
| Executing start/stop | `SpectreUserInterface` | `RenderExecutingStart()`, `RenderExecutingStop()` |
| Output line rendering | `SpectreUserInterface` | `RenderOutputLine()` |
| Tool result rendering | `SpectreUserInterface` | `RenderToolResult()` |

### 6.2 Cancel Lock

A separate lock (`AsyncInputReader._cancelLock` or
`CancellationMonitor._pressLock`) guards the cancellation state machine to
prevent race conditions between key presses and the timer callback.

**Critical ordering:** Cancel callbacks are invoked **outside** the cancel
lock to prevent deadlock with `_consoleLock`. The pattern is:

```csharp
lock (_cancelLock)
{
    // Determine action (shouldShowHint or shouldCancel)
}
// Invoke callbacks outside the lock
if (shouldShowHint) { hintCallback?.Invoke(); }
if (shouldCancel) { cancelCallback?.Invoke(); }
```

### 6.3 Thread-Safe Operations

| Operation | Thread Safety Mechanism |
|---|---|
| `Channel<string>` reads/writes | `Channel` is inherently thread-safe |
| `_outputCursorSaved` flag | Only accessed under `_consoleLock` |
| `_streamingStarted`, `_isThinking`, `_isExecuting`, `_cancelHintShowing` | Accessed from the main thread and from `RenderCancelHint`/`ClearCancelHint` (under `_consoleLock`) |

### 6.4 Not Thread-Safe (By Design)

| Operation | Notes |
|---|---|
| `_lineBuffer`, `_cursorPosition`, `_historyIndex` | Only accessed from the key reader task; single-writer |
| `_statusText` | Written by main thread, read by `DrawStatusLine` under lock; simple string assignment is atomic |
| `_agentBusy`, `_queueCount` | Written by main thread, read under lock during input display; race is benign (cosmetic only) |

### 6.5 Potential Deadlock Paths

The only identified deadlock risk is invoking a callback that acquires
`_consoleLock` while holding `_cancelLock`. Both implementations
(AsyncInputReader and CancellationMonitor) prevent this by extracting the
callback reference under the cancel lock and invoking it after releasing
the lock.

---

## 7. Output Routing

### 7.1 Layout Mode Output Flow

When the terminal layout is active, all output must flow through
`TerminalLayout` to maintain scroll region integrity:

```
Application code
    |
    v
SpectreUserInterface method (e.g., RenderAssistantText)
    |
    v
TerminalLayout method (WriteToOutput / WriteLineToOutput / WriteRenderable / etc.)
    |
    +-- Acquires _consoleLock
    +-- Moves cursor to scroll region bottom
    +-- Writes content
    +-- Repositions cursor to input line
    +-- Releases _consoleLock
```

### 7.2 Non-Layout Mode Output Flow

When the layout is not active, output goes directly to the console:

```
Application code
    |
    v
SpectreUserInterface method
    |
    +-- System.Console.Write/WriteLine (plain text)
    +-- AnsiConsole.MarkupLine (Spectre markup)
    +-- AnsiConsole.Write(IRenderable) (Spectre renderables)
```

### 7.3 Error Output

Errors rendered via `SpectreUserInterface.RenderError()` always go to stderr
via the `_stderr` `IAnsiConsole` instance, regardless of layout mode.

Errors rendered via `SpectreHelpers.Error()` go to stdout via
`AnsiConsole.MarkupLine`. This is a known inconsistency (see
`06-style-tokens.md` Section 11.2).

---

## 8. Queue Count Display

### 8.1 When Visible

The message queue count indicator `[N messages queued]` appears on the input
line only when:
- Layout mode is active
- The agent is busy (`_agentBusy == true`)
- There are pending messages (`_queueCount > 0`)
- There is sufficient terminal width for both the input text and the label

### 8.2 Rendering

The label is positioned right-aligned on the input row using dim ANSI styling
(`DimOn`/`DimOff`). It is redrawn as part of `UpdateInputLine()`.

### 8.3 Update Frequency

The queue count is updated:
- When `SetAgentBusy(true)` is called (start of agent turn)
- When `ReadLineAsync` is called (before reading a line from the channel)
- When `UpdateQueueCount` is explicitly called

The count is not updated on every key press to avoid flicker.
