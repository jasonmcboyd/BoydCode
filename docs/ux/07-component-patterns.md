# Component Patterns (Prescriptive)

This document defines the REQUIRED component library for BoydCode v2. Every
reusable UI pattern is specified here with ASCII mockups, usage guidance,
rendering notes, and accessibility considerations.

All mockups assume 80-column terminal unless labeled "120 columns."

References: `06-style-tokens.md` for colors, weights, symbols, spacing.
References: `00-vision.md` for architecture and principles.

---

## Table of Contents

1.  [User Message Block](#1-user-message-block)
2.  [Assistant Message Block](#2-assistant-message-block)
3.  [Turn Separator](#3-turn-separator)
4.  [Tool Call Badge](#4-tool-call-badge)
5.  [Tool Result Badge](#5-tool-result-badge)
6.  [Execution Progress](#6-execution-progress)
7.  [Status Message](#7-status-message)
8.  [Section Divider](#8-section-divider)
9.  [Info Grid](#9-info-grid)
10. [Simple Table](#10-simple-table)
11. [Modal Overlay](#11-modal-overlay)
12. [Selection Prompt](#12-selection-prompt)
13. [Text Prompt](#13-text-prompt)
14. [Confirmation Prompt](#14-confirmation-prompt)
15. [Delete Confirmation](#15-delete-confirmation)
16. [Edit Menu Loop](#16-edit-menu-loop)
17. [Token Usage Display](#17-token-usage-display)
18. [Streaming Text](#18-streaming-text)
19. [Thinking Indicator](#19-thinking-indicator)
20. [Cancel Hint](#20-cancel-hint)
21. [Empty State](#21-empty-state)
22. [Error Display](#22-error-display)
23. [Crash Panel](#23-crash-panel)
24. [Banner](#24-banner)
25. [Status Bar](#25-status-bar)
26. [Activity Region](#26-activity-region-formerly-indicator-bar)
27. [Context Usage Bar](#27-context-usage-bar)

---

## 1. User Message Block

### Description

Renders the user's message in the conversation. Visually distinct from
assistant messages through a subtle grey23 background tint and `>` prefix.
Rendered as a borderless Panel for the background effect.

### Mockup (80 columns)

```
 > Can you add error handling to the auth module?
```

The entire line has a `Color.Grey23` background. The `>` prefix is plain text
(not colored). The padding creates 1-character horizontal margins.

### Mockup (120 columns)

Same format. User messages do not change with width.

### When to Use

Every user message in the conversation. User messages are appended to the
conversation view immediately when submitted.

### Echo Behavior

When the user submits a message, it is immediately appended to the
conversation view as a borderless panel with grey23 background, then the
activity bar transitions to the Thinking state.

### Rendering

The user message is rendered as a borderless panel with 1-character
horizontal padding and a `Color.Grey23` background. The `>` prefix and
message text use plain (Level 2) style. The message text is escaped before
rendering to prevent markup injection.

The grey23 background is an intentional exception to the ANSI 4-bit color rule
(see 06-style-tokens.md Section 1.6). It provides a subtle blockquote-style
tint that distinguishes user messages from assistant text without using borders
or colored text.

### Style Tokens

- `Color.Grey23` background (see 06-style-tokens.md Section 1.6)
- Level 2 (plain) for the `>` prefix and message text
- `BoxBorder.None` with `Padding(1, 0, 1, 0)` (1 horizontal, 0 vertical)
- No color on the `>` prefix -- it is plain text

### Accessibility

- The `>` symbol identifies user messages without color or background
- Screen readers see: `> {message text}` which is clear
- In NO_COLOR mode, the background tint is not rendered; the `>` prefix
  provides the visual distinction
- In accessible mode, rendered as plain `> {text}` with no background

---

## 2. Assistant Message Block

### Description

Renders the assistant's response text. No border, no background. Distinguished
from user messages by the absence of the `>` indicator and by 2-space indent.

### Mockup (80 columns)

```
  I can see the project structure. Let me examine the main
  configuration file to understand the current settings,
  then I'll make the changes you requested.
```

### When to Use

Every assistant text response. May contain multiple paragraphs. Tool calls
and results render as separate inline components (see patterns 4 and 5).

### Rendering

Rendered as a borderless panel with word-wrapping and 2-space left indent.
No visible borders. The assistant text is escaped before rendering.

### Style Tokens

- Level 2 (plain) for text
- Level 1 indent (2 spaces)
- `BoxBorder.None`

---

## 3. Turn Separator

### Description

Visual separator between conversation turns (user message -> assistant response
cycle). Includes inline token usage when available.

### Mockup (80 columns)

```
  [dim]4,521 in / 892 out / 5,413 total[/]
```

Between turns, a blank line provides visual breathing room. The token usage
display (if present) acts as the separator between the assistant's response
and the next user message.

### Mockup -- Without Token Usage

When no token usage is available (e.g., after a slash command), a simple
blank line separates turns.

### When to Use

After every completed assistant turn. Not between tool calls within the same
turn (those are continuous).

### Rendering

Dim text with 2-space indent, formatted as:
`[dim]{inputTokens} in / {outputTokens} out / {totalTokens} total[/]`

### Style Tokens

- Level 3 (dim) for all token text
- Level 1 indent (2 spaces)

---

## 4. Tool Call Badge

### Description

Compact inline display of a tool invocation. Shows the tool name and a
formatted preview of the command/arguments in a bordered panel.

### Mockup (80 columns)

```
  +- Shell -------------------------------------------------+
  | Get-ChildItem -Path src/ -Recurse -Filter *.cs           |
  +----------------------------------------------------------+
```

### Mockup -- Multi-line command (80 columns)

```
  +- Shell -------------------------------------------------+
  | Set-Location src/                                        |
  | dotnet build                                             |
  | dotnet test --no-build                                   |
  +----------------------------------------------------------+
```

### Mockup (120 columns)

Same structure, panel stretches to fill available width.

### When to Use

Every tool call emitted by the LLM. Renders before execution begins.

### Rendering

Rendered as a rounded-border panel that expands to fill available width.
The tool name appears in the panel header (dim style). The command content
is escaped and displayed in plain text. Horizontal padding of 1 character.

### Style Tokens

- Rounded border with grey border color
- `[dim]` for the header (tool name)
- Level 2 (plain) for command content
- `Padding(1, 0)`

---

## 5. Tool Result Badge

### Description

Compact one-line summary of a completed tool execution. Shows success/error
status, tool name, output line count, and duration.

### Mockup -- Success (80 columns)

```
  [green]\u2713[/] [dim]Shell  42 lines | 0.3s[/]
```

### Mockup -- Success with expand hint

```
  [green]\u2713[/] [dim]Shell  42 lines | 0.3s[/]
  [dim italic]/expand to show full output[/]
```

### Mockup -- Error

```
  [red]\u2717[/] [dim]Shell  12 lines | 1.1s[/]
```

### Mockup -- No output

```
  [green]\u2713[/] [dim]Shell  Command completed successfully.[/]
```

### When to Use

After every tool execution completes.

### Rendering

A single line of styled text with 2-space indent:
- Success: `  [green]`checkmark`[/] [dim]{toolName}  {lineCount} lines | {duration}[/]`
- Error: `  [red]`cross`[/] [dim]{toolName}  {lineCount} lines | {duration}[/]`

The tool name is escaped before rendering.

### Style Tokens

- `[green]` + checkmark (success) or `[red]` + cross (error)
- Level 3 (dim) for metadata
- Level 4 (dim italic) for expand hint
- Level 1 indent (2 spaces)

---

## 6. Execution Progress

### Description

In-place indicator shown while a tool is executing. Appears in the activity
bar (not the conversation view) so it does not scroll away.

### Mockup

The activity bar shows:

```
[cyan]⠿ Executing... (2.3s)[/]
```

A braille spinner animates at 100ms per frame. The elapsed time updates each
frame alongside the spinner character. This uses the same spinner as the
Thinking and Streaming states (see Activity Region, pattern #26).

### When to Use

Between `RenderExecutingStart()` and `RenderExecutingStop()`.

### Rendering

The activity bar is a single-row region. During execution, the spinner
cycles through 8 braille frames at 100ms intervals, updating the elapsed
time on each frame. The spinner frame advances by 1 every 100ms.

### Style Tokens

- `[cyan]` (info) for executing state
- Braille spinner (8-frame, 100ms/frame) as animation
- Frame sequence: ⠿ ⠻ ⠽ ⠾ ⠷ ⠯ ⠟ ⠾

### Accessibility

In accessible mode, render as static text: `[Executing... (2.3s)]`. No
spinner animation. Update elapsed time on each refresh cycle.

---

## 7. Status Message

### Description

Single-line messages that communicate operation results. The most common output
pattern -- every user action should produce one.

### Mockup

```
  [green]\u2713[/] Project my-project created.                    (success)
[red bold]Error:[/] Could not connect to the API.                 (error)
[yellow]![/] [yellow]Warning:[/] Context is 85% full.             (warning)
[yellow]Usage:[/] /project create <name>                          (usage)
[dim]Session auto-saved.[/]                                       (dim)
[dim]Cancelled.[/]                                                (cancelled)
```

### When to Use

- After any user-initiated action completes
- To report errors, warnings, or validation failures
- To show secondary metadata or hints

### Rendering

Status messages are rendered via the user interface's status helper methods,
which handle escaping internally. Pass plain text -- never pre-escape.

### Style Tokens

See 06-style-tokens.md Section 7.1 for exact formats.

---

## 8. Section Divider

### Description

Named horizontal rule that separates content sections within modal overlays
or slash command output.

### Mockup (80 columns)

```

-- Directories -----------------------------------------------
```

### When to Use

To separate named sections within slash command output (project details,
JEA profile views, context breakdown).

### Rendering

A blank line followed by a left-justified horizontal rule with bold title
text and dim rule line style.

### Style Tokens

- `[bold]` for section title
- `[dim]` for rule line style
- 1 blank line above

---

## 9. Info Grid

### Description

Two-column or four-column key-value display for metadata. Labels are dim,
values are cyan.

### Mockup (80 columns)

```
  Provider  Gemini          Project  my-project
  Model     gemini-2.5-pro  Engine   InProcess
  cwd       C:\Users\jason\source\repos\my-project
```

### Mockup (120 columns)

```
  Provider  Gemini                        Project  my-project
  Model     gemini-2.5-pro                Engine   InProcess
  cwd       C:\Users\jason\source\repos\my-project
  Git       C:\Users\jason\source\repos\my-project (main)
```

### When to Use

Startup banner, `/project show`, `/provider show`.

### Rendering

A 4-column grid with dim labels and cyan values. When a row has only one
key-value pair (e.g., a long path), it spans both columns. The grid has
2-space left indent.

### Style Tokens

- `[dim]` for labels
- `[cyan]` (info) for values
- Level 1 indent via `PadLeft(2)`

---

## 10. Simple Table

### Description

Standard data table for listing items. Simple border, bold headers, clean cells.

### Mockup (80 columns)

```
  Name            Dirs  Docker    Engine
 -----------------------------------------------
  my-project      3     python    InProcess
  api-service     1     --        Container
  _default        0     --        InProcess
```

### When to Use

`/project list`, `/jea list`, `/conversations list`, `/provider list`.

### Rendering

A simple-bordered table with bold column headers. Empty cells display a dim
em-dash (`[dim]\u2014[/]`).

### Style Tokens

- `TableBorder.Simple`
- `[bold]` for headers (applied by SimpleTable)
- `[dim]\u2014[/]` for empty cells

---

## 11. Modal Overlay

### Description

A bordered window that overlays the conversation to show slash command output
without blocking it. The window appears instantly, can be dismissed with Esc,
and restores the conversation view on dismissal.

This is the KEY NEW PATTERN in BoydCode v2. It enables non-blocking access
to configuration and information while the AI continues working.

### Mockup (80 columns) -- /help

```
+-- Help ---------------------------------------------------+
|                                                            |
|  /quit                   Exit the session (also: /exit)    |
|  /project                Manage projects                   |
|    create [name]           Create a new project            |
|    list                    List all projects               |
|    ...                                                     |
|  /provider               Manage LLM providers              |
|    ...                                                     |
|  /jea                    Manage JEA profiles               |
|    ...                                                     |
|  /context                View and manage conversation ...  |
|    ...                                                     |
|  /conversations          Manage conversations and ...      |
|    ...                                                     |
|  /expand                 Show full output from the ...     |
|  /agent                  Manage agent definitions          |
|    ...                                                     |
|  /help                   Show available commands           |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

### Mockup -- /project show

The window is sized to fit its content and centered on screen:

```
+-- my-project ------------------------------------------+
|                                                         |
|  Provider  Gemini          Engine   InProcess            |
|  Docker    python:3.12     Branch   main                |
|                                                         |
|  -- Directories ---                                     |
|  Path                          Access                   |
|  C:\Users\jason\src\app        ReadWrite                |
|  C:\Users\jason\src\lib        ReadOnly                 |
|                                                         |
|  -- System Prompt ---                                   |
|  You are a Python expert working on the app project.    |
|                                                         |
|  Esc to dismiss                                         |
|                                                         |
+---------------------------------------------------------+
```

### When to Use

Read-only slash commands that do not need interactive prompts:
- `/help`
- `/project show`
- `/provider show`
- `/conversations show <id>`
- `/jea show <name>`, `/jea effective`
- `/agent show <name>`
- `/context show`
- `/expand`

### Behavior

1. User triggers a modal slash command (via message queue or direct input).
2. A modeless window opens over the conversation view, showing the content.
3. The activity bar shows `[dim]Esc to dismiss[/]`.
4. The AI continues working in the background -- tokens accumulate in the
   conversation model but are not visible while the window is open.
5. User presses Esc (or Enter).
6. The window closes. The conversation view is fully visible again,
   including any tokens that arrived while the window was open.
7. The activity bar returns to its prior state.

### Rendering

`ShowModal(title, content)` opens a Terminal.Gui `Window` overlay centered on
screen and sized to fit its content. The window width and height are computed
from the content string (longest line + chrome, line count + chrome), capped
at 90% of the terminal dimensions. If the content exceeds available space,
the `TextView` scrolls.

Inside the window, a read-only `TextView` displays the content with 1-char
offset from each edge (`X = 1`, `Width = Dim.Fill(1)`), giving 2 characters
of total horizontal padding (1 from the window border + 1 from the offset).
The window border provides 1 character of vertical padding.

`ShowModal` appends `"\n\nEsc to dismiss"` to every modal's content.

### Style Tokens

References `06-style-tokens.md`:

- **Border**: Rounded style, accent (blue) color (see 5.1, 5.2)
- **Padding**: 2-char horizontal, 1-char vertical (see 4.3)
- **Sizing**: Sized to content, centered, capped at 90% of terminal
- **Dismiss hint**: "Esc to dismiss" appended to content
- **Content**: Read-only `TextView` (scrollable if content exceeds window)

### Accessibility

In accessible mode, modals render as delimited blocks:

```
=== Help ===
Command          Description
/help            Show this help
...
=== End Help ===
```

### Interaction with Streaming

When a window is open and the AI is streaming:
- Tokens accumulate in the conversation model
- The window covers the conversation view; streaming text is not visible
- When dismissed, the conversation view shows all accumulated text
- The user sees the conversation "jump forward" to the current state
- This is intentional -- the window is a lens over the data, not a pause button

---

## 12. Selection Prompt

### Description

Interactive list for choosing one option. Uses vim-style navigation (j/k in
addition to arrows).

### Mockup

```
  Choose a [green]template[/]:
  > console
    webapi
    classlib
```

### When to Use

Any choice from a list of 3-10 options. For 2 options, consider Confirmation
Prompt instead. For 10+ options, consider grouping or search.

### Rendering

Presented as a selection list with green highlight on the selected item.
The prompt title uses `[green]` markup for the field name.

### Style Tokens

- Green highlight for selected item
- `[green]` in prompt title for the field name

### Note on Interactive Prompts

Interactive selection prompts open as modal dialogs that block until the
user makes a selection. This is why `/project create` uses a modal dialog.

---

## 13. Text Prompt

### Description

Free-text input with optional validation, default values, and hints.

### Mockup

```
  Project [green]name[/]: my-project
```

### Mockup -- With hint

```
  Docker image [dim](Enter to skip)[/]: python:3.12
```

### When to Use

Named text input during wizard flows.

### Rendering

Text input with label. Three variants:
- **Required**: Validates non-empty input before accepting.
- **Optional**: Accepts empty input (Enter to skip).
- **With default**: Pre-fills a default value; Enter accepts the default.

### Style Tokens

- `[green]` for field name
- `[dim]` for hints and default values

---

## 14. Confirmation Prompt

### Description

Yes/no question for non-destructive confirmations.

### Mockup

```
  Save changes? [y/N]: y
```

### When to Use

Before applying changes that are easily reversible.

### Rendering

A yes/no prompt. The default value determines which option is capitalized
in the `[y/N]` or `[Y/n]` hint.

---

## 15. Delete Confirmation

### Description

Two-step confirmation for destructive operations. Shows what will be deleted,
then asks for confirmation.

### Mockup (80 columns)

```

-- Delete Project -------------------------------------------
  [dim]\u2022[/] Name: [bold]my-project[/]
  [dim]\u2022[/] Directories: 3
  [dim]\u2022[/] Custom prompt: Yes
  [dim]\u2022[/] Docker image: python:3.12

  Delete project [bold]my-project[/]? [y/N]: y
  [green]\u2713[/] Project [bold]my-project[/] deleted.
```

### When to Use

`/project delete`, `/conversations delete`, `/jea delete`, `/provider remove`.

### Rendering

Section divider + bulleted detail list + Confirm prompt. Presented as a
modal dialog that blocks until the user confirms or cancels.

### Style Tokens

- Section divider pattern
- `[dim]\u2022[/]` for detail bullets
- `[bold]` for entity names
- Confirmation prompt pattern

---

## 16. Edit Menu Loop

### Description

Repeated selection prompt that presents editable fields and returns to the
menu after each edit. "Done" option exits the loop.

### Mockup

```
  Edit [bold]my-project[/]:
  > Name               my-project
    System prompt      You are a Python expert...
    Docker image       python:3.12
    Directories        3 configured
    Done

  Project [green]name[/]: new-name
  [green]\u2713[/] Name updated.

  Edit [bold]new-name[/]:
  > Name               new-name
    ...
```

### When to Use

`/project edit`, `/jea edit`.

### Rendering

A repeating selection prompt loop. After each field edit, the menu
re-displays with updated values. Selecting "Done" exits the loop.
Presented as a modal dialog.

### Style Tokens

- Selection prompt pattern
- Status message pattern for confirmations
- `[bold]` for entity name in title

---

## 17. Token Usage Display

### Description

Cumulative token count display showing input, output, and total tokens for
the conversation.

### Mockup

```
  [dim]4,521 in / 892 out / 5,413 total[/]
```

### When to Use

After each LLM response (each round in a multi-round agentic turn).

### Rendering

Dim text with 2-space indent, formatted as:
`  [dim]{input} in / {output} out / {total} total[/]`

Numbers are formatted with thousand separators.

### Style Tokens

- Level 3 (dim) for all text
- Numbers formatted with thousand separators
- Level 1 indent (2 spaces)

---

## 18. Streaming Text

### Description

Token-by-token rendering of the LLM response as it arrives. Text accumulates
left-to-right with word wrapping.

### Mockup -- In progress

```
  I can see the project structure. Let me examine the main
  configuration file to understand the current se|
```

The cursor position is implicit -- tokens append at the end.

### Mockup -- Complete

```
  I can see the project structure. Let me examine the main
  configuration file to understand the current settings,
  then I'll make the changes you requested.

  [dim]4,521 in / 892 out / 5,413 total[/]
```

### When to Use

Every streaming LLM response.

### Rendering

As each token arrives, it is appended to a text buffer. The conversation
view re-renders with the growing text and auto-scrolls to keep the latest
content visible. The streaming block is always the last element in the
conversation view.

### Style Tokens

- Level 2 (plain) for streamed text rendered as `Markup`
- Level 1 indent (2 spaces) prepended to the escaped text
- No additional markup on streamed text (it is LLM output, escaped before render)

### Performance

- Rate-limit screen updates to ~60fps (16ms minimum between refreshes)
- Cache finalized message renderables
- Only rebuild the streaming portion on each token
- The conversation view uses its full height for visible content calculation

---

## 19. Thinking Indicator

### Description

Shown when the LLM request has been sent but no response tokens have arrived.
Renders in the activity bar, not in the conversation view.

### Mockup

The activity bar shows:

```
⠿ Thinking...
```

In yellow text. The braille character animates at 100ms per frame (8-frame
cycle), the same spinner used for all active states. When the first token
arrives, the activity bar transitions to Streaming.

### When to Use

Between `RenderThinkingStart()` and the first token or `RenderThinkingStop()`.

### Rendering

The activity bar displays yellow text with the braille spinner character
and "Thinking..." label. The spinner animates at 100ms per frame. When the
first response token arrives, the activity bar transitions to Streaming.

### Style Tokens

- `[yellow]` (warning/attention) for thinking state
- Braille spinner (8-frame, 100ms/frame) -- same as Executing and Streaming

### Accessibility

In accessible mode: `[Thinking...]` (static text, no animation).

---

## 20. Cancel Hint

### Description

Feedback shown after the first Esc/Ctrl+C press during agent activity.
Appears in the activity bar and auto-dismisses after 1 second.

### Mockup

The activity bar shows:

```
Press Esc again to cancel
```

In yellow text. Replaces the spinner that was in the activity bar.

### When to Use

First Esc/Ctrl+C press during thinking, streaming, or execution.

### Behavior

1. First press: Activity bar shows cancel hint (yellow text, no spinner).
2. Second press within 1 second: Cancellation fires.
3. Timeout (1 second): Activity bar reverts to prior state (with spinner).

### Rendering

The activity bar shows yellow text: "Press Esc again to cancel" (no
spinner). After 1 second, the activity bar reverts to its prior state.

### Style Tokens

- `[yellow]` for cancel hint text

---

## 21. Empty State

### Description

Explicit message when a list or collection has no items. Never show an empty
table -- always explain why there is nothing to show.

### Mockup

```
  [dim]No projects configured. Create one with /project create[/]
```

```
  [dim]No sessions found.[/]
```

### When to Use

Any list, table, or collection that might be empty.

### Rendering

Dim text with 2-space indent. The message text is escaped before rendering.

### Style Tokens

- Level 3 (dim) for empty state text
- Include actionable guidance when possible ("Create one with...")

---

## 22. Error Display

### Description

Error messages that explain what happened, why, and what the user can do.

### Mockup -- Simple error

```
[red bold]Error:[/] Could not connect to the API.
```

### Mockup -- Error with suggestion

```
[red bold]Error:[/] Could not connect to the API.
  [yellow]Suggestion:[/] [dim]Check your network connection or verify
  the API key with /provider setup[/]
```

### Mockup -- Error with multiple suggestions

```
[red bold]Error:[/] Authentication failed.
  Try:
  [dim]\u2022 Check your API key with /provider show[/]
  [dim]\u2022 Re-authenticate with /provider setup[/]
  [dim]\u2022 Run with --verbose for detailed logs[/]
```

### When to Use

All error conditions. Every error MUST include actionable guidance.

### Rendering

Simple errors are rendered via the user interface's error method, which
handles escaping internally. Errors with suggestions append the suggestion
text on the next line with 2-space indent.

### Style Tokens

- `[red bold]` for "Error:" prefix
- Level 2 (plain) for error body
- `[yellow]` for "Suggestion:" prefix
- Level 3 (dim) for suggestion text

---

## 23. Crash Panel

### Description

Unrecoverable error display when the application crashes. Shows a bordered
panel with error details and log file path.

### Mockup (80 columns)

```
+-- boydcode crash ----------------------------------------+
|                                                          |
|  An unexpected error occurred.                           |
|                                                          |
|  NullReferenceException: Object reference not set to     |
|  an instance of an object.                               |
|                                                          |
|  Log: ~/.boydcode/logs/crash-2026-02-27.log              |
|                                                          |
+----------------------------------------------------------+
```

### When to Use

Top-level exception handler in `Program.cs`.

### Rendering

A rounded-border panel with red border and red bold header text. The panel
displays the error message and log file path. Padding of 1 character on all
sides. This renders outside the TUI application (after it has been disposed
or before it starts), so it writes directly to the terminal.

### Style Tokens

- `BoxBorder.Rounded` with `Color.Red` border (default Rounded, red color)
- `[red bold]` for header
- `Padding(1, 1, 1, 1)`

---

## 24. Banner

### Description

Startup display showing brand identity, session configuration, and readiness
status. Renders once at startup and becomes part of the scrollable
conversation history.

### Mockup (120 columns, full height)

```

  [bold cyan]  ██████╗  ██████╗ ██╗   ██╗██████╗ [/]           Users:      1
  [bold cyan]  ██╔══██╗██╔═══██╗╚██╗ ██╔╝██╔══██╗[/]           Revenue:    $0
  [bold cyan]  ██████╔╝██║   ██║ ╚████╔╝ ██║  ██║[/]           Valuation:  $0B
  [bold cyan]  ██╔══██╗██║   ██║  ╚██╔╝  ██║  ██║[/]           Commas:     tres
  [bold cyan]  ██████╔╝╚██████╔╝   ██║   ██████╔╝[/]           Status:     pre
  [bold cyan]  ╚═════╝  ╚═════╝    ╚═╝   ╚═════╝ [/]
  [bold blue]                   ██████╗  ██████╗ ██████╗ ███████╗[/]
  [bold blue]                  ██╔════╝ ██╔═══██╗██╔══██╗██╔════╝[/]
  [bold blue]                  ██║      ██║   ██║██║  ██║█████╗  [/]
  [bold blue]                  ██║      ██║   ██║██║  ██║██╔══╝  [/]
  [bold blue]                  ╚██████╗ ╚██████╔╝██████╔╝███████╗[/]
  [bold blue]                   ╚═════╝  ╚═════╝ ╚═════╝ ╚══════╝[/]
  [dim]v0.1  Artificial Intelligence, Personal Edition[/]

  ----------------------------------------------------------------

  Provider  Gemini                  Project  my-project
  Model     gemini-2.5-pro          Engine   InProcess
  cwd       C:\Users\jason\source\repos\my-project
  Git       C:\Users\jason\source\repos\my-project (main)

  [green]\u2713[/] Ready  Commands run in a constrained PowerShell runspace.

  [dim italic]Type a message to start, or /help for available commands.[/]

```

### Mockup (80 columns, compact height)

```

  [bold cyan]BOYD[/][bold blue]CODE[/]  [dim]v0.1  AI Coding Assistant[/]

  ----------------------------------------------------------------

  Provider  Gemini          Project  my-project
  Model     gemini-2.5-pro  Engine   InProcess
  cwd       C:\Users\jason\source\repos\my-project

  [green]\u2713[/] Ready  Commands run in a constrained PowerShell runspace.

  [dim italic]Type a message to start, or /help for available commands.[/]

```

### When to Use

Application startup, before the interactive loop begins.

### Rendering

The banner is the first content appended to the conversation view when the
application starts. It is composed from the figlet art, info grid, and
status message components. Once rendered, it becomes part of the scrollable
conversation history.

### Style Tokens

- `[bold cyan]` for "BOYD" art
- `[bold blue]` for "CODE" art
- `[dim]` for tagline, metadata sidebar, info labels
- `[cyan]` for info values
- `[green]` + checkmark for "Ready"
- `[yellow bold]` for "Not configured"
- `[dim italic]` for hint line
- Info grid pattern for configuration display

---

## 25. Status Bar

### Description

Persistent bottom row showing session metadata and contextual keybinding hints.

### Mockup (120 columns)

```
[dim]Gemini | gemini-2.5-pro | my-project | main | InProcess          Esc: cancel  /help: commands[/]
```

### Mockup (80 columns)

```
[dim]Gemini | gemini-2.5-pro | my-project         /help[/]
```

### Mockup (compact, < 80 columns)

```
[dim]Gemini | gemini-2.5-pro[/]
```

### When to Use

Always visible as the bottom row of the view hierarchy. Displays session
metadata on the left and contextual keybinding hints on the right.

### Rendering

A single-row region with left-aligned metadata and right-aligned hints.
Content adapts to terminal width (see Responsive Behavior below).

### Style Tokens

- Level 3 (dim) for all status bar text
- Pipe separators between metadata items

### Responsive Behavior

| Width Tier | Content                                              |
|------------|------------------------------------------------------|
| Full       | Provider, model, project, branch, engine + key hints |
| Standard   | Provider, model, project + abbreviated hints         |
| Compact    | Provider, model only                                 |

---

## 26. Activity Region (formerly Indicator Bar)

### Description

Single-row activity indicator in the view hierarchy. Shows agent state with
an animated braille spinner during active turns. In idle state, displays a
dim horizontal rule.

All busy states use the same animated 8-frame braille spinner at 100ms per
frame.

### Mockup -- Thinking

```
[yellow]⠿ Thinking...[/]
```

### Mockup -- Streaming

```
[cyan]⠿ Streaming...[/]
```

### Mockup -- Executing

```
[cyan]⠿ Executing... (2.3s)[/]
```

The braille character animates at 100ms per frame (8-frame cycle). The elapsed
time advances each frame.

### Mockup -- Cancel hint

```
[yellow]Press Esc again to cancel[/]
```

### Mockup -- Modal active

```
[dim]Esc to dismiss[/]
```

### Companion: Separator

Below the input view sits a static separator row:

```
────────────────────────────────────────────────────────────────
```

A dim horizontal rule on a single-row region. It never changes content. It
visually separates the input view from the status bar.

### When to Use

Always present as a single-row region in the view hierarchy.

### Rendering

All busy states display left-aligned styled text with the braille spinner
character (except cancel hint and modal dismiss, which use text only):

- **Thinking**: `[yellow]{spinner} Thinking...[/]`
- **Streaming**: `[cyan]{spinner} Streaming...[/]`
- **Executing**: `[cyan]{spinner} Executing... ({elapsed})[/]`
- **Cancel hint**: `[yellow]Press Esc again to cancel[/]` (no spinner)
- **Modal dismiss**: `[dim]Esc to dismiss[/]` (no spinner)
- **Idle**: dim horizontal rule

Spinner frames cycle at 100ms: `["⠿", "⠻", "⠽", "⠾", "⠷", "⠯", "⠟", "⠾"]`

### Style Tokens

- Thinking: `[yellow]` left-aligned Markup, braille spinner prefix
- Streaming: `[cyan]` left-aligned Markup, braille spinner prefix
- Executing: `[cyan]` left-aligned Markup, braille spinner prefix, elapsed time
- Cancel hint: `[yellow]` left-aligned Markup (no spinner)
- Modal: `[dim]` left-aligned Markup (no spinner)
- Separator: dim Rule (static, never changes)

### Key Change from Old Architecture

In the old architecture, the Indicator Bar used a static `@` prefix for
Thinking and Streaming states. This has been replaced with the animated
braille spinner for all busy states.

---

## 27. Context Usage Bar

### Description

Visual bar chart showing context window utilization. Used in the `/context show`
modal overlay.

### Mockup (80 columns)

```
  Context: 45% of 1,048,576 tokens

  [blue]\u2588\u2588\u2588\u2588[/][green]\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588[/][grey]\u2591\u2591\u2591\u2591\u2591\u2591\u2591\u2591\u2591\u2591\u2591\u2591\u2591\u2591\u2591\u2591\u2591[/]

  \u25a0 System prompt   12,400 (3%)
  \u25a0 Messages        425,000 (41%)
  \u25a0 Free            611,176 (58%)
```

### When to Use

`/context show` modal overlay.

### Rendering

The bar is built character-by-character using block characters with color
markup. The legend uses black square characters with semantic colors.

### Style Tokens

- `[blue]` for system prompt segment
- `[green]` for messages segment
- `[grey]` (dim) for free space
- Full block `\u2588` for filled, light shade `\u2591` for free
- Black square `\u25a0` for legend dots
- Threshold colors for the header percentage
