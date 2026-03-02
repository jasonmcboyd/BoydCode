# Interaction Specifications

This document codifies every keyboard binding, animation timing, state machine,
and concurrency contract in the BoydCode terminal UI. It is the authoritative
reference for how the application responds to user input at the millisecond
level.

The UI uses a **persistent always-on TUI** architecture:
- The application starts once at session begin and stays active for the entire
  session. All content accumulates in a scrollable conversation view. The
  five-view hierarchy (ConversationView, ActivityBar, InputView, Separator, StatusBar)
  is always visible; there is no mode switching between idle and active states.
- **Between turns**: Idle state. The activity bar shows a dim Rule. Input via
  the input view at a `> _` prompt. The user can scroll through conversation
  history.
- **During active turns**: Views update in real-time. The activity bar shows a
  spinner with state label. Streaming tokens and tool output appear in the
  conversation view. The input view accepts typeahead.

---

## 1. Keyboard Shortcuts

### 1.1 Input Line Editing (Between Turns)

Handled by the input view's key processing logic.
Active between turns when the application is in idle state.

| Key | Action | Guard | Notes |
|---|---|---|---|
| Any printable char | Insert at cursor, advance cursor | Non-control, non-null character | Unicode-aware; no modifier filtering |
| Enter | Submit line to message queue, clear buffer, reset history index | Line must be non-empty to be submitted | Empty Enter is silently ignored (no submission) |
| Backspace | Delete character before cursor | Cursor position > 0 | No-op at position 0 |
| Delete | Delete character at cursor | Cursor position < buffer length | No-op at end of buffer |
| Left Arrow | Move cursor left one position | Cursor position > 0 | No-op at position 0 |
| Right Arrow | Move cursor right one position | Cursor position < buffer length | No-op at end of buffer |
| Ctrl+Left Arrow | Move cursor to previous word boundary | Cursor position > 0 | Skips whitespace then word chars |
| Ctrl+Right Arrow | Move cursor to next word boundary | Cursor position < buffer length | Skips word chars then whitespace |
| Ctrl+Backspace | Delete from cursor to previous word boundary | Cursor position > 0 | Removes whitespace then word chars |
| Ctrl+Delete | Delete from cursor to next word boundary | Cursor position < buffer length | Removes word chars then whitespace |
| Home | Move cursor to position 0 | None | Always succeeds |
| End | Move cursor to end of buffer | None | Always succeeds |
| Up Arrow | Navigate to previous history entry | History count > 0 | Saves current buffer on first press; stops at oldest entry |
| Down Arrow | Navigate to next history entry | Currently navigating history | Restores saved buffer when past newest entry |
| Escape | No effect (agent is not active between turns) | N/A | Ignored when no cancel scope attached |
| Tab | Ignored | N/A | No tab completion implemented |
| Ctrl+C | No effect (agent is not active between turns) | Cancellation always suppressed | Process termination is always suppressed |

### 1.2 During Active Turns

During active turns, the input view provides typeahead echo. The input
handler's key loop runs continuously and routes display to the input view.
Typed text is queued and submitted on the next idle prompt. Cancellation keys
are handled as below:

| Key | Action | Notes |
|---|---|---|
| Escape | Trigger cancellation handler | See Section 3.3 (Cancellation) |
| Ctrl+C | Trigger cancellation handler (via cancel key press event) | Cancellation always suppressed |
| Printable chars | Inserted into line buffer, visible in input view | Typeahead -- queued for next turn |
| Enter | Submits line to message queue | `[N queued]` badge shown in input view |
| Other editing keys | Normal line editing (Backspace, Delete, arrows, Home, End) | Visible in input view |

### 1.3 Input Prompt (Fallback / Non-Layout Mode)

When the terminal height is < 10 rows or output is piped, input is handled by
a blocking text prompt via the user interface.

| Key | Action | Notes |
|---|---|---|
| Enter | Submit input | Empty input allowed |
| Standard text editing | Handled by the prompt widget | Backspace, arrow keys, Home, End |

### 1.4 Non-Interactive Terminal (Piped Input)

When the terminal is not interactive (piped input):

| Input | Action | Notes |
|---|---|---|
| Line read from stdin | Read one line | Returns null on EOF |
| EOF (null) | Converted to `/quit` | Graceful session exit |

### 1.5 Vim-Style Key Remapping in Selection Prompts

Active only during selection prompt interactions (routed through a Vim-aware
input wrapper).

| Key | Remapped To | Guard |
|---|---|---|
| j (no modifiers) | Down Arrow | No modifiers held |
| k (no modifiers) | Up Arrow | No modifiers held |
| All other keys | Pass through unchanged | N/A |

The remapping also supports pre-loading Down Arrow keys to set a default
selection index.

### 1.6 Cancellation Keys

Active only when the agent is busy (during active turns, or during non-layout
mode execution). See Section 3.3 for the full state machine.

| Key | Handler | Context |
|---|---|---|
| Escape | Cancel press handler | During active turn, via key event |
| Ctrl+C | Cancel key press handler | During active turn, via system cancel event |
| Escape | Escape key polling | Non-layout mode, polled every 50ms |
| Ctrl+C | Cancel key press handler | Non-layout mode, via system cancel event |

### 1.7 Session Exit Commands

Handled by the session loop in the orchestrator. Matched case-insensitively
after trimming.

| Input | Action |
|---|---|
| `/quit` | Break session loop, auto-save, exit |
| `/exit` | Same as `/quit` |
| `quit` | Same as `/quit` (no slash required) |
| `exit` | Same as `/quit` (no slash required) |

### 1.8 List Navigation (Interactive List Windows)

Active when an Interactive List window (pattern #28) is open and the
`ListView` has focus. These keys enable keyboard-driven navigation and
actions on list items.

| Key | Action | Notes |
|---|---|---|
| Up Arrow / k | Move selection up one row | Wraps to bottom if at top |
| Down Arrow / j | Move selection down one row | Wraps to top if at bottom |
| Enter | Primary action on selected row | Open, show, or expand |
| Home | Jump to first item | |
| End | Jump to last item | |
| Page Up | Scroll list up one viewport | |
| Page Down | Scroll list down one viewport | |
| e | Edit selected item | Opens Form Dialog (#31) or Edit Menu Loop |
| d | Delete selected item | Opens Delete Confirmation (#15) |
| n | New / Create | Opens Form Dialog (#31) or Multi-Step Wizard (#32) |
| r | Rename selected item | Opens rename prompt (context-dependent) |
| s | Setup (provider context) | Opens Multi-Step Wizard (#32) |
| / | Focus search/filter field | If present (pattern #30); not all lists have filters |
| Esc | Dismiss the list window | Closes window, returns focus to conversation |
| Type-ahead | Jump to first matching item | Letters typed quickly match against the primary column; resets after 500ms of inactivity |

Single-letter hotkeys are handled in the window's `OnKeyDown` override.
They fire only when the `ListView` has focus (not when a sub-dialog is open).
See pattern #28 (Interactive List) and pattern #29 (Action Bar).

### 1.9 Dialog Navigation (Form Dialogs and Wizards)

Active when a Form Dialog (pattern #31) or Multi-Step Wizard (pattern #32)
is open. These keys navigate between fields and buttons within the dialog.

| Key | Action | Notes |
|---|---|---|
| Tab | Move to next field or button | Follows visual top-to-bottom, left-to-right order |
| Shift+Tab | Move to previous field or button | Reverse of Tab order |
| Enter | Confirm / Submit | When Ok/Create/Done button is focused, or in single-field dialogs |
| Esc | Cancel and close dialog | Returns to conversation view; no changes saved |
| Ctrl+A | Select all text in focused TextField | Standard text selection |

**Multi-Step Wizard additional keys (pattern #32):**

| Key | Action | Notes |
|---|---|---|
| Alt+B | Back to previous step | Same as clicking Back button; disabled on step 1 |
| Alt+N | Next step / Done | Same as clicking Next/Done button; validates before advancing |

**Selection lists within dialogs:**

| Key | Action | Notes |
|---|---|---|
| Up Arrow / k | Move selection up | Standard list navigation |
| Down Arrow / j | Move selection down | Standard list navigation |
| Enter | Confirm selection | Selects the highlighted item |

### 1.10 Command History

Managed by the input handler.

| Behavior | Detail |
|---|---|
| Max entries | 100 |
| Deduplication | Consecutive duplicate entries are not added |
| Eviction | Oldest entry removed when limit exceeded |
| Persistence | In-memory only; lost on session exit |
| Saved buffer | Current input is saved when entering history navigation and restored when navigating past the newest entry |

---

## 2. Animation Timing

### 2.1 Activity Spinner (All Active States)

The braille spinner appears in the **activity bar** during ALL busy states
(Thinking, Streaming, Executing). It uses 10 braille frames cycling at 100ms
per frame.

| Parameter | Value |
|---|---|
| Frame rate | 100ms per frame |
| Frame count | 10 braille characters |
| Frame sequence | ⠋ ⠙ ⠹ ⠸ ⠼ ⠴ ⠦ ⠧ ⠇ ⠏ |
| Active states | Thinking, Streaming, Executing |
| Display format (Thinking) | `{frame} Thinking...` (yellow) |
| Display format (Streaming) | `{frame} Streaming...` (cyan) |
| Display format (Executing) | `{frame} Executing... ({elapsed})` (cyan) |

### 2.2 Elapsed Time Formatting

| Range | Format | Example |
|---|---|---|
| < 10 seconds | One decimal place + `s` | `3.2s` |
| 10 seconds to < 1 minute | Whole seconds + `s` | `45s` |
| >= 1 minute | Minutes and seconds | `2m 15s` |

### 2.3 Streaming Refresh Throttle

| Parameter | Value |
|---|---|
| Minimum interval | 16ms between refreshes (~60fps) |
| Context | During streaming phase only (spinner phases use 100ms interval) |

### 2.4 Cancellation Timing

| Parameter | Value |
|---|---|
| Cancel window | 1000ms (1 second) |
| Hint auto-clear | 1000ms after first press |
| Esc key poll interval | 50ms (non-layout mode only) |

### 2.5 Key Reader Polling

| Parameter | Value |
|---|---|
| Polling interval | 16ms (~60 Hz) |
| Scope | Between turns AND during active turns |

### 2.6 Spinner/Task Stop Timeouts

| Operation | Timeout |
|---|---|
| Spinner task wait | 200ms |
| Input reader task wait | 200ms |
| Esc poll task wait | 200ms |

### 2.7 Input Line Horizontal Scrolling

When the text in the line buffer exceeds the available input width, the rendered
input line displays a scrolling viewport.

| Parameter | Value | Notes |
|---|---|---|
| Available width | Terminal width minus `> ` prefix (2 chars) | Between turns only |
| Viewport width | Available width minus scroll indicator chars | 1 char reserved per visible arrow indicator |
| Left indicator | `<-` dim | Shown when viewport start > 0 |
| Right indicator | `->` dim | Shown when viewport end < buffer length |
| Viewport tracking | Cursor always kept within the visible window | On cursor move, viewport shifts to include cursor |
| Home / End | Viewport snaps to buffer start / end | Left indicator hidden at start; right hidden at end |

The scroll indicators use dim styling and consume 1 character each on the
input row. Their presence reduces the visible text window rather than
overwriting text characters.

---

## 3. State Machines

### 3.1 Turn Lifecycle

The core state machine of the persistent always-on TUI architecture.

```
    [Idle]                       (application active, activity bar shows dim Rule)
      |
      | User submits message (Enter)
      v
    [UserMessageRendered]        (Panel with grey23 background in conversation view)
      |
      | Message added to conversation model
      v
    [BeginTurn]                  (activity bar set to Thinking, agent-busy flag set)
      |
      | Activity: "{spinner} Thinking..." (yellow)
      v
    [AgentBusy]                  (views updating in real-time)
      |
      +---> Streaming: Activity bar shows "{spinner} Streaming..." (cyan)
      |     Conversation view shows growing text
      |
      +---> Tool call: Activity bar shows "{spinner} Executing... (Ns)" (cyan)
      |     Conversation view shows tool badge
      |     (may loop back to Streaming for next round)
      |
      +---> Cancel: Activity bar shows "Press Esc again to cancel" (yellow)
      |
      +---> end_turn or max rounds
      v
    [EndTurn]                    (activity bar set to Idle, agent-busy flag cleared)
      |
      | All turn content already in scroll buffer -- no flush needed
      v
    [Idle]                       (application still active, input view ready)
```

### 3.2 Execution Window

The execution window tracks the state of individual tool executions within an
agent turn.

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
| Inactive | Waiting | Start | Clears buffer, restarts stopwatch, starts spinner task |
| Waiting | Streaming | AddOutputLine | Stops spinner, processes output line |
| Waiting | Inactive | Stop | Stops spinner, stops stopwatch, clears residual text |
| Streaming | Inactive | Stop | Stops stopwatch, forces final redraw if pending |

### 3.3 Cancellation Monitor

Two parallel implementations share the same logical state machine.

**During active turn** (input handler with cancellation scope):

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

**Non-layout mode** (cancellation monitor):

Same state machine, different implementation. Uses the system cancel key press
event handler and a background Esc polling task.

**Transitions:**

| From | To | Trigger | Guard | Side Effects |
|---|---|---|---|---|
| Idle | HintShown | First Esc/Ctrl+C | Elapsed since last press > 1000ms, or no previous press | Records timestamp, starts 1s reset timer, invokes hint callback |
| HintShown | Cancelled | Second Esc/Ctrl+C | Elapsed since last press <= 1000ms | Disposes timer, resets timestamp, invokes cancel callback |
| HintShown | Idle | Timer fires | 1000ms elapsed since first press | Resets timestamp, invokes clear callback |

**Lifecycle:**

- During active turns: A cancellation scope is created per tool execution.
  Attaches callbacks on construction, detaches on dispose. The input handler
  continues running between scopes; key presses without an attached scope are
  ignored.
- Non-layout mode: A cancellation monitor is created per tool execution.
  Subscribes to the system cancel event and starts Esc polling on construction.
  Unsubscribes and stops polling on dispose.

### 3.4 Agent Turn

Managed by the orchestrator's agent turn logic.

```
    [Idle]                          (between turns, application active)
      |
      | User submits message
      | User message rendered in conversation view
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
      +──> HasToolUse == false ──> [TurnComplete]
      |                            (activity bar set to Idle)
      v
    [ToolExecution]
      |
      | For each ToolUseBlock:
      |   Render tool badge (in conversation view)
      |   Set activity bar to Executing (with spinner + elapsed time)
      |   Execute command (with cancellation monitor)
      |   Set activity bar to Streaming
      |   Render tool result badge (in conversation view)
      |
      | All tool results added to conversation
      v
    [Streaming]  (next round, up to 50 rounds)
```

**Max rounds:** 50 (`maxToolRounds` constant). Exceeding this renders an
error and stops.

### 3.5 Input Reader

Managed by the input handler.

```
    [Reading]                   (between turns, input view active)
      |
      | User presses Enter (non-empty buffer)
      v
    [Submitting]
      |
      | Line written to message queue
      | Buffer cleared, history updated
      v
    [Reading]
```

This is a continuous loop. The reader runs on a background task and never
blocks the main thread. Submitted lines are consumed by the user interface's
input reading method.

During active turns, the reader continues running. Typed text is visible in
the input view (dim styling, with `[N queued]` badge for submitted lines).
Esc/Ctrl+C trigger the cancellation state machine.

### 3.6 Application Lifecycle

The TUI application starts once and remains active for the entire session.
There is no per-turn activation or deactivation of the application itself.

```
    [NotStarted]                (before session initialization)
      |
      | Application starts, view hierarchy created
      | (ConversationView + ActivityBar + InputView + Separator + StatusBar)
      v
    [Active]                    (render loop running, views accepting updates)
      |              |
      | Session      | Dialog opened (interactive slash command)
      | ends         | Dialog takes focus, blocks input to conversation
      v              v
    [Shutdown]      [DialogActive]
      |              |
      | Dispose       | Dialog dismissed
      | terminal      | Focus returns to conversation
      v              v
    [Terminated]    [Active]
```

**Key points:**
- The `Active` state persists across all agent turns. BeginTurn and EndTurn
  toggle the activity bar state, not the application lifecycle.
- `DialogActive` is a sub-state of `Active` -- the application and views
  remain running. The dialog overlays the conversation view and takes focus.
  The agent may continue working in the background.
- `Shutdown` occurs only when the session ends (`/quit`, `/exit`, or EOF).

---

## 4. Interactive Command Overlay Protocol

### 4.1 Overview

Interactive slash commands that need user input (text fields, selection lists,
confirmation prompts) open in modal Dialog views that overlay the conversation.
This is the natural windowing model of the TUI framework -- no suspension or
mode switching is required.

### 4.2 How Dialogs Work

1. **User types an interactive slash command** (e.g., `/project create`).
2. **A modal Dialog opens** over the conversation view. The dialog takes focus
   and blocks keyboard input from reaching the conversation or input views.
3. **The agent continues working** if a turn is active. Streaming tokens
   accumulate in the conversation model. Tool results add blocks to the scroll
   buffer. The dialog does not block background processing.
4. **User completes or cancels the dialog.** Focus returns to the input view.
   The conversation view displays all content that arrived during the dialog.

### 4.3 Dialog Types

| Slash Command Category | Dialog Behavior |
|---|---|
| Create wizards (`/project create`, `/jea create`) | Multi-step dialog with text fields and selection lists |
| Edit menus (`/project edit`, `/jea edit`) | Selection list of editable fields, then field-specific input |
| Delete confirmations (`/project delete`, `/jea delete`) | Confirmation dialog (Yes/No) |
| Setup flows (`/provider setup`) | API key input, provider selection |
| Assignments (`/jea assign`, `/jea unassign`) | Selection list of profiles/projects |

### 4.4 Read-Only Windows

Read-only slash commands (`/help`, `/project show`, `/context show`, etc.) open
as modeless windows. These float over the conversation view and do not block
input. The activity bar shows `Esc to dismiss` while a window is open.

### 4.5 Common Case: No Dialog Needed

Most slash commands that display information use modeless windows (see 4.4).
Only commands that require interactive user input open modal dialogs. The
majority of user interaction is at the input view prompt.

### 4.6 Pre-Application Prompts

Two prompt categories run before the TUI application starts:
- Login prompts (API key input, OAuth flow)
- GCP location selection

These use direct console prompts as a fallback because the TUI application does
not exist yet. They do not require any special overlay protocol.

---

## 5. Resize Handling

### 5.1 During Active Turns

When the terminal is resized during an active turn:

1. Terminal dimensions are re-read on the next render cycle.
2. The view hierarchy redistributes space to its regions.
3. The conversation view re-renders at the new width.
4. Dynamic input height is recalculated.
5. The layout adapts automatically -- no manual resize detection needed.

### 5.2 Between Turns (Idle State)

When the terminal is resized between turns, the view hierarchy adapts on the
next render cycle. The conversation view re-renders visible blocks at the new
width. The input view re-measures its content.

### 5.3 Minimum Terminal Height

If the terminal height is below 10 rows:
- The TUI layout is not activated
- The app runs in scrollback-only mode with blocking prompts
- Thinking/streaming states use static messages to stderr

---

## 6. Thread Safety

### 6.1 Between Turns

Between turns, there is minimal concurrency:
- The main thread owns all state
- The input handler runs on a background thread, writing completed lines to a
  thread-safe message queue
- The message queue is inherently thread-safe
- No console lock needed

### 6.2 During Active Turns

During active turns:
- The view hierarchy's render loop runs on the main thread
- The orchestrator updates shared state from background threads via
  `Application.Invoke()` (thread-safe main-thread invocation)
- Shared state (stream buffer, tool results, activity state) uses thread-safe
  patterns (Interlocked, volatile, or lock-free data structures)
- The render loop reads shared state; background threads write to it through
  the main-thread invocation mechanism
- No console lock is needed -- the application serializes all rendering

### 6.3 Cancel Lock

A separate lock guards the cancellation state machine to prevent race
conditions between key presses and the timer callback.

**Critical ordering:** Cancel callbacks are invoked **outside** the lock to
prevent deadlock. The pattern is:

1. Acquire the cancel lock
2. Determine action (shouldShowHint or shouldCancel)
3. Release the cancel lock
4. Invoke callbacks outside the lock

### 6.4 Thread-Safe Operations

| Operation | Thread Safety Mechanism |
|---|---|
| Message queue reads/writes | Queue is inherently thread-safe |
| Stream buffer updates | Interlocked/volatile, read by render loop |
| Activity state changes | Volatile field, written via main-thread invocation |
| View updates from background threads | `Application.Invoke()` posts to main thread |

### 6.5 Not Thread-Safe (By Design)

| Operation | Notes |
|---|---|
| Line buffer, cursor position, history index | Only accessed from the key reader task; single-writer |
| Turn content model | Written by main thread during active turn; read by render loop (serialized by the application) |

---

## 7. Output Routing

### 7.1 All Output Through Views

While the TUI application is active, ALL visual output routes through the
view hierarchy. There are no direct console writes.

| Content | Destination View |
|---|---|
| User message echo | Conversation view (Panel with grey23 background) |
| Streaming text | Conversation view (appended as tokens arrive) |
| Tool call badges | Conversation view (Panel with rounded border) |
| Tool result badges | Conversation view (status line) |
| Token usage | Conversation view (dim metadata line) |
| Activity state | Activity bar (spinner + label, or dim Rule) |
| Status metadata | Status bar (provider, model, project, engine, key hints) |
| Input echo | Input view (user's typed text with cursor) |
| Slash command output (read-only) | Modeless window overlay |
| Slash command output (interactive) | Modal dialog overlay |
| Error messages | Conversation view or dialog (depending on context) |

### 7.2 Pre-Application Output

Before the TUI application starts, output goes directly to the terminal:
- Login prompts and provider setup (separate CLI commands)
- Provider activation error messages
- Pre-session configuration errors

### 7.3 Non-Layout Mode Output

When the TUI layout is not available (terminal height < 10 rows, piped output):
- All output goes to stdout as plain text
- Errors go to stderr
- No view hierarchy, no scrolling, no windowing

---

## 8. Turn Lifecycle Detail

### 8.1 Full Sequence

This is the complete step-by-step sequence for a single agent turn:

1. **User types in the input view** (`> _`). Application is active in idle
   state.
2. **User presses Enter**. Line submitted to message queue.
3. **Main thread reads from message queue**. Adds message to conversation
   model.
4. **User message rendered in conversation view**: A Panel with grey23
   background containing the escaped user text. The conversation view
   auto-scrolls to the bottom.
5. **BeginTurn**: Activity bar set to Thinking (yellow spinner +
   `Thinking...`). Agent-busy flag set. Input view prompt dims.
6. **Dispatch LLM request**. Await streaming response.
7. **On first token**: Activity bar transitions to Streaming (cyan spinner +
   `Streaming...`).
8. **Streaming loop**: Append tokens to stream buffer, update conversation
   view, screen refreshes at 60fps.
9. **On CompletionChunk**: Check for tool calls.
10. **If tool calls**: Render tool badge in conversation view. Activity bar
    shows Executing with elapsed timer. Execute command with cancellation
    monitor. Render tool result badge. Add results to conversation. Loop back
    to step 6 for next round.
11. **If end_turn or max rounds**: Set turn-complete flag.
12. **EndTurn**: Activity bar set to Idle (dim Rule). Agent-busy flag cleared.
    Input view prompt changes from dim to bold blue `>`.
13. **All turn content is already in the scroll buffer.** No flushing needed.
    The conversation view continues showing the conversation history.
14. **User types next message** in the input view. Back to step 1.

### 8.2 Error During Turn

If an error occurs during the active turn:

1. The error is rendered in the conversation view (or as a dialog if critical).
2. Any partial content remains in the scroll buffer.
3. Activity bar returns to Idle (dim Rule).
4. User message is removed from conversation (allow retry).
5. Input view returns to normal (bold blue `>`).

### 8.3 Cancellation During Turn

If the user cancels during an active turn:

1. Cancellation token fires.
2. Current operation (streaming or execution) stops.
3. Partial results are preserved in the scroll buffer.
4. Activity bar returns to Idle (dim Rule).
5. Conversation view shows all content up to the cancellation point.
6. Input view returns to normal (bold blue `>`).
