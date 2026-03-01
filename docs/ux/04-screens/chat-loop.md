# Screen: Chat Loop (Prescriptive)

## Overview

The chat loop is the primary screen of BoydCode. The user spends 95%+ of their
time here. It is a proper TUI with structured regions, non-blocking modal
overlays, streaming content, and keyboard-driven navigation -- all rendered
through Spectre.Console's Layout and Live display widgets. No raw ANSI escape
sequences.

This spec is PRESCRIPTIVE -- it describes what the screen SHOULD look like.

---

## Layout Architecture

The screen is divided into four named regions managed by a Spectre.Console
`Layout` widget inside `AnsiConsole.Live()`.

```
Layout("Root")
  SplitRows:
    Layout("Content")    -- Ratio(1), MinimumSize(5)
    Layout("Indicator")  -- Size(1)
    Layout("Input")      -- Size(1)
    Layout("StatusBar")  -- Size(1)
```

### Region Responsibilities

| Region    | Size       | Content                                     |
|-----------|------------|---------------------------------------------|
| Content   | Ratio(1)   | Conversation messages, tool panels, modals   |
| Indicator | Size(1)    | Idle rule or active state indicator          |
| Input     | Size(1)    | User input line with `> ` prefix             |
| StatusBar | Size(1)    | Provider, model, project, engine, key hints  |

The Content region absorbs all remaining vertical space. The three fixed rows
(Indicator, Input, StatusBar) consume exactly 3 rows total.

---

## Layout (120 columns) -- Idle State

Terminal: 40 rows by 120 columns. Content region is 37 rows.

```
                                                                                                                        Row 1
                                                                                                                        ...
  (previous conversation scrollback)                                                                                    ...
                                                                                                                        ...
  > Can you add error handling to the auth module?                                                                      Row 28
                                                                                                                        Row 29
  I've examined the auth module and added comprehensive error handling.                                                 Row 30
  Each public method now has try-catch blocks that log the exception                                                    Row 31
  and throw a typed AuthenticationException with context.                                                               Row 32
                                                                                                                        Row 33
  4,521 in / 892 out / 5,413 total                                                                                     Row 34
                                                                                                                        Row 35
                                                                                                                        Row 36
                                                                                                                        Row 37
----------------------------------------------------------------------------------------------------------------------------Row 38
> _                                                                                                                     Row 39
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands        Row 40
```

### Anatomy

**Rows 1-37 (Content region)**: Shows the tail of the conversation. Older
messages scroll out of view as new ones arrive. Each turn consists of:
- User message: `  > {text}` (bold blue `>`, plain text)
- Blank line
- Assistant response: `  {text}` (plain text, 2-space indent)
- Tool call badges and results (if any, inline within the response)
- Token usage: `  {input} in / {output} out / {total} total` (dim)
- Blank line (turn separator)

**Row 38 (Indicator)**: Dim horizontal rule (`Rule` with dim style). This row
transitions to show active state indicators during agent activity.

**Row 39 (Input)**: `> ` prefix followed by the user's typed text and cursor.
When the agent is busy and messages are queued, a dim queue count appears
right-aligned: `[2 queued]`.

**Row 40 (StatusBar)**: Left-aligned session metadata (pipe-separated) and
right-aligned keybinding hints (both dim).

---

## Layout (80 columns) -- Idle State

Terminal: 30 rows by 80 columns. Content region is 27 rows.

```
                                                                Row 1
  (previous scrollback)                                         ...
                                                                ...
  > Can you add error handling?                                 Row 20
                                                                Row 21
  I've examined the auth module and added comprehensive         Row 22
  error handling. Each public method now has try-catch           Row 23
  blocks that log the exception and throw a typed               Row 24
  AuthenticationException with context.                         Row 25
                                                                Row 26
  4,521 in / 892 out / 5,413 total                              Row 27
                                                                Row 28
------------------------------------------------------------    Row 28
> _                                                             Row 29
Gemini | gemini-2.5-pro | my-project         /help              Row 30
```

Differences from 120-column:
- Content wraps at 80 columns (less text per line)
- Status bar shows abbreviated metadata (no branch, no engine)
- Key hints abbreviated

---

## Layout (120 columns) -- Agent Busy, Streaming

```
  > Can you add error handling to the auth module?

  I've examined the auth module. Let me look at the specific
  methods that need error handling|

@ Streaming...
> _                                                                                                     [1 queued]
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Key Observations

1. The Content region shows the streaming response growing token by token.
2. The Indicator bar shows `@ Streaming...` in cyan text.
3. The Input line shows `> _` with a right-aligned `[1 queued]` indicator
   because the user has typed another message while the AI is working.
4. The cursor is on the Input line, ready for more typing.
5. The StatusBar is unchanged.

---

## Layout (120 columns) -- Agent Busy, Executing Tool

```
  > Can you add error handling to the auth module?

  I'll start by examining the current code.

  4,521 in / 245 out / 4,766 total

  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs -TotalCount 100                                                  |
  +------------------------------------------------------------------------------------------------------------+

@ Executing... (1.2s)
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Key Observations

1. The tool call badge (Panel with rounded border, grey) appears in the Content.
2. The Indicator bar shows `@ Executing... (1.2s)` in blue text with elapsed time.
3. No execution output is visible in the content area during execution. Output
   is buffered and will appear as a Tool Result Badge when execution completes.

---

## Layout (120 columns) -- Tool Result Shown

```
  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs -TotalCount 100                                                  |
  +------------------------------------------------------------------------------------------------------------+
  \u2713 Shell  42 lines | 0.3s
  /expand to show full output

  Now I can see the auth module. I'll add error handling to each public method...

@ Streaming...
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Key Observations

1. The tool result badge appears immediately below the tool call badge.
2. The expand hint is dim italic.
3. The assistant's next text segment follows the tool result.
4. This is all within one assistant turn (continuous, no turn separator).

---

## Layout (120 columns) -- Modal Overlay (/help)

User types `/help` while the AI is streaming. The Content region is replaced
with the modal panel.

```
+-- Help --------------------------------------------------------------------------------------------------+
|                                                                                                          |
|  Command            Description                                                                          |
|  /help              Show available commands                                                              |
|  /project <sub>     Manage projects (create, list, show, edit, delete)                                   |
|  /provider <sub>    Manage LLM providers (setup, list, show, remove)                                     |
|  /jea <sub>         Manage JEA security profiles                                                         |
|  /sessions <sub>    Manage sessions (list, show, delete)                                                 |
|  /context <sub>     View/manage context window (show, compact, summarize)                                |
|  /expand            Show last tool output                                                                |
|  /refresh           Refresh session context                                                              |
|  /clear             Clear conversation                                                                   |
|  /quit              Exit BoydCode                                                                        |
|                                                                                                          |
|  Esc to dismiss                                                                                          |
|                                                                                                          |
+----------------------------------------------------------------------------------------------------------+
Esc to dismiss
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Key Observations

1. The ENTIRE Content region is replaced by the modal Panel.
2. The Indicator bar shows `Esc to dismiss` (dim text).
3. The Input line remains active -- the user can type commands while the
   modal is visible.
4. The AI continues streaming in the background. Tokens accumulate in the
   data model but are not visible until the modal is dismissed.
5. Pressing Esc restores the Content region to the conversation view.

---

## Layout (80 columns) -- Modal Overlay (/project show)

```
+-- my-project ------------------------------------------+
|                                                        |
|  Provider  Gemini          Engine   InProcess           |
|  Docker    python:3.12     Branch   main               |
|                                                        |
|  -- Directories ---                                    |
|  C:\Users\jason\src\app    ReadWrite                   |
|  C:\Users\jason\src\lib    ReadOnly                    |
|                                                        |
|  -- System Prompt ---                                  |
|  You are a Python expert working on the app            |
|  project. Focus on clean code and type safety.         |
|                                                        |
|  Esc to dismiss                                        |
|                                                        |
+--------------------------------------------------------+
Esc to dismiss
> _
Gemini | gemini-2.5-pro | my-project         /help
```

---

## Layout (120 columns) -- Interactive Slash Command (Suspended)

When the user types `/project create`, the Live context is suspended. The
terminal shows a standard Spectre.Console prompt flow:

```
  Project name: my-api
  Choose execution engine:
  > InProcess
    Container

  Directory path (Enter to finish): C:\Users\jason\src\my-api
  Access level:
  > ReadWrite
    ReadOnly

  Directory path (Enter to finish):
  Docker image (Enter to skip):

  \u2713 Project my-api created.
```

After the wizard completes, the Live context is re-entered and the conversation
view is restored. Any tokens that arrived during the wizard are visible.

---

## Layout (120 columns) -- Cancel Hint

```
  I'll start by examining the current code|

Press Esc again to cancel
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

The Indicator bar shows the cancel hint in yellow. After 1 second without a
second press, it reverts to the prior state (e.g., `@ Streaming...`).

---

## Layout (120 columns) -- Queue Indicator

When the agent is busy and the user has submitted additional messages:

```
@ Executing... (3.1s)
> what about the error codes?                                                                           [2 queued]
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

The `[2 queued]` indicator is dim, right-aligned on the Input row. It shows
only when `_agentBusy == true` and `queueCount > 0`.

---

## States

| State | Condition | Content Region | Indicator Bar | Input Line | StatusBar |
|-------|-----------|----------------|---------------|------------|-----------|
| Idle | No agent activity | Conversation tail | Dim rule | `> _` | Metadata + hints |
| Thinking | Request sent, no tokens | Conversation tail | `@ Thinking...` (yellow) | `> _` | Unchanged |
| Streaming | Tokens arriving | Conversation + streaming text | `@ Streaming...` (cyan) | `> _` + optional queue | Unchanged |
| Executing | Tool running | Conversation + tool badge | `@ Executing... (Ns)` (blue) | `> _` + optional queue | Unchanged |
| Cancel hint | First Esc press | Unchanged | `Press Esc again to cancel` (yellow) | Unchanged | Unchanged |
| Modal open | Read-only slash cmd | Modal Panel | `Esc to dismiss` (dim) | `> _` | Unchanged |
| Suspended | Interactive slash cmd | Normal terminal (no Layout) | N/A | Spectre prompts | N/A |
| Non-layout | Piped, < 10 rows | Scrollback output | N/A | `[bold blue]>[/]` prompt | Inline status |

---

## Interactive Elements

### Input Line

The Input line provides full line-editing via `AsyncInputReader`:

| Key | Action |
|-----|--------|
| Printable characters | Insert at cursor position |
| Enter | Submit line. Add to history. Clear buffer. |
| Backspace | Delete character before cursor |
| Delete | Delete character at cursor |
| Left / Right Arrow | Move cursor within line |
| Home / End | Jump to start / end of line |
| Up / Down Arrow | Navigate command history |
| Esc | Cancel hint (if agent active) or dismiss modal |
| Tab | Reserved (no action) |

### Command History

- Stores up to 100 entries
- Consecutive duplicates are deduplicated
- Current unsaved input is preserved when entering history
- Down past the newest entry restores the unsaved input

### Slash Command Dispatch

Lines starting with `/` are dispatched to the slash command registry:

| Category | Commands | Behavior |
|----------|----------|----------|
| Modal | `/help`, `/project show`, `/project list`, `/provider show`, `/provider list`, `/sessions list`, `/sessions show`, `/jea list`, `/jea show`, `/jea effective`, `/context show`, `/expand` | Open modal overlay in Content region |
| Interactive | `/project create`, `/project edit`, `/project delete`, `/provider setup`, `/provider remove`, `/jea create`, `/jea edit`, `/jea delete`, `/jea assign`, `/jea unassign`, `/sessions delete` | Suspend Live, run prompts, resume Live |
| Inline | `/clear`, `/refresh`, `/context compact`, `/context summarize` | Execute and show result in Content region |
| Exit | `/quit`, `/exit` | End session loop |

---

## Behavior

### Layout Activation

1. Banner renders BEFORE Layout/Live activation (standard Spectre.Console output).
2. `AnsiConsole.Live(layout).StartAsync(...)` is entered.
3. `AsyncInputReader.Start()` begins background key polling.
4. Content region shows the initial conversation state (empty or resumed).
5. Indicator bar shows idle rule.
6. Status bar shows session metadata.
7. Input line shows `> _`.

### Content Region Rendering

The Content region renders a composed `Rows` renderable built from the
conversation data model. On each refresh cycle:

1. Calculate how many turns fit in the available height.
2. Build renderables for each visible turn (user message, assistant text,
   tool badges, token usage).
3. If streaming is active, append the in-progress streaming block.
4. Compose all blocks into a `Rows(...)` renderable.
5. `layout["Content"].Update(rows)`.
6. `ctx.Refresh()`.

The refresh cycle runs at ~60fps maximum (16ms minimum interval) during
streaming. During idle, refresh occurs only on state changes.

### Modal Overlay Flow

1. User submits a modal slash command (e.g., `/project show`).
2. The command handler builds a Panel renderable with the content.
3. `layout["Content"].Update(modalPanel)`.
4. `_indicatorState = IndicatorState.Modal`.
5. `ctx.Refresh()`.
6. AsyncInputReader watches for Esc key.
7. On Esc: `layout["Content"].Update(BuildConversationView())`.
8. Indicator bar reverts to prior state.
9. `ctx.Refresh()`.

**During modal**: The AI continues working. Streaming tokens accumulate in
the conversation model. When the modal is dismissed, the conversation view
includes all new content.

### Suspend/Resume for Interactive Prompts

1. User submits an interactive slash command (e.g., `/project create`).
2. Set `_suspendRequested = true` in the render loop.
3. The render loop exits the `Live.StartAsync` lambda cleanly.
4. The terminal is now under standard Spectre.Console control.
5. The slash command handler runs its prompts normally.
6. After completion, `AnsiConsole.Live(layout).StartAsync(...)` is re-entered.
7. The Content region is rebuilt from the (possibly updated) conversation model.

### Layout Deactivation

1. User types `/quit` or `/exit`.
2. The render loop exits the `Live.StartAsync` lambda.
3. `AsyncInputReader` is disposed.
4. The terminal returns to normal operation (no `ESC[r]` reset needed --
   Spectre.Console handles cleanup).

### Resize Handling

When the terminal is resized:

1. `AnsiConsole.Profile.Width` updates automatically.
2. On the next `ctx.Refresh()`, Spectre.Console re-measures all renderables
   at the new width.
3. The Layout widget redistributes space to its regions.
4. The Content region re-renders the conversation view at the new width.
5. No manual `CheckForResize()` or screen clearing is needed.

---

## Edge Cases

- **Narrow terminal (< 80 columns)**: Status bar shows abbreviated metadata
  (provider + model only). Conversation text wraps at the narrow width. Tool
  preview panels wrap their content. Modal overlays fill the available width.

- **Very wide terminal (> 200 columns)**: No issues. Panels expand via
  `.Expand()`. Text does not stretch -- it remains left-aligned with the
  standard indent.

- **Terminal height < 10 rows**: `CanUseLayout()` returns false. No Layout
  widget. No Live display. Fallback to scrollback mode with blocking
  `AnsiConsole.Prompt` for input.

- **Non-interactive/piped**: `AnsiConsole.Profile.Capabilities.Interactive`
  is false. No Layout. Input via `Console.ReadLine()`, returns `/quit` on EOF.
  Slash commands output to scrollback.

- **Modal open during streaming**: Tokens accumulate in the data model.
  Conversation view "catches up" on modal dismiss. User sees the response
  appear as if it arrived instantly.

- **Rapid typing during output**: AsyncInputReader writes to a Channel.
  The render loop reads from it. No lock contention -- the Live context
  serializes all rendering.

- **Console.WindowHeight throws**: Caught. Default to 24 rows. Layout may
  use fallback sizing.

- **Stale settings warning**: After a slash command modifies settings (e.g.,
  `/provider setup`), the status bar updates to reflect the new configuration
  on the next render cycle. No separate warning mechanism needed.

---

## Component Patterns Used

| Pattern | Reference | Usage |
|---------|-----------|-------|
| User Message Block | 07-component-patterns.md #1 | User messages in conversation |
| Assistant Message Block | 07-component-patterns.md #2 | Assistant responses |
| Turn Separator | 07-component-patterns.md #3 | Between conversation turns |
| Tool Call Badge | 07-component-patterns.md #4 | Tool invocation preview |
| Tool Result Badge | 07-component-patterns.md #5 | Tool execution result |
| Modal Overlay | 07-component-patterns.md #11 | Read-only slash commands |
| Status Bar | 07-component-patterns.md #25 | Bottom metadata row |
| Indicator Bar | 07-component-patterns.md #26 | Activity indicator |
| Streaming Text | 07-component-patterns.md #18 | LLM token streaming |
| Thinking Indicator | 07-component-patterns.md #19 | Pre-response state |
| Cancel Hint | 07-component-patterns.md #20 | Esc double-press flow |

---

## Spectre.Console Implementation Notes

### Layout Construction

```csharp
var layout = new Layout("Root")
    .SplitRows(
        new Layout("Content").Ratio(1).MinimumSize(5),
        new Layout("Indicator").Size(1),
        new Layout("Input").Size(1),
        new Layout("StatusBar").Size(1));
```

### Live Display Loop

```csharp
await AnsiConsole.Live(layout)
    .AutoClear(false)
    .Overflow(VerticalOverflow.Ellipsis)
    .StartAsync(async ctx =>
    {
        while (!_exitRequested && !_suspendRequested)
        {
            // Update regions from shared state
            layout["Content"].Update(
                _modalActive ? _modalContent : BuildConversationView());
            layout["Indicator"].Update(BuildIndicator());
            layout["Input"].Update(BuildInputLine());
            layout["StatusBar"].Update(BuildStatusBar());
            ctx.Refresh();

            // Wait for state change or refresh interval
            await Task.Delay(16); // ~60fps cap
        }
    });
```

### Thread Safety Model

- The render loop runs on the Live context's thread.
- `AsyncInputReader` runs on a background thread, writing completed lines
  to a `Channel<string>`.
- The orchestrator runs on the main thread, reading from the Channel.
- Shared state (conversation model, indicator state, modal state) is
  accessed via thread-safe patterns (Interlocked, volatile, or lock-free
  data structures).
- The render loop reads shared state; worker threads write to it.
- No `_consoleLock` is needed -- the Live context handles render serialization.
