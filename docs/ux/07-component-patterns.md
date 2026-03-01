# Component Patterns (Prescriptive)

This document defines the REQUIRED component library for BoydCode v2. Every
reusable UI pattern is specified here with ASCII mockups, usage guidance,
Spectre.Console implementation notes, and accessibility considerations.

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
26. [Indicator Bar](#26-indicator-bar)
27. [Context Usage Bar](#27-context-usage-bar)

---

## 1. User Message Block

### Description

Renders the user's message in the conversation. Visually distinct from
assistant messages through an accent-colored prompt indicator.

### Mockup (80 columns)

```
  [bold blue]>[/] Can you add error handling to the auth module?
```

### Mockup (120 columns)

Same format. User messages do not change with width.

### When to Use

Every user message in the conversation history.

### Spectre.Console Implementation

```csharp
new Markup($"  [bold blue]>[/] {Markup.Escape(userMessage)}")
```

Wrapped in a `Padder` if needed. The `>` symbol is always `[bold blue]`.

### Style Tokens

- `[bold blue]` (accent) for the `>` indicator
- Level 2 (plain) for the message text
- Level 1 indent (2 spaces)

### Accessibility

- The `>` symbol identifies user messages without color
- Screen readers see: `> {message text}` which is clear

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

### Spectre.Console Implementation

```csharp
new Panel(Markup.Escape(assistantText))
    .Border(BoxBorder.None)
    .PadLeft(2)
```

The `Panel` with `BoxBorder.None` provides word-wrapping without visible borders.
`PadLeft(2)` creates the standard indent.

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

### Spectre.Console Implementation

```csharp
new Markup($"  [dim]{inputTokens:N0} in / {outputTokens:N0} out / {totalTokens:N0} total[/]")
```

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

### Spectre.Console Implementation

```csharp
new Panel(Markup.Escape(formattedPreview))
    .Header($"[dim]{Markup.Escape(toolName)}[/]")
    .Border(BoxBorder.Rounded)
    .BorderColor(Color.Grey)
    .Padding(1, 0)
    .Expand()
```

### Style Tokens

- `BoxBorder.Rounded` with `Color.Grey` border
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

### Spectre.Console Implementation

```csharp
// Success
new Markup($"  [green]\u2713[/] [dim]{Markup.Escape(toolName)}  {lineCount} lines | {duration}[/]")

// Error
new Markup($"  [red]\u2717[/] [dim]{Markup.Escape(toolName)}  {lineCount} lines | {duration}[/]")
```

### Style Tokens

- `[green]` + checkmark (success) or `[red]` + cross (error)
- Level 3 (dim) for metadata
- Level 4 (dim italic) for expand hint
- Level 1 indent (2 spaces)

---

## 6. Execution Progress

### Description

In-place indicator shown while a tool is executing. Appears in the indicator
bar (not the content region) so it does not scroll away.

### Mockup

The indicator bar shows:

```
@ Executing... (2.3s)
```

The `@` character replaces the braille spinner. The elapsed time updates
in-place via the Live context refresh.

### When to Use

Between `RenderExecutingStart()` and `RenderExecutingStop()`.

### Spectre.Console Implementation

The indicator bar is a Size(1) row in the Layout. During execution:

```csharp
layout["Indicator"].Update(
    new Markup($"[blue]@ Executing... ({elapsed})[/]"));
ctx.Refresh();
```

### Style Tokens

- `[blue]` (accent) for executing state
- `@` symbol as activity indicator

### Accessibility

In accessible mode, render as static text: `[Executing... 2.3s]`

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

### Spectre.Console Implementation

Use `SpectreHelpers` methods. They handle escaping internally.

```csharp
SpectreHelpers.Success("Project my-project created.");
SpectreHelpers.Error("Could not connect to the API.");
SpectreHelpers.Warning("Context is 85% full.");
```

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

### Spectre.Console Implementation

```csharp
SpectreHelpers.Section("Directories");
// Renders: blank line + Rule("[bold]Directories[/]").LeftJustified().RuleStyle("dim")
```

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

### Spectre.Console Implementation

```csharp
SpectreHelpers.InfoGrid()       // Creates 4-column Grid with padding
SpectreHelpers.AddInfoRow(grid, "Provider", "Gemini", "Project", "my-project")
SpectreHelpers.AddInfoRow(grid, "cwd", path)
```

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

`/project list`, `/jea list`, `/sessions list`, `/provider list`.

### Spectre.Console Implementation

```csharp
var table = SpectreHelpers.SimpleTable("Name", "Dirs", "Docker", "Engine");
table.AddRow("my-project", "3", "python", "--");
```

### Style Tokens

- `TableBorder.Simple`
- `[bold]` for headers (applied by SimpleTable)
- `[dim]\u2014[/]` for empty cells

---

## 11. Modal Overlay

### Description

A Panel that replaces the Content region to show slash command output without
blocking the conversation. The modal appears instantly, can be dismissed with
Esc, and restores the conversation view on dismissal.

This is the KEY NEW PATTERN in BoydCode v2. It enables non-blocking access
to configuration and information while the AI continues working.

### Mockup (80 columns) -- /help

```
+-- Help ---------------------------------------------------+
|                                                            |
|  Command          Description                              |
|  -------          -----------                              |
|  /help            Show this help                           |
|  /project <sub>   Manage projects                          |
|  /provider <sub>  Manage LLM providers                     |
|  /jea <sub>       Manage JEA security profiles             |
|  /sessions <sub>  Manage sessions                          |
|  /context <sub>   View/manage context window               |
|  /expand          Show last tool output                    |
|  /refresh         Refresh session context                  |
|  /clear           Clear conversation                       |
|  /quit            Exit BoydCode                            |
|                                                            |
|  [dim]Esc to dismiss[/]                                    |
|                                                            |
+------------------------------------------------------------+
```

### Mockup (80 columns) -- /project show

```
+-- my-project ---------------------------------------------+
|                                                            |
|  Provider  Gemini          Engine   InProcess               |
|  Docker    python:3.12     Branch   main                   |
|                                                            |
|  -- Directories ---                                        |
|  Path                          Access                      |
|  C:\Users\jason\src\app        ReadWrite                   |
|  C:\Users\jason\src\lib        ReadOnly                    |
|                                                            |
|  -- System Prompt ---                                      |
|  You are a Python expert working on the app project.       |
|                                                            |
|  [dim]Esc to dismiss[/]                                    |
|                                                            |
+------------------------------------------------------------+
```

### Mockup (120 columns) -- /project show

```
+-- my-project -------------------------------------------------------------------------------------------------+
|                                                                                                                |
|  Provider  Gemini                                Engine   InProcess                                            |
|  Docker    python:3.12-slim                      Branch   main                                                 |
|                                                                                                                |
|  -- Directories ---                                                                                            |
|  Path                                            Access                                                        |
|  C:\Users\jason\source\repos\app                 ReadWrite                                                     |
|  C:\Users\jason\source\repos\lib                 ReadOnly                                                      |
|                                                                                                                |
|  -- System Prompt ---                                                                                          |
|  You are a Python expert working on the app project. Focus on clean code and type safety.                      |
|                                                                                                                |
|  [dim]Esc to dismiss[/]                                                                                        |
|                                                                                                                |
+----------------------------------------------------------------------------------------------------------------+
```

### When to Use

Read-only slash commands that do not need interactive prompts:
- `/help`
- `/project show`, `/project list`
- `/provider show`, `/provider list`
- `/sessions list`, `/sessions show <id>`
- `/jea list`, `/jea show <name>`, `/jea effective`
- `/context show`
- `/expand`

### Behavior

1. User types the slash command (even while AI is streaming).
2. The Content region in the Layout is replaced with the modal Panel.
3. The Indicator bar shows `[dim]Esc to dismiss[/]`.
4. The AI continues working in the background -- tokens accumulate in the
   data model but are not visible.
5. User presses Esc (or Enter).
6. The Content region restores the conversation view, now including any
   tokens that arrived while the modal was open.
7. The Indicator bar returns to its prior state.

### Spectre.Console Implementation

```csharp
// Build modal content
var content = BuildModalContent(command, args);
var panel = new Panel(content)
    .Header($"[bold]{Markup.Escape(title)}[/]")
    .Border(BoxBorder.Rounded)
    .Expand()
    .Padding(2, 1);

// Show in Content region
layout["Content"].Update(panel);
_modalActive = true;
_indicatorState = IndicatorState.Modal;
ctx.Refresh();

// Wait for dismiss key (handled by AsyncInputReader dispatch)
// On Esc:
layout["Content"].Update(BuildConversationView());
_modalActive = false;
_indicatorState = _priorIndicatorState;
ctx.Refresh();
```

### Style Tokens

- `BoxBorder.Rounded` border
- `[bold]` for panel header
- `Padding(2, 1)`
- `[dim]Esc to dismiss[/]` at bottom of content

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

When a modal is open and the AI is streaming:
- Tokens accumulate in the `StringBuilder` / conversation model
- The Content region shows the modal, not the streaming text
- When dismissed, the conversation view shows all accumulated text
- The user sees the conversation "jump forward" to the current state
- This is intentional -- the modal is a lens over the data, not a pause button

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

### Spectre.Console Implementation

```csharp
SpectreHelpers.Select("Choose a [green]template[/]:", choices);
// Internally: SelectionPrompt with HighlightStyle(Color.Green)
// Wraps in SuspendLayout/ResumeLayout
```

### Style Tokens

- `Color.Green` highlight for selected item
- `[green]` in prompt title for the field name

### Note on Modals

Selection prompts CANNOT render inside the Live context. They require
Suspend/Resume. This is why `/project create` suspends the Live context.

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

### Spectre.Console Implementation

```csharp
SpectreHelpers.PromptNonEmpty("Project [green]name[/]:");
SpectreHelpers.PromptOptional("Docker image [dim](Enter to skip)[/]:");
SpectreHelpers.PromptWithDefault("Model:", "gemini-2.5-pro");
```

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

### Spectre.Console Implementation

```csharp
SpectreHelpers.Confirm("Save changes?", defaultValue: true);
```

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

`/project delete`, `/sessions delete`, `/jea delete`, `/provider remove`.

### Spectre.Console Implementation

Section divider + bulleted detail list + Confirm prompt. All wrapped in
SuspendLayout/ResumeLayout.

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

### Spectre.Console Implementation

```csharp
while (true)
{
    var choice = SpectreHelpers.Select(
        $"Edit [bold]{Markup.Escape(name)}[/]:", editChoices);
    if (choice == "Done") break;
    // Handle the selected field edit
}
```

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

### Spectre.Console Implementation

```csharp
new Markup($"  [dim]{input:N0} in / {output:N0} out / {total:N0} total[/]")
```

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

### Spectre.Console Implementation

Within the Live context, the streaming message is a `Markup` renderable that
grows as tokens arrive:

```csharp
_streamBuffer.Append(token);
var streamBlock = new Markup($"  {Markup.Escape(_streamBuffer.ToString())}");
// Update the last message in the conversation view
layout["Content"].Update(BuildConversationWithStream(streamBlock));
ctx.Refresh();
```

### Style Tokens

- Level 2 (plain) for streamed text
- Level 1 indent (2 spaces)
- No markup on streamed text (it is user-generated content from the LLM)

### Performance

- Rate-limit `ctx.Refresh()` to ~60fps (16ms minimum between refreshes)
- Cache finalized message renderables
- Only rebuild the streaming portion on each token

---

## 19. Thinking Indicator

### Description

Shown when the LLM request has been sent but no response tokens have arrived.
Renders in the Indicator bar, not in the content area.

### Mockup

Indicator bar shows:

```
@ Thinking...
```

In yellow text. The `@` character does not animate (replaces braille spinner).

### When to Use

Between `RenderThinkingStart()` and the first token or `RenderThinkingStop()`.

### Spectre.Console Implementation

```csharp
layout["Indicator"].Update(new Markup("[yellow]@ Thinking...[/]"));
ctx.Refresh();
```

### Style Tokens

- `[yellow]` (warning/attention) for thinking state
- `@` activity indicator

### Accessibility

In accessible mode: `[Thinking...]` (static text, no special character).

---

## 20. Cancel Hint

### Description

Feedback shown after the first Esc/Ctrl+C press during agent activity.
Appears in the Indicator bar and auto-dismisses after 1 second.

### Mockup

Indicator bar shows:

```
Press Esc again to cancel
```

In yellow text. Replaces whatever was in the indicator bar.

### When to Use

First Esc/Ctrl+C press during thinking, streaming, or execution.

### Behavior

1. First press: Indicator bar shows cancel hint (yellow text).
2. Second press within 1 second: Cancellation fires.
3. Timeout (1 second): Indicator bar reverts to prior state.

### Spectre.Console Implementation

```csharp
layout["Indicator"].Update(
    new Markup("[yellow]Press Esc again to cancel[/]"));
ctx.Refresh();

// After 1 second timeout:
layout["Indicator"].Update(_priorIndicatorContent);
ctx.Refresh();
```

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

### Spectre.Console Implementation

```csharp
new Markup($"  [dim]{Markup.Escape(emptyMessage)}[/]")
```

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

### Spectre.Console Implementation

```csharp
// Simple error -- via SpectreHelpers (writes to stderr)
SpectreHelpers.Error("Could not connect to the API.");

// Error with suggestion -- via IUserInterface (writes to stderr)
_ui.RenderError("Could not connect to the API.\n  Suggestion: Check your network connection");
```

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

### Spectre.Console Implementation

```csharp
var panel = new Panel(errorContent)
    .Header("[red bold] boydcode crash [/]")
    .BorderColor(Color.Red)
    .Padding(1, 1, 1, 1);
AnsiConsole.Write(panel);
```

### Style Tokens

- `BoxBorder.Rounded` with `Color.Red` border (default Rounded, red color)
- `[red bold]` for header
- `Padding(1, 1, 1, 1)`

---

## 24. Banner

### Description

Startup display showing brand identity, session configuration, and readiness
status. Renders once and becomes scrollback.

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

### Spectre.Console Implementation

The banner renders BEFORE the Layout/Live context activates. It uses standard
Spectre.Console calls (`AnsiConsole.MarkupLine`, `AnsiConsole.Write(rule)`,
`AnsiConsole.Write(grid)`) and becomes part of scrollback.

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

Always visible as the bottom row of the Layout during the interactive session.

### Spectre.Console Implementation

A Size(1) row in the Layout, rendered as a `Grid` or `Columns` with left-aligned
metadata and right-aligned hints.

```csharp
layout["StatusBar"].Update(BuildStatusBar(widthTier));
```

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

## 26. Indicator Bar

### Description

Single-row indicator between the content area and the input line. In idle state,
it is a dim horizontal rule. In active states, it shows agent status.

### Mockup -- Idle

```
────────────────────────────────────────────────────────────
```

### Mockup -- Thinking

```
[yellow]@ Thinking...[/]
```

### Mockup -- Streaming

```
[cyan]@ Streaming...[/]
```

### Mockup -- Executing

```
[blue]@ Executing... (2.3s)[/]
```

### Mockup -- Cancel hint

```
[yellow]Press Esc again to cancel[/]
```

### Mockup -- Modal active

```
[dim]Esc to dismiss[/]
```

### When to Use

Always present as a Size(1) row in the Layout.

### Spectre.Console Implementation

```csharp
// Idle
layout["Indicator"].Update(new Rule().RuleStyle("dim"));

// Active states
layout["Indicator"].Update(new Markup("[yellow]@ Thinking...[/]"));
```

### Style Tokens

- Idle: dim Rule
- Thinking: `[yellow]`
- Streaming: `[cyan]`
- Executing: `[blue]`
- Cancel hint: `[yellow]`
- Modal: `[dim]`

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

### Spectre.Console Implementation

The bar is built character-by-character using block characters with color markup.
The legend uses black square characters with semantic colors.

### Style Tokens

- `[blue]` for system prompt segment
- `[green]` for messages segment
- `[grey]` (dim) for free space
- Full block `\u2588` for filled, light shade `\u2591` for free
- Black square `\u25a0` for legend dots
- Threshold colors for the header percentage
