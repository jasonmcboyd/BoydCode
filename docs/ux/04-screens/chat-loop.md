# Screen: Chat Loop (Prescriptive)

## Overview

The chat loop is the primary screen of BoydCode. The user spends 95%+ of their
time here. It uses a **persistent always-on TUI** architecture: the application
starts once at session begin and stays active for the entire session. All
conversation content -- banner, user messages, assistant responses, tool output,
slash command results -- accumulates in a scrollable conversation view. The
layout is always visible; there is no mode switching between "scrollback" and
active states.

This spec is PRESCRIPTIVE -- it describes what the screen SHOULD look like.

---

## Architecture: Persistent TUI

### Always-On Application

- The TUI application starts once during session initialization and remains
  active until the session ends.
- All content accumulates in the conversation view (2000 blocks max). There is
  no flushing to raw scrollback between turns.
- Between turns, the layout remains visible. The activity bar shows a dim
  Rule when idle. The user types in the input view.
- During active turns, the activity bar shows an animated spinner with state
  label. Streaming tokens and tool output appear in the conversation view.

### Conversation Scroll Buffer

All conversation content is stored as blocks in an ordered list. New content is
appended at the bottom. The conversation view renders a viewport into this list,
showing the most recent blocks that fit. The user can scroll through the full
conversation history using keyboard navigation.

- Maximum 2000 blocks. When exceeded, the oldest blocks are silently trimmed.
- The banner is the first content block in the buffer (added after the
  application starts).
- Each user message, assistant text block, tool badge, tool result, token
  usage line, and separator is a separate content block.

### Pre-Application Output

Before the TUI application starts, some output renders directly to the terminal:
- Login prompts and provider setup (separate CLI commands)
- Error messages from provider activation failures

These use direct console writes as a fallback because the TUI application does
not exist yet. Once the application starts, ALL visual output routes through
the view hierarchy.

### Turn Lifecycle

```
1. Application is active (always). User types in the input view.
2. User presses Enter (non-empty text).
3. User message added to the conversation view (styled block with muted background).
4. BeginTurn: Activity bar set to Thinking, agent-busy flag set.
5. Agent works: Thinking -> Streaming -> Executing -> Streaming -> ...
6. Agent turn ends (end_turn or max rounds).
7. EndTurn: Activity bar set to Idle, agent-busy flag cleared.
8. Application still active. User types next message in the input view.
```

There is no application creation or destruction between turns. The TUI persists
across the entire session. BeginTurn and EndTurn only toggle activity state --
they do not affect the application lifecycle.

---

## View Hierarchy

### Five Views

The screen is composed of five views arranged vertically:

```
+==============================================================================+
|                                                                              |
|  CONVERSATION VIEW                                                           |
|                                                                              |
|  Scrollable conversation history: user messages, assistant responses,        |
|  tool execution panels, slash command results. Takes all remaining           |
|  vertical space.                                                             |
|                                                                              |
|  When a modal window is open, it overlays the conversation view.             |
|  The underlying conversation state is preserved and restored                 |
|  when the window is dismissed.                                               |
|                                                                              |
+------------------------------------------------------------------------------+
|  ACTIVITY BAR  (1 row)                                                       |
|  Idle: dim horizontal rule                                                   |
|  Active: spinner + "Thinking..." / "Executing..." / "Streaming..."           |
+------------------------------------------------------------------------------+
|  INPUT VIEW  (1+ rows, dynamic height)                                       |
|  > User types here                                                           |
+------------------------------------------------------------------------------+
|  SEPARATOR  (1 row)                                                          |
|  Dim horizontal rule                                                         |
+------------------------------------------------------------------------------+
|  STATUS BAR  (1 row)                                                         |
|  Provider | Model | Project | Branch | Engine    Esc: cancel  /help: commands |
+==============================================================================+
```

### View Descriptions

| View             | Height           | Content                                       |
|------------------|------------------|-----------------------------------------------|
| Conversation     | All remaining    | Scrollable conversation history (viewport)    |
| Activity Bar     | 1 row            | Spinner + state label; dim Rule when idle      |
| Input            | 1-10 rows        | User text with cursor, grows with wrapping     |
| Separator        | 1 row            | Dim horizontal rule (static, never changes)    |
| Status Bar       | 1 row            | Provider, model, project, engine, key hints    |

### Conversation View Height Calculation

The conversation view absorbs all remaining vertical space:

```
conversationHeight = termHeight - 1 (activity) - inputHeight - 1 (separator) - 1 (status bar)
```

Fixed overhead: Activity Bar (1) + Separator (1) + Status Bar (1) = 3 rows.
Input height is dynamic (see Dynamic Input Height below).

### Dynamic Input Height

The input view grows from 1 to `min(10, termHeight / 4)` lines based on
how the user's text wraps at the current terminal width. As the user types
longer text, the input view expands and the conversation view shrinks. When
the user submits and the buffer clears, the input view returns to 1 line.

### Output Routing

All console output during a session routes through the view hierarchy. No
direct console writes are permitted while the TUI application is active.

---

## Idle State (120 columns)

Between turns. The application is active; the activity bar shows a dim Rule.
The user sees the conversation history and types in the input view.

```
  (conversation history)                                                                    Conversation
                                                                                            (scrollable)
  I've examined the auth module and added comprehensive error handling.
  Each public method now has try-catch blocks that log the exception
  and throw a typed AuthenticationException with context.

  4,521 in / 892 out / 5,413 total

──────────────────────────────────────────────────────────────────────────────────────────── Activity Bar
> _                                                                                        Input
──────────────────────────────────────────────────────────────────────────────────────────── Separator
Gemini | gemini-2.5-pro | my-project | main | InProcess         Esc: cancel  /help: commands  Status Bar
```

### Anatomy

**Conversation view**: All conversation history lives in the scroll buffer.
The conversation view shows a viewport into this buffer, rendering as many
blocks as fit in the available height. The user can scroll up/down to
review earlier content (see Scroll Behavior below). The banner is the
first block at the top of the buffer.

**Activity bar (idle)**: When no agent activity is occurring, the activity bar
shows a dim horizontal Rule. This provides a clean visual separator without
suggesting any activity.

**Input view**: The input prompt `> _` with cursor. The `>` is rendered in
bold blue when the agent is idle. The cursor position is indicated with an
underline. When the user types text, it appears here in real time.

**Status bar**: Session metadata on the left, keybinding hints on the right.
Left: `Provider | Model | Project | Branch | Engine` (all dim).
Right: `Esc: cancel  /help: commands` (dim).

---

## Idle State (80 columns)

```
  (conversation history)
                                                                Conversation
  I've examined the auth module and added
  comprehensive error handling. Each public
  method now has try-catch blocks.

  4,521 in / 892 out / 5,413 total

──────────────────────────────────────────────────────────────── Activity Bar
> _                                                             Input
──────────────────────────────────────────────────────────────── Separator
Gemini | gemini-2.5-pro | my-project      /help                 Status Bar
```

Differences from 120-column:
- Content wraps at 80 columns (less text per line)
- Status bar abbreviates: shows provider, model, project, and `/help` only

---

## Active Turn -- Streaming (120 columns)

The agent is streaming a response. The layout is unchanged structurally;
only the activity bar and conversation view update.

```
  I've examined the auth module and found several methods that lack proper error handling. Let me
  walk through each one:

  1. `AuthenticateAsync` - Currently throws raw exceptions. I'll wrap this in a try-catch that
     converts to AuthenticationException.
  2. `ValidateTokenAsync` - No error handling at all. If the token is malformed, it crashes|

⠿ Streaming...                                                                                Activity Bar
> _                                                                                            Input
────────────────────────────────────────────────────────────────────────────────────────────────── Separator
Gemini | gemini-2.5-pro | my-project | main | InProcess         Esc: cancel  /help: commands   Status Bar
```

### Key Observations

1. The conversation view shows the streaming response growing token by token.
   Streaming text is only visible when the viewport is pinned to the bottom.
2. The activity bar shows an animated braille spinner followed by
   `Streaming...` in cyan text.
3. The status bar shows session metadata (unchanged).
4. The input view shows `> ` with a cursor (dim when agent is busy).
   Typeahead is visible and a `[N queued]` badge appears when messages
   are pending.

---

## Active Turn -- Thinking (120 columns)

```
  (Conversation view shows prior conversation context)



⠿ Thinking...                                                                                 Activity Bar
> _                                                                                            Input
────────────────────────────────────────────────────────────────────────────────────────────────── Separator
Gemini | gemini-2.5-pro | my-project | main | InProcess         Esc: cancel  /help: commands   Status Bar
```

The activity bar shows a yellow spinner with `Thinking...` text, with an
animated braille spinner (10-frame, 100ms/frame). The conversation view
continues showing the conversation history viewport.

---

## Active Turn -- Executing Tool (120 columns)

```
  I'll start by examining the current code.

  4,521 in / 245 out / 4,766 total

  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs -TotalCount 100                                                  |
  +------------------------------------------------------------------------------------------------------------+

⠿ Executing... (1.2s)                                                                         Activity Bar
> _                                                                                            Input
────────────────────────────────────────────────────────────────────────────────────────────────── Separator
Gemini | gemini-2.5-pro | my-project | main | InProcess         Esc: cancel  /help: commands   Status Bar
```

### Key Observations

1. The tool call badge (bordered box with muted border color) appears in the
   conversation view's scroll buffer.
2. The activity bar shows a braille spinner followed by `Executing... (1.2s)`
   in cyan text. The spinner animates at 100ms per frame (10-frame cycle); the
   elapsed time updates each frame.
3. No execution output is visible in the content area during execution. Output
   is buffered and will appear as a Tool Result Badge when execution completes.

---

## Active Turn -- Tool Result Shown (120 columns)

```
  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs -TotalCount 100                                                  |
  +------------------------------------------------------------------------------------------------------------+
  ✓ Shell  42 lines | 0.3s
  /expand to show full output

  Now I can see the auth module. I'll add error handling to each public method...|

⠿ Streaming...                                                                                Activity Bar
> _                                                                                            Input
────────────────────────────────────────────────────────────────────────────────────────────────── Separator
Gemini | gemini-2.5-pro | my-project | main | InProcess         Esc: cancel  /help: commands   Status Bar
```

### Key Observations

1. The tool result badge appears immediately below the tool call badge.
2. The expand hint is dim italic.
3. The assistant's next text segment follows the tool result.
4. This is all within one assistant turn (continuous, no turn separator).
5. The activity bar now shows Streaming again (next LLM round).

---

## Active Turn -- Cancel Hint (120 columns)

```
  I'll start by examining the current code|

Press Esc again to cancel                                                                      Activity Bar
> _                                                                                            Input
────────────────────────────────────────────────────────────────────────────────────────────────── Separator
Gemini | gemini-2.5-pro | my-project | main | InProcess         Esc: cancel  /help: commands   Status Bar
```

The activity bar shows the cancel hint in yellow: `Press Esc again to
cancel`. After 1 second without a second press, it reverts to the prior
state (e.g., the Streaming spinner).

---

## Scrolled Up (120 columns)

When the user scrolls up through the conversation history, the viewport
shifts and a "more content below" indicator appears.

```
 > Can you explain how the auth module works?

  The authentication module handles three key operations:

  1. Token validation - verifying JWT tokens against the signing key
  2. Credential refresh - obtaining new access tokens via refresh tokens
  3. Session management - tracking active sessions and their expiry

  Each operation follows the same error handling pattern: try the operation,
  catch specific exception types, wrap in AuthenticationException...

↓ More content below                                                                           Conversation
──────────────────────────────────────────────────────────────────────────────────────────────── Activity Bar
> _                                                                                            Input
────────────────────────────────────────────────────────────────────────────────────────────────── Separator
Gemini | gemini-2.5-pro | my-project | main | InProcess         Esc: cancel  /help: commands   Status Bar
```

### Key Observations

1. The dim `↓ More content below` indicator appears at the bottom of the
   conversation view when the viewport is not pinned to the bottom.
2. While scrolled up, streaming content is NOT visible. Tokens accumulate
   in the stream buffer but are only rendered when pinned to the bottom.
3. Typing any printable character auto-scrolls to the bottom.
4. Pressing Enter auto-scrolls to the bottom.
5. The activity bar shows the current agent state (or dim Rule if idle)
   regardless of scroll position.

---

## Multi-Line Input (120 columns)

When the user types a long message that wraps, the input view grows.

```
  (conversation history)

──────────────────────────────────────────────────────────────────────────────────────────────── Activity Bar
> I'd like you to refactor the authentication module to use the new IAuthProvider interface.    Input
  Please update all three methods -- AuthenticateAsync, ValidateTokenAsync, and                    (3 lines)
  RefreshCredentialsAsync -- to use dependency injection instead of direct instantiation._
────────────────────────────────────────────────────────────────────────────────────────────────── Separator
Gemini | gemini-2.5-pro | my-project | main | InProcess         Esc: cancel  /help: commands   Status Bar
```

### Behavior

1. The input view size grows from 1 to `min(10, termHeight/4)` as the
   user types text that wraps.
2. The conversation view shrinks correspondingly (maintaining the height
   invariant: `conversationHeight = termHeight - 3 - inputHeight`).
3. When the user submits (Enter), the input view returns to 1 line and
   the conversation view expands.
4. The activity bar always maintains its position between the conversation
   view and the input view.

---

## Interactive Slash Command (Modal Dialog)

When the user types an interactive slash command (e.g., `/project create`),
a modal dialog opens. The dialog blocks until the interactive flow completes.

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

  ✓ Project my-api created.
```

After the dialog completes, the layout reappears with all prior content
intact. The success message from the slash command appears in the
conversation view.

---

## Modal Overlay (120 columns)

When the user triggers a read-only slash command, a modeless window opens
over the conversation view. The layout structure is unchanged underneath.

```
+-- Help --------------------------------------------------------------------------------------------------+
|                                                                                                          |
|  Command            Description                                                                          |
|  /help              Show available commands                                                              |
|  /project <sub>     Manage projects (create, list, show, edit, delete)                                   |
|  /provider <sub>    Manage LLM providers (setup, list, show, remove)                                     |
|  /jea <sub>         Manage JEA security profiles                                                         |
|  /conversations <sub>  Manage conversations (list, show, rename, delete, clear)                          |
|  /context <sub>     View/manage context window (show, summarize, refresh)                                |
|  /expand            Show last tool output                                                                |
|  /quit              Exit BoydCode                                                                        |
|                                                                                                          |
|  Esc to dismiss                                                                                          |
|                                                                                                          |
+----------------------------------------------------------------------------------------------------------+
Esc to dismiss                                                                                 Activity Bar
> _                                                                                            Input
────────────────────────────────────────────────────────────────────────────────────────────────── Separator
Gemini | gemini-2.5-pro | my-project | main | InProcess         Esc: cancel  /help: commands   Status Bar
```

### Key Observations

1. The window overlays the conversation view. It does not replace it.
2. The activity bar shows `Esc to dismiss` (dim).
3. If the AI is streaming in the background, tokens accumulate in the
   data model but are not visible until the window is dismissed.
4. Pressing Esc dismisses the window and restores the full conversation
   view. All content that arrived during the overlay is immediately visible.

---

## States

| State | Condition | Activity Bar | Conversation View | Input |
|-------|-----------|--------------|-------------------|-------|
| Idle | No agent activity | Dim Rule | Conversation viewport | Bold blue `>` + cursor |
| Thinking | Request sent, no tokens | Yellow spinner + `Thinking...` | Conversation viewport | Dim `>` (typeahead) |
| Streaming | Tokens arriving | Cyan spinner + `Streaming...` | Streaming text grows (when pinned) | Dim `>` (typeahead) |
| Executing | Tool running | Cyan spinner + `Executing... (Ns)` | Tool badge visible | Dim `>` (typeahead) |
| Cancel hint | First Esc press | Yellow `Press Esc again to cancel` | Unchanged | Dim `>` (typeahead) |
| Modal | Read-only slash cmd | Dim `Esc to dismiss` | Window overlays conversation | Dim `>` or bold blue `>` |
| Dialog | Interactive slash cmd | N/A (dialog owns focus) | Visible behind dialog | Dialog owns input |
| Scrolled | User scrolled up | Current state (may be any) | Viewport offset, `↓` indicator | Normal input |
| Non-layout | Piped, < 10 rows | N/A | N/A (fallback scrollback output) | Blocking prompt |

### Key Architectural Points

- **No separate "Idle" rendering mode.** The layout is always on. Idle is just
  an activity state (dim Rule), not a different rendering pipeline.
- **No "Scrollback" mode.** All content lives in the scroll buffer. There is
  no flushing to raw scrollback between turns.
- **No application creation/destruction per turn.** The TUI application persists
  for the entire session.

---

## Activity Bar

The activity bar is a single-row view that shows the current agent state.

| State | Activity Bar Content | Color |
|-------|---------------------|-------|
| Idle | Dim horizontal Rule | dim |
| Thinking | `{spinner} Thinking...` | yellow |
| Streaming | `{spinner} Streaming...` | cyan |
| Executing | `{spinner} Executing... (Ns)` | cyan |
| Cancel hint | `Press Esc again to cancel` | yellow |
| Modal active | `Esc to dismiss` | dim |

Braille spinner frame sequence: ⠋ ⠙ ⠹ ⠸ ⠼ ⠴ ⠦ ⠧ ⠇ ⠏ (10 frames, 100ms per frame).

**Idle state**: When no agent is active, the activity bar renders a dim
Rule. This provides a clean visual separator between the conversation view
and the input view.

**All busy states** use the animated braille spinner. The spinner provides
consistent animated feedback for all active states.

In accessible mode (`BOYDCODE_ACCESSIBLE=1`), all animated indicators are
replaced with static text: `[Thinking...]`, `[Streaming...]`,
`[Executing... (2.3s)]`.

---

## Interactive Elements

### Input View

The input view is always visible at the bottom of the layout. It grows
dynamically based on text wrapping.

#### Idle Input (Agent Not Busy)

```
> _
```

The prompt `>` renders in bold blue. The cursor is indicated with an
underline. User text appears in plain (unstyled) text.

#### Busy Input (Agent Working)

```
> some typeahead text
```

When the agent is busy, the entire input line renders in dim styling.
Typed text is still visible and editable. Messages submitted during the
busy state are queued.

#### Queued Messages

```
> next instruction [2 queued]
```

When the user has submitted messages during an active turn, a yellow
`[N queued]` badge appears after the input text.

#### Key Bindings

| Key | Action |
|-----|--------|
| Printable characters | Insert at cursor position, auto-scroll to bottom |
| Enter | Submit line, add to history, clear buffer, auto-scroll to bottom |
| Backspace | Delete character before cursor |
| Delete | Delete character at cursor |
| Left / Right Arrow | Move cursor within line |
| Ctrl+Left / Right Arrow | Move cursor by word |
| Ctrl+Backspace | Delete word before cursor |
| Ctrl+Delete | Delete word at cursor |
| Home / End | Jump to start / end of line |
| Up / Down Arrow | Navigate command history |
| PageUp | Scroll conversation view up 5 blocks |
| PageDown | Scroll conversation view down 5 blocks |
| Ctrl+Home | Scroll conversation view to top |
| Ctrl+End | Scroll conversation view to bottom |
| Esc | Dismiss modal (if active), or trigger cancel flow |
| Tab | Reserved (no action) |

#### Horizontal Scrolling

When the text in the input buffer exceeds the available input width (terminal
width minus the `> ` prefix), the input line displays a scrolling viewport:

- The visible window is centered on the cursor position.
- A dim `←` appears at the left edge when text extends to the left.
- A dim `→` appears at the right edge when text extends to the right.
- Both arrows can appear simultaneously when the cursor is in the middle.

Example (cursor at `|`):

```
> ← ...e authentication module so that it handles expir|ed tokens →
```

### Command History

- Stores up to 100 entries
- Consecutive duplicates are deduplicated
- Current unsaved input is preserved when entering history
- Down past the newest entry restores the unsaved input

### Scroll Behavior

Users can scroll through the conversation history in the conversation view:

| Key | Action | Scroll Amount |
|-----|--------|---------------|
| PageUp | Scroll up | 5 blocks |
| PageDown | Scroll down | 5 blocks |
| Ctrl+Home | Scroll to top | All the way |
| Ctrl+End | Scroll to bottom (pin) | All the way |

**Viewport offset** is measured in blocks from the bottom. An offset of 0
means "pinned to bottom" (the default). Scrolling up increases the offset;
scrolling down decreases it toward 0.

**Auto-pin behavior**: The viewport automatically pins to the bottom when:
- The user types any printable character
- The user presses Enter (submits a message)

This ensures the user sees new streaming content as it arrives whenever
they begin interacting with the input.

**Streaming visibility**: Streaming tokens from the active response are only
visible when the viewport is pinned to the bottom (offset = 0). When scrolled
up, the stream buffer accumulates tokens but does not render them. When the
user scrolls or auto-pins to the bottom, all accumulated tokens become visible.

**Scroll indicator**: When the viewport is not at the bottom, the last line
of the conversation view shows `↓ More content below` (dim).

**Scroll position indicator** (pattern #33): When the conversation content
exceeds the viewport height and the user scrolls up, a position indicator
appears in the bottom-right corner of the conversation view showing
`{line}/{total}` in `Theme.Semantic.Muted` (dark gray). The indicator is
hidden when the viewport is pinned to the bottom (offset = 0) or when all
content fits within the viewport. It updates on each scroll event and when
new content arrives.

**Content growth while scrolled**: When new content blocks are added to the
buffer while the user is scrolled up, the viewport offset is incremented
to maintain the user's view position. The `↓` indicator remains visible.

### Slash Command Dispatch

Lines starting with `/` are dispatched to the slash command registry:

| Category | Commands | Behavior |
|----------|----------|----------|
| Interactive List | `/project list`, `/provider list`, `/conversations list`, `/jea list`, `/agent list` | Open as Interactive List window (pattern #28) with keyboard navigation and action bar (pattern #29). Rows are navigable with Up/Down; Enter for primary action; single-letter hotkeys for secondary actions; Esc to dismiss |
| Read-only Modal | `/help`, `/project show`, `/provider show`, `/conversations show`, `/jea show`, `/jea effective`, `/context show`, `/agent show`, `/expand` | Open as modeless window over conversation view; scrollable content; Esc to dismiss |
| Interactive Dialog | `/project create`, `/project edit`, `/project delete`, `/provider setup`, `/provider remove`, `/jea create`, `/jea edit`, `/jea delete`, `/jea assign`, `/jea unassign`, `/conversations rename`, `/conversations delete`, `/context summarize`, `/context prune` | Open modal dialog for interactive prompts (form dialogs, wizards, confirmations) |
| Inline | `/context refresh`, `/conversations clear` | Execute and show result in conversation view |
| Exit | `/quit`, `/exit` | End session loop |

---

## Behavior

### Turn Activation Sequence

When the user submits a message (presses Enter with non-empty text):

1. The submitted text is added to the conversation data model.
2. The user message is added to the conversation scroll buffer as a styled
   block with muted background (see User Message Block pattern).
3. BeginTurn: Activity state set to Thinking. Agent-busy flag set.
4. The LLM request is dispatched.

### Content Rendering (Continuous)

The conversation view renders a viewport into the scroll buffer on every
refresh cycle (60fps during streaming, 100ms otherwise):

1. Calculate the conversation view height from the terminal dimensions.
2. Determine the viewport window: the blocks that fit in the available
   height, adjusted by the scroll offset.
3. If streaming is active and viewport is pinned to bottom, append the
   in-progress streaming block (escaped text with 2-space indent).
4. If scrolled up, append the `↓ More content below` indicator.
5. Compose all visible blocks into the conversation view content.
6. Refresh the display.

### Turn Deactivation Sequence

When the agent turn completes (stop_reason is `end_turn` or max rounds):

1. EndTurn: Activity state set to Idle (dim Rule).
2. Agent-busy flag cleared.
3. The input view prompt changes from dim to bold blue `>`.
4. All turn content (assistant text, tool badges, token usage) is already
   in the scroll buffer -- no flushing needed.
5. The user can immediately type their next message.

### Modal Overlay Flow

1. User submits a read-only modal slash command.
2. The command handler builds the content for the window.
3. A modeless window opens over the conversation view. The activity bar
   shows `Esc to dismiss` (dim).
4. On Esc: the window closes and the conversation view is fully visible
   again.

**During modal**: The AI continues working if a turn is active. Streaming
tokens accumulate in the stream buffer. Tool results add blocks to the
content list. When the window is dismissed, the conversation view
includes all content that arrived during the overlay.

### Interactive Dialog Flow

When the user types an interactive slash command:

1. A modal dialog opens, taking focus from the conversation view.
2. The dialog presents its interactive prompts (text fields, selection
   lists, confirmations).
3. After the user completes or cancels the dialog, focus returns to the
   conversation view.
4. Any results from the dialog are shown in the conversation view.

### Resize Handling

When the terminal is resized:

1. Terminal dimensions are re-read on each render cycle.
2. Dynamic input height is recalculated.
3. Conversation view height adjusts.
4. Content re-renders at the new dimensions.

The layout adapts automatically on the next render cycle.

### Application Shutdown

1. User types `/quit` or `/exit`.
2. The session loop ends.
3. The TUI application shuts down.
4. The terminal returns to normal operation.

---

## Edge Cases

- **Narrow terminal (< 80 columns)**: Status bar shows abbreviated metadata
  (provider + model only). Conversation text wraps at the narrow width. Tool
  preview panels wrap their content. Modal windows fill the available width.

- **Very wide terminal (> 200 columns)**: No issues. Panels expand to fill
  the width. Text does not stretch -- it remains left-aligned with the
  standard indent.

- **Terminal height < 10 rows**: Layout activation is skipped. No TUI views
  are created. The app runs in scrollback-only mode with blocking prompts
  for input. Thinking/streaming states use static messages to stderr.

- **Non-interactive/piped**: No TUI application. Input via stdin line reads,
  returns `/quit` on EOF. All output goes to stdout as plain text.

- **Modal open during streaming**: Tokens accumulate in the stream buffer.
  Conversation view "catches up" on window dismiss. User sees the response
  appear as if it arrived instantly.

- **Scroll buffer full (2000 blocks)**: Oldest blocks are silently trimmed.
  Viewport offset is decremented to maintain the user's relative position.
  The banner (first block) may be trimmed in very long sessions.

- **Scrolled up during streaming**: Streaming tokens accumulate but are not
  rendered. The `↓ More content below` indicator is visible. Typing any
  character or pressing Enter auto-pins to bottom, making the streamed
  content visible.

- **Terminal dimension read fails**: Caught. Default to 24 rows and 120
  columns.

- **Stale settings warning**: After a slash command modifies settings (e.g.,
  `/provider setup`), the status bar updates on the next render cycle.

- **Dynamic input expansion**: As the user types a long message, the input
  view grows and the conversation view shrinks. The visible conversation
  history contracts but the scroll buffer is unaffected.

- **Dialog open during streaming**: If an interactive slash command is
  triggered while the agent is streaming, the agent continues working.
  Stream buffer accumulates tokens. On dialog close, the conversation
  view displays all accumulated content.

---

## Component Patterns Used

| Pattern | Reference | Usage |
|---------|-----------|-------|
| User Message Block | 07-component-patterns.md #1 | User messages (muted background block) |
| Assistant Message Block | 07-component-patterns.md #2 | Assistant responses |
| Turn Separator | 07-component-patterns.md #3 | Between conversation turns |
| Tool Call Badge | 07-component-patterns.md #4 | Tool invocation preview |
| Tool Result Badge | 07-component-patterns.md #5 | Tool execution result |
| Modal Overlay | 07-component-patterns.md #11 | Read-only slash commands |
| Status Bar | 07-component-patterns.md #25 | Bottom metadata row |
| Activity Region | 07-component-patterns.md #26 | Spinner + state label / dim Rule |
| Streaming Text | 07-component-patterns.md #18 | LLM token streaming |
| Cancel Hint | 07-component-patterns.md #20 | Esc double-press flow |
| Interactive List | 07-component-patterns.md #28 | List slash commands with keyboard navigation |
| Action Bar | 07-component-patterns.md #29 | Shortcut hints in Interactive List windows |
| Scroll Position Indicator | 07-component-patterns.md #33 | Position display when scrolled up |
