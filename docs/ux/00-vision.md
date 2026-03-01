# UX Vision: BoydCode v2

This document is the north star for BoydCode's redesigned user experience. It
describes what the application SHOULD look like and feel like -- not what it
currently is. Every screen spec, component pattern, and style token derives from
the principles and architecture defined here.

---

## 1. Design Philosophy

Seven principles guide every decision. They are listed in priority order -- when
principles conflict, higher-numbered principles yield to lower-numbered ones.

### Principle 1: The Human is the Customer

Design for the person sitting at the keyboard. Not for the terminal emulator, not
for the POSIX tradition, not for purity of abstraction. If a GUI pattern (modal
dialog, status bar, progress indicator) serves the human better than a CLI
pattern, use it. BoydCode is a TUI -- a text user interface -- and TUIs borrow
freely from every tradition.

### Principle 2: Spectre.Console is the Rendering Engine

All visual output goes through Spectre.Console's rendering pipeline. No raw ANSI
escape sequences (`\x1b[...`). No direct `Console.Write` for visual content. The
framework handles capability detection, color depth negotiation, unicode
fallbacks, NO_COLOR compliance, and terminal width adaptation. Bypassing it means
losing all of those for free.

**What this means concretely:**
- Layout regions via `Layout` widget, not `ESC[1;{n}r` scroll regions
- In-place updates via `AnsiConsole.Live()`, not cursor save/restore
- Spinners via Spectre's `Spinner` type, not hand-rolled braille loops
- Styled text via `[markup]` tags, not `\x1b[2m` dim sequences
- Status bars via Layout regions, not cursor-positioned raw writes

### Principle 3: Non-Blocking by Default

The user should never wait for something they did not ask to wait for. This
manifests in three ways:

1. **Async input**: The user can type while the AI is thinking, streaming, or
   executing. Messages queue and process in order.
2. **Modal overlays**: Read-only slash commands (/help, /project show, /context
   show, /sessions list, /jea list) render as overlays that do not block the
   conversation. The AI continues working underneath.
3. **Streaming everything**: LLM responses stream token by token. Tool output
   streams line by line. Nothing buffers to completion before showing output.

### Principle 4: Progressive Disclosure

Show what matters now. Hide what might matter later. Reveal on demand.

- **Level 0 -- Always visible**: Input line, status bar, current message
- **Level 1 -- Visible during activity**: Thinking indicator, execution badge,
  streaming text
- **Level 2 -- On demand**: Token usage (/context), full tool output (/expand),
  session history (/sessions show)
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

---

## 2. Layout Architecture

### The Four Regions

BoydCode's screen is divided into four named regions managed by Spectre.Console's
`Layout` widget inside a `Live` display context.

```
+==============================================================================+
|                                                                              |
|  CONTENT REGION                                                              |
|                                                                              |
|  This is where the conversation lives. User messages, assistant responses,   |
|  tool execution panels, slash command results -- everything renders here.    |
|  This region takes all remaining vertical space (Ratio-based sizing).        |
|                                                                              |
|  When a modal overlay is active, this region is replaced by the modal        |
|  content. The underlying conversation state is preserved and restored        |
|  when the modal is dismissed.                                                |
|                                                                              |
+------------------------------------------------------------------------------+
|  INDICATOR BAR  (1 row, Size(1))                                             |
|  Idle: dim horizontal rule                                                   |
|  Active: spinner + "Thinking..." / "Executing..." / "Streaming..."           |
+------------------------------------------------------------------------------+
|  > INPUT LINE  (1 row, external to Live -- handled by AsyncInputReader)      |
+------------------------------------------------------------------------------+
|  STATUS BAR  (1 row, Size(1))                                                |
|  Provider | Model | Project | Branch | Engine    Esc: cancel  /help: commands |
+==============================================================================+
```

### Region Behaviors

**Content Region** (Ratio 1, minimum 5 rows)
- Renders the conversation as a composed renderable (Rows of message blocks)
- During streaming, the last message block updates in-place via Live refresh
- Shows only the tail of the conversation (most recent messages that fit)
- When a modal overlay is requested, the Content region's renderable is replaced
  with the modal Panel. The conversation data is unchanged -- only the view.
- After modal dismissal, the conversation view is restored.

**Indicator Bar** (Size 1)
- Idle state: `new Rule().RuleStyle("dim")` -- a dim horizontal line
- Thinking state: `new Markup("[yellow]@ Thinking...[/]")` with spinner character
- Streaming state: `new Markup("[cyan]@ Streaming...[/]")`
- Executing state: `new Markup("[blue]@ Executing... (2.3s)[/]")` with elapsed time
- Cancel hint state: `new Markup("[yellow]Press Esc again to cancel[/]")`
- The indicator bar is always one row. State transitions are instantaneous.

**Input Line** (not part of Layout -- external)
- Handled by `AsyncInputReader` which polls `Console.KeyAvailable` and manages
  a line buffer with cursor editing, history, and key dispatch.
- Renders below the Layout via manual cursor positioning (the one place where
  cursor control is needed -- but it is isolated to the input handler).
- Alternatively, the input line can be a Size(1) row in the Layout that renders
  a Markup of the current buffer. The AsyncInputReader updates the Markup text
  and the Live context refreshes it.

**Status Bar** (Size 1)
- Left side: `Provider | Model | Project | Branch | Engine`
- Right side: Contextual keybinding hints (adapts to current state)
- Renders as a `Grid` or `Columns` with left-aligned and right-aligned content.
- Updates when provider, project, or session changes.

### Modal Overlay System

Modals are the key innovation in BoydCode v2. They enable non-blocking slash
commands that render over the conversation without interrupting it.

**How it works:**

1. User types `/project show` while the AI is streaming a response.
2. The Content region's renderable is replaced with a Panel showing project
   details. The indicator bar shows "Modal: /project show -- Esc to dismiss".
3. The AI continues streaming in the background. Tokens accumulate in the
   conversation data model but are not visible until the modal is dismissed.
4. User presses Esc. The Content region restores the conversation view, now
   including all tokens that arrived while the modal was open.
5. The indicator bar returns to its prior state (e.g., "Streaming...").

**Which commands are modal (read-only, no prompts needed):**
- `/help` -- Show available commands
- `/project show` -- Display current project config
- `/project list` -- List all projects
- `/provider show` -- Display current provider config
- `/provider list` -- List all providers
- `/sessions list` -- List all sessions
- `/sessions show <id>` -- Display session details
- `/jea list` -- List JEA profiles
- `/jea show <name>` -- Display profile details
- `/jea effective` -- Show composed profile
- `/context show` -- Display context usage
- `/expand` -- Show last tool output

**Which commands suspend the Live context (need interactive prompts):**
- `/project create` -- Wizard with TextPrompt, SelectionPrompt
- `/project edit` -- Edit menu with SelectionPrompt
- `/project delete` -- Confirmation prompt
- `/provider setup` -- API key prompt, SelectionPrompt
- `/provider remove` -- Confirmation prompt
- `/jea create` -- TextPrompt for name
- `/jea edit` -- Edit menu with SelectionPrompt
- `/jea delete` -- Confirmation prompt
- `/jea assign` / `/jea unassign` -- SelectionPrompt
- `/sessions delete` -- Confirmation prompt

**Which commands are inline (render in the conversation flow):**
- `/clear` -- Clears conversation, shows confirmation in content area
- `/refresh` -- Refreshes context, shows confirmation in content area
- `/context compact` -- Runs compaction, shows result in content area
- `/context summarize` -- Runs summarization, shows result in content area

### Rendering Pipeline

```
User input -> AsyncInputReader -> Channel<string>
                                      |
                                      v
ChatCommand session loop -> reads from Channel
    |                           |
    |-- slash command?          |-- user message?
    |   |                       |
    |   |-- modal?              v
    |   |   Show overlay    AgentOrchestrator.RunAgentTurnAsync
    |   |                       |
    |   |-- interactive?        |-- RenderThinkingStart
    |   |   Suspend Live        |-- StreamResponseAsync
    |   |   Run prompts             |-- tokens -> conversation model
    |   |   Resume Live             |-- update Content region
    |   |                           |-- refresh Live
    |   |-- inline?             |-- ProcessToolCallsAsync
    |       Render in               |-- RenderToolExecution
    |       content area            |-- RenderExecutingStart
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

| Token       | Spectre Markup | Meaning                          | Paired With    |
|-------------|----------------|----------------------------------|----------------|
| success     | `[green]`      | Completed, allowed, ready        | Checkmark text |
| error       | `[red]`        | Failed, denied, broken           | "Error:" text  |
| warning     | `[yellow]`     | Caution, degraded, not ideal     | "Warning:" text|
| info        | `[cyan]`       | Data values, identifiers, paths  | Label text     |
| accent      | `[blue]`       | Brand, interactive, commands     | Context text   |
| muted       | `[dim]`        | Metadata, hints, secondary       | Primary text   |

Colors are NEVER used alone. Every colored element has a text or symbol companion
that conveys the same meaning without color (for NO_COLOR and colorblind users).

### Typography Hierarchy (4 levels)

| Level | Markup           | Usage                                    |
|-------|------------------|------------------------------------------|
| 1     | `[bold]`         | Headings, entity names, primary actions  |
| 2     | (plain)          | Body text, standard content              |
| 3     | `[dim]`          | Metadata, timestamps, secondary info     |
| 4     | `[dim italic]`   | Hints, ephemeral status, contextual tips |

### Status Symbols (Unicode)

| Symbol | Codepoint | Markup             | Meaning           |
|--------|-----------|--------------------|--------------------|
| check  | U+2713    | `[green]\u2713[/]` | Success/allowed    |
| cross  | U+2717    | `[red]\u2717[/]`   | Error/denied       |
| warn   | U+26A0    | `[yellow]\u26a0[/]`| Warning            |
| bullet | U+2022    | `\u2022`           | List item          |
| arrow  | U+25B6    | `[dim]\u25b6[/]`   | Indicator/pointer  |
| dash   | U+2014    | `[dim]\u2014[/]`   | Empty/not set      |

### Border Styles (3 types)

| Style               | Spectre API            | Usage                      |
|----------------------|------------------------|----------------------------|
| Rounded              | `BoxBorder.Rounded`    | Modal overlays, tool preview |
| None                 | `BoxBorder.None`       | Conversation messages      |
| Heavy (Rule)         | `new Rule().RuleStyle()`| Section separators        |

### Spacing Rules

- 1 blank line between conversation turns
- 0 blank lines within a turn (between text blocks)
- 2-space left indent for all content within the conversation
- 1 blank line before and after section dividers
- Panel internal padding: `Padding(1, 0)` (1 horizontal, 0 vertical)

---

## 4. Interaction Model

### Keyboard Navigation

| Key            | Context              | Action                          |
|----------------|----------------------|---------------------------------|
| Enter          | Input line           | Submit message / slash command   |
| Up/Down Arrow  | Input line           | Command history navigation      |
| Left/Right     | Input line           | Cursor movement within line     |
| Home/End       | Input line           | Jump to start/end of line       |
| Esc            | Modal open           | Dismiss modal overlay           |
| Esc            | AI active (1st)      | Show cancel hint in indicator   |
| Esc            | AI active (2nd)      | Cancel current operation        |
| Ctrl+C         | AI active            | Same as Esc (cancel semantics)  |
| Ctrl+C         | Idle                 | No effect (input line active)   |
| Tab            | Input line           | Reserved for future autocomplete|

### Slash Command Dispatch

All slash commands start with `/`. The first word after `/` is the command name.
Remaining words are subcommands and arguments.

```
/help                       -> Modal: show help
/project show               -> Modal: show project details
/project create             -> Suspend: wizard prompts
/context show               -> Modal: context usage
/clear                      -> Inline: clear conversation
/quit                       -> Exit session
```

### Cancellation Flow

1. User presses Esc (or Ctrl+C) while AI is active.
2. Indicator bar shows: `Press Esc again to cancel` (yellow text).
3. If user presses Esc again within 1 second: cancellation fires.
4. If 1 second passes: indicator bar reverts to prior state.
5. On cancellation: current operation stops, partial results are kept,
   conversation state is consistent, user can continue.

### Message Queue

When the AI is busy, user messages queue in a Channel. The queue count appears
in the status bar: `[2 queued]`. Messages process in FIFO order after the current
turn completes. This enables "fire and forget" workflows where the user types
several instructions and walks away.

---

## 5. Rendering Pipeline: From Raw ANSI to Spectre.Console

### What Changes

| Current (v1)                        | New (v2)                              |
|-------------------------------------|---------------------------------------|
| `ESC[1;{n}r` scroll regions        | `Layout` widget with `Ratio`/`Size`   |
| `ESC[s` / `ESC[u` cursor save      | `Live` context with `ctx.Refresh()`   |
| `ESC[{r};{c}H` cursor positioning  | `layout["Region"].Update(renderable)` |
| `ESC[2K` clear line                 | Replace renderable content            |
| `ESC[2m` / `ESC[22m` dim           | `[dim]` Spectre markup                |
| `Console.Write(braille)` spinner    | `Spinner` type in renderable          |
| `_consoleLock` for thread safety    | Live context render loop (single thread) |
| `TerminalLayout` class (553 lines)  | ~50 lines of Layout construction     |
| `ExecutionWindow` raw ANSI (499 lines) | Panel + Markup renderables         |

### What Stays

| Component                           | Reason                                |
|-------------------------------------|---------------------------------------|
| `AsyncInputReader`                  | Correct pattern for non-blocking input|
| `SpectreHelpers`                    | Consistent helper abstractions        |
| `IUserInterface` abstraction        | Clean layer boundary                  |
| Channel-based message queue         | Correct async pattern                 |
| CancellationScope double-press      | Good UX pattern                       |
| Command history (up/down)           | Expected terminal behavior            |

### Content Region Rendering Strategy

The Content region displays the conversation as a single composed renderable.
This renderable is rebuilt on each refresh cycle from the conversation data model.

```
Rows(
    // ... older messages (only as many as fit in the region height)
    UserMessageBlock("Can you add error handling to the auth module?"),
    AssistantMessageBlock(
        TextSegment("I'll examine the auth module and add error handling."),
        ToolCallBadge("Shell", "Get-Content src/Auth/AuthService.cs"),
        ToolResultBadge("Shell", "42 lines", "0.3s", isError: false),
        TextSegment("I've added try-catch blocks around the key operations..."),
    ),
    TokenUsageBar(inputTokens: 4521, outputTokens: 892),
    // Streaming in progress:
    AssistantMessageBlock(
        StreamingTextSegment("Now let me update the tests to cover the new"),
        // cursor blinks here as tokens arrive
    ),
)
```

Each message type is a composed renderable:
- **UserMessageBlock**: `[bold blue]>[/] {message text}` with 2-space indent
- **AssistantMessageBlock**: Plain text with 2-space indent, tool badges inline
- **ToolCallBadge**: Compact `[dim]Shell[/] command preview` with optional expand
- **ToolResultBadge**: `[green]\u2713[/] Shell 42 lines 0.3s` or error variant
- **TokenUsageBar**: `[dim]4,521 in / 892 out / 5,413 total[/]`
- **StreamingTextSegment**: Plain text that grows as tokens arrive

### Responsive Behavior

Three tiers based on terminal width:

| Tier     | Width      | Adaptations                               |
|----------|------------|-------------------------------------------|
| Full     | >= 120 col | Side-by-side info in status bar, full tool |
|          |            | previews, wide conversation lines          |
| Standard | 80-119 col | Stacked status info, wrapped tool previews |
| Compact  | < 80 col   | Abbreviated status, narrower margins       |

Terminal height affects how many conversation turns are visible in the Content
region. Minimum usable height is 10 rows (below this, fall back to non-layout
scrollback mode).

---

## 6. Migration Path

### Phase 1: Layout Foundation
Replace `TerminalLayout.cs` (raw ANSI scroll regions) with a Spectre.Console
`Layout` widget. Keep the same 4-zone structure but implement it through Layout
regions. Keep `AsyncInputReader` unchanged. Validate that all existing
functionality works with the new rendering.

### Phase 2: Content Rendering
Replace raw `Console.Write`/`AnsiConsole.Write` output routing with composed
renderables in the Content region. Implement UserMessageBlock and
AssistantMessageBlock as Spectre renderables. Streaming tokens update the last
AssistantMessageBlock.

### Phase 3: Modal Overlays
Implement the modal overlay system for read-only slash commands. Test that
modals can be opened and dismissed while the AI is streaming.

### Phase 4: Visual Polish
Apply the new style token system. Replace "v" with checkmark. Fix all
consistency issues from the style audit. Implement responsive tier adaptations.

### Phase 5: Execution Redesign
Replace `ExecutionWindow.cs` (raw ANSI spinner and scrolling window) with
Spectre.Console renderables that update within the Content region via the
Live context.

---

## 7. What Success Looks Like

When the redesign is complete, a user opening BoydCode should experience:

1. **Instant recognition**: The startup banner establishes identity and shows
   that the app is ready. Configuration problems are explained clearly.

2. **Fluid conversation**: Messages flow naturally. User messages are visually
   distinct from assistant messages. Tool calls are compact but informative.
   Streaming feels smooth and responsive.

3. **Non-blocking workflow**: Typing `/help` while the AI is working shows help
   immediately without interrupting the stream. Dismissing it returns to the
   conversation exactly where it was.

4. **Consistent polish**: Every color means the same thing everywhere. Every
   error explains what to do. Every success has a checkmark. The app feels
   like a cohesive product, not a collection of printf statements.

5. **Graceful degradation**: On a narrow terminal, the app adapts. In a pipe,
   it outputs clean text. With NO_COLOR, it remains readable. With a screen
   reader, it remains usable.
