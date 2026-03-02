# UX Vision: BoydCode

This document is the north star for BoydCode's user experience. It describes
what the application SHOULD look like and feel like -- not what it currently is.
Every screen spec, component pattern, and style token derives from the
principles and architecture defined here.

---

## 1. Design Philosophy

Eight principles guide every decision. They are listed in priority order -- when
principles conflict, higher-numbered principles yield to lower-numbered ones.

### Principle 1: The Human is the Customer

Design for the person sitting at the keyboard. Not for the terminal emulator, not
for the POSIX tradition, not for purity of abstraction. If a GUI pattern (modal
dialog, status bar, progress indicator) serves the human better than a CLI
pattern, use it. BoydCode is a TUI -- a text user interface -- and TUIs borrow
freely from every tradition.

### Principle 2: All Visual Output Through the Rendering Pipeline

No raw ANSI escape sequences (`\x1b[...`). No direct `Console.Write` for visual
content. All visual output flows through a two-layer rendering architecture:

- **The application shell** manages the screen. It owns the application
  lifecycle, the view hierarchy, layout, keyboard input, focus navigation, and
  windowing. It provides the four persistent screen regions, handles scrolling
  natively, and dispatches events without polling loops. Background threads post
  updates to the main thread for safe rendering.

- **The content renderer** produces rich formatted content -- styled text,
  tables, panels, rules, trees, grids -- that is displayed inside the
  application shell's views. It handles color, typography, measurement, unicode
  fallbacks, and NO_COLOR compliance.

These two layers are complementary. The shell manages *where* content appears
and *how* the user interacts with it. The renderer decides *what* that content
looks like. Bypassing either layer means losing capability for free: the shell's
event-driven input and thread-safe updates, or the renderer's styled output and
terminal adaptation.

**What this means concretely:**
- Screen regions via the view hierarchy, not `ESC[1;{n}r` scroll regions
- In-place updates via the event loop, not cursor save/restore
- Spinners via built-in spinner views, not hand-rolled braille loops
- Styled text via markup tags, not `\x1b[2m` dim sequences
- Windowed overlays via first-class window and dialog views, not cursor-positioned raw writes
- Thread-safe updates via main-thread invocation, not console locks

### Principle 3: Non-Blocking by Default

The user should never wait for something they did not ask to wait for. This
manifests in three ways:

1. **Async input**: The user can type while the AI is thinking, streaming, or
   executing. Messages queue and process in order.
2. **Windowed overlays**: Read-only slash commands (/help, /project show,
   /context show, /conversations list, /jea list) render as windows that float
   over the conversation without blocking it. The AI continues working
   underneath.
3. **Streaming everything**: LLM responses stream token by token. Tool output
   streams line by line. Nothing buffers to completion before showing output.

### Principle 4: Progressive Disclosure

Show what matters now. Hide what might matter later. Reveal on demand.

- **Level 0 -- Always visible**: Input line, status bar, current message
- **Level 1 -- Visible during activity**: Thinking indicator, execution badge,
  streaming text
- **Level 2 -- On demand**: Token usage (/context), full tool output (/expand),
  session history (/conversations show)
- **Level 3 -- Diagnostic**: Debug logs (--debug), conversation JSON (/context
  show --raw), JSONL log files

### Principle 5: Consistent Visual Language

Every color, border, symbol, and spacing choice has exactly one semantic meaning.
No ad-hoc styling. No "this looks good here" one-offs. The style token system
(06-style-tokens.md) is the law. When adding new UI, find the existing token --
do not invent a new one.

### Principle 6: Keyboard-First, Accessible Always

Every action is reachable via keyboard. Mouse is not required. Screen readers can
follow the conversation. NO_COLOR disables all color but preserves all
information. Accessible mode replaces animations with static text.

### Principle 7: Speed is Felt, Not Measured

A 200ms operation that shows instant feedback feels faster than a 50ms operation
that shows nothing. Every user action produces visible feedback within 100ms.
Long operations get progress indication. The render loop targets 60fps for
streaming content.

### Principle 8: Interactive by Default

List views are interactive with keyboard navigation. When users see a list of
projects, conversations, or profiles, they can navigate to an item and act on it
directly -- open, edit, delete, rename -- without reading output and typing
follow-up commands. Static text output is reserved for non-actionable
informational displays and piped/non-TUI fallback.

This principle extends the conversation/tool content distinction:
**Conversation content** (user messages, assistant responses, tool badges,
streaming text, token usage) stays in the conversation view and becomes part of
the permanent conversation history. **Tool content** (help, entity lists, detail
views, context dashboards, expanded output) opens in separate windows or dialogs
and is ephemeral -- dismissed when the user is done, never polluting the
conversation.

---

## 2. Layout Architecture

### The Four Regions

BoydCode's screen is divided into four persistent regions that remain visible
for the entire session.

```
+==============================================================================+
|                                                                              |
|  CONVERSATION                                                                |
|                                                                              |
|  Scrollable conversation history. User messages, assistant responses, tool   |
|  execution badges, streaming text -- everything that IS the conversation     |
|  renders here. This region takes all remaining vertical space.               |
|                                                                              |
|  The user can scroll back through the full conversation history at any       |
|  time. New content auto-scrolls to the bottom.                               |
|                                                                              |
+------------------------------------------------------------------------------+
|  ACTIVITY BAR  (1 row)                                                       |
|  Idle: dim horizontal rule                                                   |
|  Active: spinner + "Thinking..." / "Executing..." / "Streaming..."           |
+- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -+
|  INPUT SEPARATOR  (1 row, dim rule)                                          |
+------------------------------------------------------------------------------+
|  > INPUT  (1 row)                                                            |
|  Text input with line editing, command history, and key dispatch             |
+------------------------------------------------------------------------------+
|  STATUS BAR  (1 row)                                                         |
|  Provider | Model | Project | Branch | Engine    Esc: cancel  /help: commands |
+==============================================================================+
```

### Region Behaviors

**Conversation** (all remaining vertical space)
- Scrollable view of the full conversation history, not just the tail that fits.
  The user can scroll up to review earlier messages at any time.
- During streaming, new tokens append at the bottom and the view auto-scrolls
  to follow.
- Content is composed from message blocks: user messages, assistant text, tool
  call badges, tool result badges, token usage, and streaming text.
- This view shows ONLY conversation content. Non-conversation output (help,
  agent lists, JEA profiles, project details) opens in separate windows -- never
  injected into the conversation.

**Activity Bar** (1 row)
- Idle state: a dim horizontal rule.
- Thinking state: spinner + "Thinking..." in yellow.
- Streaming state: spinner + "Streaming..." in cyan.
- Executing state: spinner + "Executing... (2.3s)" in cyan with elapsed time.
- Cancel hint state: "Press Esc again to cancel" in yellow.
- State transitions are instantaneous. The bar is always exactly one row.

A dim horizontal rule separates the Activity Bar from the Input region.
This is a structural element (InputSeparatorView) that provides visual
separation without semantic content.

**Input** (1 row)
- Text input with cursor editing, command history (Up/Down), and Home/End
  navigation.
- The user can type at any time, including while the AI is working. Messages
  queue and process in order.
- Horizontal scrolling with directional indicators when text exceeds the
  visible width.

**Status Bar** (1 row)
- Left side: Provider | Model | Project | Branch | Engine.
- Right side: Contextual keybinding hints (adapts to current state).
- Updates when provider, project, or session changes.

### Windowing System

Windows are the mechanism for displaying non-conversation content without
polluting the conversation view. They float over the conversation and can be
dismissed independently.

**How it works:**

1. User types `/project show` while the AI is streaming a response.
2. A window opens over the conversation, showing project details. The window
   title identifies the command.
3. The AI continues streaming in the background. Tokens accumulate in the
   conversation and are visible when the window is dismissed.
4. User presses Esc. The window closes and the conversation view is fully
   visible again.

**Which commands open interactive list windows (modeless, keyboard navigation):**
- `/project list` -- Navigate, open, edit, delete projects (pattern #28)
- `/provider list` -- Navigate, show, setup, remove providers (pattern #28)
- `/conversations list` -- Navigate, open, rename, delete conversations (pattern #28)
- `/jea list` -- Navigate, show, edit, delete profiles (pattern #28)
- `/agent list` -- Navigate, show agent details (pattern #28)

**Which commands open read-only windows (modeless, Esc to dismiss):**
- `/help` -- Show available commands
- `/project show` -- Display current project config
- `/provider show` -- Display current provider config
- `/conversations show <id>` -- Display session details
- `/jea show <name>` -- Display profile details
- `/jea effective` -- Show composed profile
- `/agent show <name>` -- Display agent details
- `/context show` -- Display context usage
- `/expand` -- Show last tool output

**Which commands open interactive dialogs (modal, blocks until complete):**
- `/project create` -- Wizard with text fields and selection lists
- `/project edit` -- Edit menu with selection list
- `/project delete` -- Confirmation dialog
- `/provider setup` -- API key input, selection list
- `/provider remove` -- Confirmation dialog
- `/jea create` -- Text input for name
- `/jea edit` -- Edit menu with selection list
- `/jea delete` -- Confirmation dialog
- `/jea assign` / `/jea unassign` -- Selection list
- `/conversations delete` -- Confirmation dialog
- `/conversations rename` -- Text input for new name
- `/context summarize` -- Four-option menu
- `/context prune` -- Confirmation of pruning boundaries

**Which commands are inline (render in the conversation flow):**
- `/conversations clear` -- Clears conversation, shows confirmation in conversation
- `/context refresh` -- Refreshes context, shows confirmation in conversation
- `/context summarize` -- Runs summarization, shows result in conversation

### Input Processing

```
User input -> Input view -> message queue
                                |
                                v
ChatCommand session loop -> reads from queue
    |                           |
    |-- slash command?          |-- user message?
    |   |                       |
    |   |-- read-only?          v
    |   |   Open window     AgentOrchestrator.RunAgentTurnAsync
    |   |                       |
    |   |-- interactive?        |-- RenderThinkingStart
    |   |   Open dialog         |-- StreamResponseAsync
    |   |                           |-- tokens -> conversation model
    |   |                           |-- append to conversation view
    |   |-- inline?             |-- ProcessToolCallsAsync
    |       Render in               |-- RenderToolExecution
    |       conversation            |-- RenderExecutingStart
    |                               |-- output lines -> model
    |                               |-- RenderToolResult
    v                           |-- RenderStreamingComplete
(loop)                          |-- Token usage -> model
                                v
                            (next round or end_turn)
```

---

## 3. Visual Language

The complete visual language is defined in `06-style-tokens.md`. This section
provides the summary.

### Color Palette (6 semantic colors)

| Token       | Meaning                          | Paired With    |
|-------------|----------------------------------|----------------|
| success     | Completed, allowed, ready        | Checkmark text |
| error       | Failed, denied, broken           | "Error:" text  |
| warning     | Caution, degraded, not ideal     | "Warning:" text|
| info        | Data values, identifiers, paths  | Label text     |
| accent      | Brand, interactive, commands     | Context text   |
| muted       | Metadata, hints, secondary       | Primary text   |

Colors are NEVER used alone. Every colored element has a text or symbol companion
that conveys the same meaning without color (for NO_COLOR and colorblind users).

### Typography Hierarchy (4 levels)

| Level | Style            | Usage                                    |
|-------|------------------|------------------------------------------|
| 1     | Bold             | Headings, entity names, primary actions  |
| 2     | Plain            | Body text, standard content              |
| 3     | Dim              | Metadata, timestamps, secondary info     |
| 4     | Dim italic       | Hints, ephemeral status, contextual tips |

### Status Symbols (Unicode)

| Symbol | Codepoint | Meaning           |
|--------|-----------|-------------------|
| check  | U+2713    | Success/allowed   |
| cross  | U+2717    | Error/denied      |
| warn   | U+26A0    | Warning           |
| bullet | U+2022    | List item         |
| arrow  | U+25B6    | Indicator/pointer |
| dash   | U+2014    | Empty/not set     |

### Border Styles (3 types)

| Style    | Usage                                |
|----------|--------------------------------------|
| Rounded  | Windows, dialogs, tool preview panel |
| None     | Conversation messages                |
| Rule     | Section separators                   |

### Spacing Rules

- 1 blank line between conversation turns
- 0 blank lines within a turn (between text blocks)
- 2-space left indent for all content within the conversation
- 1 blank line before and after section dividers
- Panel internal padding: 1 character horizontal, 0 vertical

---

## 4. Interaction Model

### Keyboard Navigation

| Key            | Context              | Action                          |
|----------------|----------------------|---------------------------------|
| Enter          | Input line           | Submit message / slash command   |
| Up/Down Arrow  | Input line           | Command history navigation      |
| Left/Right     | Input line           | Cursor movement within line     |
| Home/End       | Input line           | Jump to start/end of line       |
| Esc            | Window open          | Dismiss window                  |
| Esc            | AI active (1st)      | Show cancel hint in activity bar|
| Esc            | AI active (2nd)      | Cancel current operation        |
| Ctrl+C         | AI active            | Same as Esc (cancel semantics)  |
| Ctrl+C         | Idle                 | No effect (input line active)   |
| Tab            | Input line           | Reserved for future autocomplete|

### Slash Command Dispatch

All slash commands start with `/`. The first word after `/` is the command name.
Remaining words are subcommands and arguments.

```
/help                       -> Window: show help
/project show               -> Window: show project details
/project create             -> Dialog: wizard prompts
/context show               -> Window: context usage
/conversations clear        -> Inline: clear conversation
/quit                       -> Exit session
```

### Cancellation Flow

1. User presses Esc (or Ctrl+C) while AI is active.
2. Activity bar shows: `Press Esc again to cancel` (yellow text).
3. If user presses Esc again within 1 second: cancellation fires.
4. If 1 second passes: activity bar reverts to prior state.
5. On cancellation: current operation stops, partial results are kept,
   conversation state is consistent, user can continue.

### Message Queue

When the AI is busy, user messages queue. The queue count appears in the status
bar: `[2 queued]`. Messages process in FIFO order after the current turn
completes. This enables "fire and forget" workflows where the user types several
instructions and walks away.

---

## 5. What Success Looks Like

When the redesign is complete, a user opening BoydCode should experience:

1. **Instant recognition**: The startup banner establishes identity and shows
   that the app is ready. Configuration problems are explained clearly.

2. **Fluid conversation**: Messages flow naturally. User messages are visually
   distinct from assistant messages. Tool calls are compact but informative.
   Streaming feels smooth and responsive.

3. **Non-blocking workflow**: Typing `/help` while the AI is working shows help
   immediately in a floating window without interrupting the stream. Dismissing
   it returns to the conversation exactly where it was.

4. **Consistent polish**: Every color means the same thing everywhere. Every
   error explains what to do. Every success has a checkmark. The app feels
   like a cohesive product, not a collection of printf statements.

5. **Graceful degradation**: On a narrow terminal, the app adapts. In a pipe,
   it outputs clean text. With NO_COLOR, it remains readable. With a screen
   reader, it remains usable.
