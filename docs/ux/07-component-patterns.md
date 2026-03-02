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
28. [Interactive List](#28-interactive-list)
29. [Action Bar](#29-action-bar)
30. [Search/Filter Field](#30-searchfilter-field)
31. [Form Dialog](#31-form-dialog)
32. [Multi-Step Wizard](#32-multi-step-wizard)
33. [Scroll Position Indicator](#33-scroll-position-indicator)

---

## 1. User Message Block

### Description

Renders the user's message in the conversation. Visually distinct from
assistant messages through a subtle dark background tint and `>` prefix.
Rendered by `ConversationBlockRenderer` as a `UserMessageBlock`.

### Mockup (80 columns)

```
 > Can you add error handling to the auth module?
```

The entire row is filled with `Theme.User.Text` (white on dark background,
rgb(50,50,50)). The `>` prefix uses `Theme.User.Prefix` (dark gray on that
same background). Content wraps at `width - 2` to preserve the prefix column.

### Mockup (120 columns)

Same format. User messages do not change with width.

### When to Use

Every user message in the conversation. User messages are appended to the
conversation view immediately when submitted.

### Echo Behavior

When the user submits a message, it is immediately appended to the
conversation view as a `UserMessageBlock`, then the activity bar transitions
to the Thinking state.

### Rendering

`ConversationBlockRenderer.Draw` handles `UserMessageBlock`. Each line fills
the full row width with `Theme.User.Text` (dark background), draws the `> `
prefix with `Theme.User.Prefix` (dark gray on dark background), then draws
the message text. Word wrapping uses `width - 2` to keep text aligned after
the prefix. No Spectre markup processing — text is drawn as plain characters
via `AddStr`.

The `Theme.User.Background` (rgb 50,50,50) is an intentional exception to the
ANSI 4-bit color rule (see 06-style-tokens.md Section 1.6). It provides a
subtle blockquote-style tint that distinguishes user messages from assistant
text without using borders or colored text.

### Style Tokens

- `Theme.User.Background` — rgb(50,50,50) row fill (see 06-style-tokens.md Section 1.6)
- `Theme.User.Text` — white on `Theme.User.Background` for message text
- `Theme.User.Prefix` — dark gray on `Theme.User.Background` for `> ` prefix
- `Theme.Text.PromptPrefix` = `"> "` (2-character prefix constant)

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

`ConversationBlockRenderer.Draw` handles `AssistantTextBlock`. Each wrapped
line is drawn via `AddStr` with a 2-space prefix (`"  "`). The attribute is
`Theme.Semantic.Default` (white). Word wrapping uses `width - 2`.

### Style Tokens

- `Theme.Semantic.Default` (white) for text
- 2-space indent prepended to each line via `AddStr("  ")`

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

`ConversationBlockRenderer.Draw` handles `TokenUsageBlock`. Drawn as a single
line with `Theme.Semantic.Muted` (dark gray) and 2-space indent:
`  {input:N0} in / {output:N0} out / {total:N0} total`

### Style Tokens

- `Theme.Semantic.Muted` (dark gray) for all token text (see 06-style-tokens.md)
- 2-space indent prepended to the line

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

`ConversationBlockRenderer.Draw` handles `ToolCallConversationBlock`. Drawn
using Unicode box-drawing characters via `Theme.Symbols` constants:

- Top border: `BoxTopLeft` + `Rule` + ` {toolName} ` + `Rule`... + `BoxTopRight`
- Content lines: `BoxVertical` + ` {line} ` + `BoxVertical`
- Bottom border: `BoxBottomLeft` + `Rule`... + `BoxBottomRight`

All borders use `Theme.ToolBox.Border` (= `Theme.Semantic.Muted`, dark gray).
Content text uses `Theme.Semantic.Default` (white). Preview text wraps at
`width - 4` (2 border chars + 2 padding chars per side).

### Style Tokens

- `Theme.ToolBox.Border` = `Theme.Semantic.Muted` (dark gray) for box borders
- `Theme.Semantic.Default` (white) for command content
- `Theme.Symbols.BoxTopLeft/TopRight/BottomLeft/BottomRight/Vertical/Rule`
  for the box-drawing characters

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

`ConversationBlockRenderer.Draw` handles `ToolResultConversationBlock` and
`ExpandHintBlock` separately. Both are drawn via `AddStr` with 2-space indent:

- Success: `Theme.Semantic.Success` (green) `Theme.Symbols.Check` + tool name,
  then `Theme.Semantic.Muted` for `  {lineCount} lines | {duration}`
- Error: `Theme.Semantic.Error` (bright red) `Theme.Symbols.Cross` + tool name
  + `" error"`, then `Theme.Semantic.Muted` for `  {lineCount} lines | {duration}`
- No output: muted suffix `"  Command completed successfully."` or `"  error"`
- Expand hint: `ExpandHintBlock` drawn as `Theme.Semantic.Muted`
  `  /expand to show full output` (`Theme.Text.ExpandHint`)

### Style Tokens

- `Theme.Semantic.Success` (green) for checkmark on success
- `Theme.Semantic.Error` (bright red) for cross on error
- `Theme.Semantic.Muted` (dark gray) for metadata and expand hint
- `Theme.Symbols.Check` = `\u2713`, `Theme.Symbols.Cross` = `\u2717`
- `Theme.Text.ExpandHint` = `"/expand to show full output"`

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

`ActivityBarView` handles all activity states. During execution, it draws
with `Theme.Semantic.Info` (cyan) and cycles through `Theme.Symbols.SpinnerFrames`
(10-frame braille sequence) at `Theme.Layout.SpinnerIntervalMs` (100ms) per
frame via a repeating `TguiApp.AddTimeout` timer.

### Style Tokens

- `Theme.Semantic.Info` (cyan) for executing state
- `Theme.Symbols.SpinnerFrames` — 10-frame braille cycle at 100ms/frame
- `Theme.Text.ExecutingLabel` = `"Executing..."`

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

`ConversationBlockRenderer.Draw` handles `StatusMessageBlock`. The `MessageKind`
enum selects the attribute:

- `Success` → `Theme.Semantic.Success` (green)
- `Error` → `Theme.Semantic.Error` (bright red)
- `Warning` → `Theme.Semantic.Warning` (yellow)
- `Hint` → `Theme.Semantic.Muted` (dark gray)
- Default → `Theme.Semantic.Default` (white)

Text wraps at `width - 2` with a 2-space indent. Pass plain text to the
user interface — no Spectre markup escaping needed.

### Style Tokens

See 06-style-tokens.md Section 7.1 for exact formats. Theme constants:
`Theme.Semantic.Success/Error/Warning/Muted/Default`.

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

`ConversationBlockRenderer.Draw` handles `SectionBlock`. Drawn as a centered
horizontal rule: `{Rule×n} {title} {Rule×n}` where the rule character is
`Theme.Symbols.Rule` (`\u2500`) and the side lengths are computed from the
available width. A blank line (a `SeparatorBlock` with no drawn content)
precedes the section block.

### Style Tokens

- `Theme.Semantic.Muted` (dark gray) for the full section line
- `Theme.Symbols.Rule` = `\u2500` for the rule character
- Blank line above represented by a `SeparatorBlock`

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

In the banner, `BannerRenderer` draws info pairs using `DrawInfoPair` (two
label/value columns) or `DrawInfoSingle` (one full-width row). Labels use
`Theme.Banner.InfoLabel` (= `Theme.Semantic.Muted`, dark gray); values use
`Theme.Banner.InfoValue` (= `Theme.Semantic.Info`, cyan). Label column is
padded to `Theme.Layout.InfoLabelPad` (10 chars). Start position is `x = 2`.

In slash command output (e.g., `/project show`), the same visual result is
produced by formatting plain text lines into a `PlainTextBlock` or a modal
`ShowModal` call — column alignment is done with string padding.

### Style Tokens

- `Theme.Banner.InfoLabel` = `Theme.Semantic.Muted` (dark gray) for labels
- `Theme.Banner.InfoValue` = `Theme.Semantic.Info` (cyan) for values
- `Theme.Layout.InfoLabelPad` = 10 character label column width
- 2-character left offset (`Move(2, y)`)

---

## 10. Simple Table

### Description

Standard data table for listing items. Simple border, bold headers, clean cells.
Used for **non-interactive, informational output only**.

> **Note**: For data that the user can act on (open, edit, delete), use the
> [Interactive List](#28-interactive-list) pattern instead. Simple Table remains
> the correct choice for read-only tabular data displayed inline in the
> conversation view (e.g., list summaries, status reports).

### Mockup (80 columns)

```
  Name            Dirs  Docker    Engine
 -----------------------------------------------
  my-project      3     python    InProcess
  api-service     1     --        Container
  _default        0     --        InProcess
```

### When to Use

- **Non-TUI/piped fallback**: When list commands (`/project list`, `/jea list`,
  `/conversations list`, `/provider list`, `/agent list`) run in a non-interactive
  or piped terminal, they fall back to column-aligned plain text using this
  pattern instead of the Interactive List window.
- **Inline informational tables**: Read-only tabular data displayed within the
  conversation view (e.g., status reports, summaries) that does not need
  row-level actions.

In TUI mode, the interactive versions of all list commands use Interactive List
(#28) in a windowed overlay with keyboard navigation and row-level actions.

### Rendering

List commands output column-aligned plain text lines as `PlainTextBlock`
records in the conversation view. Column alignment is done with string padding
(`PadRight`). The header row and separator line are included as plain text.
Empty cells display a dim em-dash (`\u2014`). No Spectre `Table` objects are
constructed in TUI mode.

### Style Tokens

- `Theme.Semantic.Default` (white) for data rows via `PlainTextBlock`
- `\u2014` em-dash for empty cells
- `Theme.Layout.CommandPad` = 24 character column width for command lists

---

## 11. Modal Overlay

### Description

A bordered window that overlays the conversation to show slash command output
without blocking it. The window appears instantly, can be dismissed with Esc,
and restores the conversation view on dismissal.

This is the KEY NEW PATTERN in BoydCode v2. It enables non-blocking access
to configuration and information while the AI continues working.

Modal overlays come in three variants based on the content type:

| Variant        | Inner View              | Use Case                            |
|----------------|-------------------------|-------------------------------------|
| Text Modal     | Read-only `TextView`    | Unstructured text (/help, /expand)  |
| List Modal     | `ListView` + Action Bar | Tabular data with row actions       |
| Detail Modal   | Native drawing layout   | Structured key-value data (/show)   |

### Variant A: Text Modal

The default variant. A read-only `TextView` fills the window, providing
scrollable unstructured text content.

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

### Variant B: List Modal

A `ListView` with keyboard navigation and an Action Bar (see pattern #29)
at the bottom. Used when the modal displays tabular data that the user can
act on row-by-row.

### Mockup (80 columns) -- List Modal

```
+-- Conversations ------------------------------------------+
|                                                            |
|  Name                     Messages  Created                |
|  ▶ Auth module refactor   42        2026-02-28 14:30       |
|    API design review      18        2026-02-27 09:15       |
|    Docker setup            6        2026-02-26 16:45       |
|    Initial project setup  12        2026-02-25 11:00       |
|                                                            |
|  Enter: Open  r: Rename  d: Delete  Esc: Close            |
|                                                            |
+------------------------------------------------------------+
```

The highlighted row uses `Theme.List.SelectedBackground` and
`Theme.List.SelectedText`. The `▶` arrow indicator (`Theme.Symbols.Arrow`,
U+25B6) marks the current row. See [Interactive List](#28-interactive-list)
for the full pattern specification and [Action Bar](#29-action-bar) for the
shortcut hint row.

### Variant C: Detail Modal

A structured key-value layout using Terminal.Gui native drawing
(`SetAttribute`, `Move`, `AddStr`). Labels are drawn with
`Theme.Semantic.Muted` and values with `Theme.Semantic.Info` (or
`Theme.Semantic.Default` for text content). Section dividers (pattern #8)
separate logical groups.

### Mockup (80 columns) -- /project show

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
- `/help` -- Text Modal
- `/expand` -- Text Modal
- `/project show` -- Detail Modal
- `/provider show` -- Detail Modal
- `/conversations show <id>` -- Detail Modal
- `/jea show <name>`, `/jea effective` -- Detail Modal
- `/agent show <name>` -- Detail Modal
- `/context show` -- Detail Modal (includes Context Usage Bar, pattern #27)
- `/conversations list` (future) -- List Modal
- `/project list` (future) -- List Modal

### Behavior

1. User triggers a modal slash command (via message queue or direct input).
2. A modeless window opens over the conversation view, showing the content.
3. The activity bar transitions to `ActivityState.Modal` and shows `"Esc to dismiss"` (`Theme.Text.EscToDismiss`).
4. The AI continues working in the background -- tokens accumulate in the
   conversation model but are not visible while the window is open.
5. User presses Esc (or Enter).
6. The window closes. The conversation view is fully visible again,
   including any tokens that arrived while the window was open.
7. The activity bar returns to its prior state.

For the List Modal variant, the `ListView` inside the window handles
Up/Down/Enter and single-letter hotkey navigation before the window's own
Esc handler fires.

### Rendering

`SpectreUserInterface.ShowModal(title, content)` opens a Terminal.Gui `Window`
overlay centered on screen and sized to fit its content:

- Width: `min(longestLine + 4, max(40, terminalWidth * 0.9))` -- the +4
  accounts for 1-char border + 1-char offset on each side
- Height: `min(lineCount + 2, max(5, terminalHeight * 0.9))` -- the +2
  accounts for the top and bottom border rows
- `BorderStyle = LineStyle.Rounded`
- `window.Border?.SetScheme(Theme.Modal.BorderScheme)` applies a blue border
  (`ColorName16.Blue` foreground)
- A read-only `TextView` fills the window at `X = 1, Width = Dim.Fill(1),
  Height = Dim.Fill()` giving 2 characters of total horizontal padding
- `ShowModal` always appends `"\n\n" + Theme.Text.EscToDismiss` to content
- `ActivityBarView` transitions to `ActivityState.Modal` while the window
  is open, showing `Theme.Text.EscToDismiss` (= `"Esc to dismiss"`)

For Variant B (List Modal), a `ShowListModal(title, items, actions)` method
provides a `ListView` instead of `TextView`, with an Action Bar view
(`Height = 1`, `Y = Pos.AnchorEnd(1)`) below the list. The list view fills
`Height = Dim.Fill(1)` to leave room for the action bar.

For Variant C (Detail Modal), a `ShowDetailModal(title, sections)` method
uses a custom `View` with `OnDrawingContent` that draws labeled key-value
rows using `Theme.Semantic.Muted` for labels and `Theme.Semantic.Info` for
values, with Section Dividers (pattern #8) between groups.

### Style Tokens

References `06-style-tokens.md`:

- **Border**: `LineStyle.Rounded`, `Theme.Modal.BorderScheme` (blue, see 5.1, 5.2)
- **Sizing**: Sized to content, centered, capped at 90% of terminal
- **Dismiss hint**: `Theme.Text.EscToDismiss` = `"Esc to dismiss"` appended to content
- **Content**: Read-only `TextView` (Text Modal), `ListView` (List Modal), or
  native drawing `View` (Detail Modal)
- **List selection**: `Theme.List.SelectedBackground`, `Theme.List.SelectedText`
- **Detail labels**: `Theme.Semantic.Muted` (dark gray)
- **Detail values**: `Theme.Semantic.Info` (cyan) or `Theme.Semantic.Default` (white)

### Accessibility

In accessible mode, modals render as delimited blocks:

```
=== Help ===
Command          Description
/help            Show this help
...
=== End Help ===
```

List Modal items are presented sequentially with a numbered prefix for
screen readers. Detail Modal sections use `---` separators.

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

Interactive list for choosing one option from a bounded set. Two approaches
are available: a Terminal.Gui `Dialog` with `ListView` (preferred) and a
Spectre.Console `SelectionPrompt` during TUI suspension (fallback).

### Approach A: Terminal.Gui Dialog (Preferred)

A modal `Dialog` with a `ListView` of options. The dialog blocks until the
user selects an item or cancels.

### Mockup -- Dialog Approach (80 columns)

```
+-- Select Template ----------------------------------------+
|                                                            |
|    console                                                 |
|  ▶ webapi                                                  |
|    classlib                                                |
|    worker                                                  |
|                                                            |
|                              [ Cancel ]  [ Ok ]            |
|                                                            |
+------------------------------------------------------------+
```

The highlighted row uses `Theme.List.SelectedBackground` and
`Theme.List.SelectedText`. Up/Down arrows (and j/k) navigate rows. Enter
confirms the selection and closes the dialog. Esc cancels and returns `null`.

### Terminal.Gui Implementation

```csharp
var dialog = new Dialog
{
    Title = "Select Template",
    Width = Dim.Percent(60),
    Height = Dim.Percent(50),
};
dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

var listView = new ListView(options)
{
    X = 1, Y = 1,
    Width = Dim.Fill(1),
    Height = Dim.Fill(2),
};
dialog.Add(listView);

var okButton = new Button("Ok") { IsDefault = true };
var cancelButton = new Button("Cancel");
dialog.AddButton(okButton);
dialog.AddButton(cancelButton);
```

### Approach B: Spectre Suspension (Fallback)

### Mockup -- Spectre Approach

```
  Choose a [green]template[/]:
  > console
    webapi
    classlib
```

Presented via Spectre.Console `SelectionPrompt<T>` during Terminal.Gui
suspension. The Terminal.Gui application is suspended before the prompt
renders, and resumed afterward. The prompt title uses Spectre `[green]` markup
for the field name. The selected item is highlighted in green.

### When to Use

Any choice from a list of 3-10 options. For 2 options, consider Confirmation
Prompt instead. For 10+ options, consider grouping or search, or use the
[Search/Filter Field](#30-searchfilter-field) pattern.

Use Approach A (Dialog) when:
- The selection is part of a multi-field form or wizard step
- The options need custom rendering (icons, descriptions, metadata)
- You want to avoid the visual flicker of suspend/resume

Use Approach B (Spectre) when:
- The prompt is a standalone one-off selection outside a form context
- The selection occurs during a flow that already uses Spectre prompts
  (e.g., mid-suspension for other inputs)

### Style Tokens

**Dialog approach**:
- `Theme.Modal.BorderScheme` (blue border) for the dialog
- `Theme.List.SelectedBackground`, `Theme.List.SelectedText` for highlighted row
- Standard Dialog button styling

**Spectre approach**:
- Spectre `[green]` highlight for selected item
- Spectre `[green]` in prompt title for the field name

### Keyboard

| Key       | Action                          |
|-----------|---------------------------------|
| Up / k    | Move selection up               |
| Down / j  | Move selection down             |
| Enter     | Confirm selection               |
| Esc       | Cancel (Dialog returns `null`)  |

### Note on Interactive Prompts

Spectre interactive selection prompts require suspending the Terminal.Gui
application because Spectre.Console reads from stdin directly. This causes a
brief visual flicker as Terminal.Gui leaves and re-enters the alternate screen
buffer. The Dialog approach avoids this entirely by staying within Terminal.Gui.

---

## 13. Text Prompt

### Description

Free-text input with optional validation, default values, and hints. Two
approaches are available: a Terminal.Gui `Dialog` with `TextField` (preferred)
and a Spectre.Console `TextPrompt` during TUI suspension (fallback).

### Approach A: Terminal.Gui Dialog (Preferred)

A modal `Dialog` containing a `Label` (field name) and a `TextField` (input).
Ok/Cancel buttons at the bottom. Tab moves between fields; Enter confirms.

### Mockup -- Dialog Approach (80 columns)

```
+-- Project Name -------------------------------------------+
|                                                            |
|  Name:  [my-project                                     ]  |
|                                                            |
|                              [ Cancel ]  [ Ok ]            |
|                                                            |
+------------------------------------------------------------+
```

The `TextField` uses `Theme.Input.Text` for entered text. The label uses
`Theme.Semantic.Default` (white, bold) for the field name. Validation errors
appear below the field in `Theme.Semantic.Error` (bright red).

### Mockup -- Dialog with validation error

```
+-- Project Name -------------------------------------------+
|                                                            |
|  Name:  [                                               ]  |
|  Name cannot be empty.                                     |
|                                                            |
|                              [ Cancel ]  [ Ok ]            |
|                                                            |
+------------------------------------------------------------+
```

### Approach B: Spectre Suspension (Fallback)

### Mockup -- Spectre Approach

```
  Project [green]name[/]: my-project
```

### Mockup -- With hint

```
  Docker image [dim](Enter to skip)[/]: python:3.12
```

Presented via Spectre.Console `TextPrompt<string>` during Terminal.Gui
suspension. Three variants:
- **Required**: Validates non-empty input before accepting.
- **Optional**: Accepts empty input (Enter to skip).
- **With default**: Pre-fills a default value; Enter accepts the default.

### When to Use

Named text input during wizard flows. See [Form Dialog](#31-form-dialog) for
multi-field input scenarios.

Use Approach A (Dialog) when:
- The input is a single field or part of a Form Dialog (pattern #31)
- Real-time validation feedback is needed (inline error messages)
- You want to avoid the visual flicker of suspend/resume

Use Approach B (Spectre) when:
- The prompt occurs during a flow that already uses Spectre prompts
- The input is a standalone one-off prompt outside a form context

### Style Tokens

**Dialog approach**:
- `Theme.Modal.BorderScheme` (blue border) for the dialog
- `Theme.Input.Text` (white) for `TextField` text
- `Theme.Semantic.Default` with `TextStyle.Bold` for field labels
- `Theme.Semantic.Error` (bright red) for inline validation messages
- `Theme.Semantic.Muted` (dark gray) for hint text below the field

**Spectre approach**:
- Spectre `[green]` for field name
- Spectre `[dim]` for hints and default values

### Keyboard

| Key       | Action                          |
|-----------|---------------------------------|
| Tab       | Move to next field / button     |
| Shift+Tab | Move to previous field / button |
| Enter     | Confirm (on Ok button or field) |
| Esc       | Cancel and close dialog         |

---

## 14. Confirmation Prompt

### Description

Yes/no question for non-destructive confirmations. Two approaches are
available: `MessageBox.Query` (preferred) and Spectre.Console `Confirm`
during TUI suspension (fallback).

### Approach A: MessageBox.Query (Preferred)

A system-level message box dialog with Yes/No buttons. Stays within the
Terminal.Gui application -- no suspend/resume flicker.

### Mockup -- MessageBox Approach (80 columns)

```
+-- Save Changes -------------------------------------------+
|                                                            |
|  Save changes to the current project?                      |
|                                                            |
|                                 [ No ]  [ Yes ]            |
|                                                            |
+------------------------------------------------------------+
```

The default button (matching the default answer) is pre-focused. Enter
confirms the focused button, Esc cancels (returns the non-default option).

### Terminal.Gui Implementation

```csharp
var result = MessageBox.Query(
    "Save Changes",
    "Save changes to the current project?",
    "No", "Yes");
// result: 0 = No, 1 = Yes
```

### Approach B: Spectre Suspension (Fallback)

### Mockup -- Spectre Approach

```
  Save changes? [y/N]: y
```

Presented via Spectre.Console `ConfirmationPrompt` during Terminal.Gui
suspension. The default value determines which option is capitalized in the
`[y/N]` or `[Y/n]` hint.

### When to Use

Before applying changes that are easily reversible. For destructive
operations, use [Delete Confirmation](#15-delete-confirmation) instead.

Use Approach A (MessageBox) when:
- The confirmation is part of an active TUI session
- You want to avoid the visual flicker of suspend/resume

Use Approach B (Spectre) when:
- The confirmation occurs during a flow that already uses Spectre prompts
  (e.g., mid-suspension for other inputs)

### Keyboard

| Key       | Action                       |
|-----------|------------------------------|
| Enter     | Confirm focused button       |
| Esc       | Cancel (non-default option)  |
| Tab       | Switch between buttons       |
| y / n     | Direct selection (Spectre)   |

---

## 15. Delete Confirmation

### Description

Two-step confirmation for destructive operations. Shows what will be deleted,
then asks for confirmation. Two approaches are available: a Terminal.Gui
`Dialog` with detail summary and Yes/No buttons (preferred) and a Spectre
suspension flow (fallback).

### Approach A: Terminal.Gui Dialog (Preferred)

A modal `Dialog` that displays a bulleted detail list of what will be
deleted, with "Delete" and "Cancel" buttons. The "Cancel" button is
pre-focused (safe default).

### Mockup -- Dialog Approach (80 columns)

```
+-- Delete Project -----------------------------------------+
|                                                            |
|  Are you sure you want to delete this project?             |
|                                                            |
|    * Name: my-project                                      |
|    * Directories: 3                                        |
|    * Custom prompt: Yes                                    |
|    * Docker image: python:3.12                             |
|                                                            |
|                           [ Cancel ]  [ Delete ]           |
|                                                            |
+------------------------------------------------------------+
```

The "Delete" button uses `Theme.Semantic.Error` (bright red) styling to
indicate a destructive action. Entity names are drawn bold. Detail bullets
use the `*` character (or `\u2022` on Unicode terminals).

### Terminal.Gui Implementation

```csharp
var dialog = new Dialog
{
    Title = "Delete Project",
    Width = Dim.Percent(60),
    Height = Dim.Auto(DimAutoStyle.Content),
};
dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

// Detail list as a Label with bulleted lines
var details = new Label
{
    X = 2, Y = 1,
    Text = $"Are you sure you want to delete this project?\n\n"
         + $"  * Name: {name}\n"
         + $"  * Directories: {dirCount}\n",
};
dialog.Add(details);

var cancelButton = new Button("Cancel") { IsDefault = true };
var deleteButton = new Button("Delete");
dialog.AddButton(cancelButton);
dialog.AddButton(deleteButton);
```

### Approach B: Spectre Suspension (Fallback)

### Mockup -- Spectre Approach (80 columns)

```

-- Delete Project -------------------------------------------
  [dim]\u2022[/] Name: [bold]my-project[/]
  [dim]\u2022[/] Directories: 3
  [dim]\u2022[/] Custom prompt: Yes
  [dim]\u2022[/] Docker image: python:3.12

  Delete project [bold]my-project[/]? [y/N]: y
  [green]\u2713[/] Project [bold]my-project[/] deleted.
```

Presented during Terminal.Gui suspension. A section divider line is written
to the console, followed by a bulleted detail list (Spectre markup), then a
Confirmation Prompt. Blocks until the user confirms or cancels.

### When to Use

`/project delete`, `/conversations delete`, `/jea delete`, `/provider remove`.

Use Approach A (Dialog) when:
- The deletion confirmation is a standalone action
- You want to avoid the visual flicker of suspend/resume

Use Approach B (Spectre) when:
- The deletion occurs during a flow that already uses Spectre prompts

### Style Tokens

**Dialog approach**:
- `Theme.Modal.BorderScheme` (blue border) for the dialog
- `Theme.Semantic.Error` (bright red) for the "Delete" button
- `Theme.Semantic.Default` with `TextStyle.Bold` for entity names
- `\u2022` bullet for detail items

**Spectre approach**:
- Spectre `[dim]\u2022[/]` for detail bullets
- Spectre `[bold]` for entity names
- Confirmation prompt pattern (Spectre)

### Edge Cases

- If the entity name is very long, truncate at 40 characters with ellipsis
  in the dialog title
- Always show the entity name in the detail list regardless of truncation
- The Cancel button is always pre-focused (safe default for destructive ops)

---

## 16. Edit Menu Loop

### Description

Multi-field editor that presents editable fields with current values and
allows the user to edit any field, then returns to the menu. Two approaches
are available: a Terminal.Gui `Dialog` with a sidebar `ListView` and content
area (preferred) and a Spectre suspension loop (fallback).

### Approach A: Terminal.Gui Dialog (Preferred)

A modal `Dialog` split into two regions: a `ListView` sidebar listing the
editable fields (with current values), and a content area that shows the
editing UI for the selected field. The sidebar acts as a persistent menu.
A "Done" button at the bottom confirms all changes.

### Mockup -- Dialog Approach (80 columns)

```
+-- Edit my-project ----------------------------------------+
|                                                            |
|  Fields              | Value                               |
|  --------------------|-------------------------------------|
|  ▶ Name              | [my-project                      ]  |
|    System prompt     |                                     |
|    Docker image      |                                     |
|    Directories       |                                     |
|                      |                                     |
|                              [ Cancel ]  [ Done ]          |
|                                                            |
+------------------------------------------------------------+
```

When the user selects a field in the sidebar, the content area on the right
updates to show the appropriate editing widget:
- **Text fields** (Name, Docker image): A `TextField` pre-filled with the
  current value
- **Multi-line text** (System prompt): A `TextView` for multi-line editing
- **Complex fields** (Directories): A sub-list with Add/Remove actions

The `ListView` sidebar uses `Theme.List.SelectedBackground` and
`Theme.List.SelectedText` for the current field. The `▶` indicator marks
the active field.

### Keyboard -- Dialog Approach

| Key            | Action                                    |
|----------------|-------------------------------------------|
| Up / Down      | Navigate fields in the sidebar            |
| Tab            | Move focus between sidebar and content    |
| Enter          | Focus the content area for the field      |
| Esc            | Cancel all changes and close              |
| Alt+D / Done   | Apply all changes and close               |

### Approach B: Spectre Suspension (Fallback)

### Mockup -- Spectre Approach

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

Presented during Terminal.Gui suspension. A repeating Spectre.Console
`SelectionPrompt` loop -- after each field edit, the menu re-displays with
updated values. Selecting "Done" exits the loop. Spectre prompts are used
throughout because this is an interactive wizard flow that requires raw
terminal input.

### When to Use

`/project edit`, `/jea edit`.

Use Approach A (Dialog) when:
- The edit flow is initiated from within the TUI
- You want to avoid repeated suspend/resume flicker
- The entity has 3+ editable fields

Use Approach B (Spectre) when:
- The edit flow already runs during a Spectre suspension
- The editing requires prompts that are difficult to reproduce in
  Terminal.Gui (e.g., directory path completion)

### Style Tokens

**Dialog approach**:
- `Theme.Modal.BorderScheme` (blue border) for the dialog
- `Theme.List.SelectedBackground`, `Theme.List.SelectedText` for sidebar
- `Theme.Semantic.Default` with `TextStyle.Bold` for entity name in title
- `Theme.Input.Text` (white) for `TextField` text in content area
- `Theme.Semantic.Muted` (dark gray) for current value labels

**Spectre approach**:
- Spectre selection prompt pattern
- Status message pattern for confirmations (output to console while suspended)
- Spectre `[bold]` for entity name in title

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

`ConversationBlockRenderer.Draw` handles `TokenUsageBlock`. Drawn as a single
line with `Theme.Semantic.Muted` (dark gray) and 2-space indent:
`  {input:N0} in / {output:N0} out / {total:N0} total`

Numbers are formatted with the `:N0` format specifier (thousand separators).

### Style Tokens

- `Theme.Semantic.Muted` (dark gray) for all text
- `:N0` format for thousand-separated numbers
- 2-space indent prepended to the line

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

Streaming tokens are accumulated into the last `AssistantTextBlock` in the
conversation view. `ConversationView.SetNeedsDraw()` is called to trigger a
redraw. The view auto-scrolls to keep the latest content visible. No Spectre
`Markup` processing — text is drawn as plain characters via `AddStr`.

Streaming updates are marshalled to the UI thread via `TguiApp.Invoke()`.
UI thread updates are batched to avoid saturating the main thread.

### Style Tokens

- `Theme.Semantic.Default` (white) for streamed text
- 2-space indent via the `AssistantTextBlock` drawing path

### Performance

- Streaming tokens are batched before triggering `SetNeedsDraw()`
- The conversation view caps history at `Theme.Layout.MaxConversationBlocks`
  (2000) to bound redraw cost
- Height measurement is re-run only for the updated block

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

In yellow text. The braille character animates at 100ms per frame (10-frame
cycle), the same spinner used for all active states. When the first token
arrives, the activity bar transitions to Streaming.

### When to Use

Between `RenderThinkingStart()` and the first token or `RenderThinkingStop()`.

### Rendering

`ActivityBarView` handles `ActivityState.Thinking`. Drawn with
`Theme.Semantic.Warning` (yellow) using `DrawSpinnerWithLabel` — spinner
character from `Theme.Symbols.SpinnerFrames` + `Theme.Text.ThinkingLabel`
(`"Thinking..."`). Spinner advances via a repeating 100ms timer. When the
first response token arrives, `SetState(ActivityState.Streaming)` transitions
the bar.

### Style Tokens

- `Theme.Semantic.Warning` (yellow) for thinking state
- `Theme.Symbols.SpinnerFrames` — 10-frame braille cycle at `Theme.Layout.SpinnerIntervalMs` (100ms)
- `Theme.Text.ThinkingLabel` = `"Thinking..."`

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

`ActivityBarView` handles `ActivityState.CancelHint`. Drawn with
`Theme.Semantic.Warning` (yellow), text `Theme.Text.CancelHint`
(`"Press Esc again to cancel"`), no spinner. The cancel state is managed by
`ChatInputView`: a one-shot `TguiApp.AddTimeout` of `Theme.Layout.CancelWindowMs`
(1000ms) reverts the bar by firing `CancelHintCleared`.

### Style Tokens

- `Theme.Semantic.Warning` (yellow) for cancel hint text
- `Theme.Text.CancelHint` = `"Press Esc again to cancel"`
- `Theme.Layout.CancelWindowMs` = 1000ms cancel window duration

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

A `StatusMessageBlock` with `MessageKind.Hint` appended to the conversation
view. `ConversationBlockRenderer` draws it with `Theme.Semantic.Muted`
(dark gray) and 2-space indent.

### Style Tokens

- `Theme.Semantic.Muted` (dark gray) via `MessageKind.Hint`
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

During an active TUI session, errors are appended to the conversation view
as a `StatusMessageBlock` with `MessageKind.Error`, drawn by
`ConversationBlockRenderer` with `Theme.Semantic.Error` (bright red).
Errors are also written to stderr via Spectre.Console markup (`[red bold]`
for `"Error:"`, `[yellow]` for `"Suggestion:"`) for non-TUI and piped
contexts. The `SpectreUserInterface.RenderError` method handles both paths.

### Style Tokens

- `Theme.Semantic.Error` (bright red) for the conversation view block
- Spectre `[red bold]Error:[/]` for stderr output
- Spectre `[yellow]Suggestion:[/]` for suggestion prefix on stderr

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

This pattern renders outside the TUI application — either before it starts
or after it has been disposed in the top-level exception handler in
`Program.cs`. It writes directly to the terminal using Spectre.Console
`AnsiConsole` (the TUI is not available). A Spectre `Panel` with rounded
border, red border color, and bold red header text.

### Style Tokens

- Spectre `BoxBorder.Rounded` with red border color
- Spectre `[red bold]` for header text
- Spectre `Padding(1, 1, 1, 1)`

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

A `BannerBlock` (wrapping a `BannerData` record) is the first block appended
to the conversation view at startup. `ConversationBlockRenderer` delegates to
`BannerRenderer.DrawBanner` and `BannerRenderer.MeasureBanner`.

`BannerRenderer` selects a rendering tier based on terminal dimensions:
- **Full** (`height >= 30`, unicode, `width >= 80`): ASCII art wordmark with
  sidebar + rule + info grid + status footer + hint line
- **Compact** (`height >= 15`): Inline `BOYDCODE` wordmark + rule + info grid
  + status footer + hint line
- **Minimal** (`10 <= height < 15`): Info grid + status footer (no wordmark,
  no blank lines)
- **Fallback** (`height < 10`): Single-line summary
- **Accessible** (`BannerData.Accessible`): Plain text, no art

### Style Tokens

- `Theme.Banner.BoydArt` = `BrightCyan` for "BOYD" art
- `Theme.Banner.CodeArt` = `BrightBlue` for "CODE" art
- `Theme.Banner.Version` = `Theme.Semantic.Muted` for tagline/version
- `Theme.Banner.InfoLabel` = `Theme.Semantic.Muted` (dark gray) for labels
- `Theme.Banner.InfoValue` = `Theme.Semantic.Info` (cyan) for values
- `Theme.Banner.StatusReady` = `Theme.Semantic.Success` (green) for "Ready"
- `Theme.Banner.StatusNotConfigured` = `Theme.Semantic.Warning` (yellow)
- `Theme.Symbols.Rule` for the separator rule line
- `Theme.Text.BannerHintWide/Medium/Narrow` for the responsive hint line

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

`ChatStatusBar` extends `View` and draws via `OnDrawingContent`. The entire
row is filled with `Theme.StatusBar.Fill` (dark background, rgb 30,30,30).
Status text is drawn at `x = 1` with `Theme.StatusBar.Status` (white on dark
background). Key hints are right-aligned, drawn with `Theme.StatusBar.Hint`
(dark gray on dark background). The hint string is chosen based on width:

- `width >= Theme.Layout.FullWidth` (120) → `Theme.Text.HintsWide`
- `width >= Theme.Layout.StandardWidth` (80) → `Theme.Text.HintsMedium`
- otherwise → `Theme.Text.HintsNarrow`

### Style Tokens

- `Theme.StatusBar.Fill` — dark background row fill (rgb 30,30,30)
- `Theme.StatusBar.Status` — white on dark background for metadata
- `Theme.StatusBar.Hint` — dark gray on dark background for key hints
- `Theme.Text.HintsWide/Medium/Narrow` — responsive hint strings

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

All busy states use the same animated 10-frame braille spinner at 100ms per
frame (`Theme.Layout.SpinnerIntervalMs`).

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

The braille character animates at 100ms per frame (10-frame cycle). The elapsed
time label advances each frame.

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

`ActivityBarView` extends `View` and draws via `OnDrawingContent`. State is
set by calling `SetState(ActivityState)`. The row is first cleared with
`Theme.Semantic.Muted`, then the state-specific content is drawn via `AddStr`:

- **Thinking**: `Theme.Semantic.Warning` (yellow), `DrawSpinnerWithLabel` with `Theme.Text.ThinkingLabel`
- **Streaming**: `Theme.Semantic.Info` (cyan), `DrawSpinnerWithLabel` with `Theme.Text.StreamingLabel`
- **Executing**: `Theme.Semantic.Info` (cyan), `DrawSpinnerWithLabel` with `Theme.Text.ExecutingLabel`
- **Cancel hint**: `Theme.Semantic.Warning` (yellow), `Theme.Text.CancelHint` (no spinner)
- **Modal**: `Theme.Semantic.Muted` (dark gray), `Theme.Text.EscToDismiss` (no spinner)
- **Idle**: `Theme.Semantic.Muted` full-width `Theme.Symbols.Rule` rule line

Busy states start a repeating `TguiApp.AddTimeout` at
`Theme.Layout.SpinnerIntervalMs` (100ms) that advances `_spinnerFrame` through
`Theme.Symbols.SpinnerFrames` (10-frame braille sequence). The timer is stopped
when the state leaves a busy state.

`InputSeparatorView` is a separate `View` below `ChatInputView` that draws a
static full-width `Theme.Symbols.Rule` rule line with `Theme.Semantic.Muted`.
It never changes.

### Style Tokens

- `Theme.Semantic.Warning` (yellow) for Thinking and CancelHint states
- `Theme.Semantic.Info` (cyan) for Streaming and Executing states
- `Theme.Semantic.Muted` (dark gray) for Idle, Modal, and separator
- `Theme.Symbols.SpinnerFrames` — 10-frame braille animation
- `Theme.Layout.SpinnerIntervalMs` = 100ms per frame
- `Theme.Text.ThinkingLabel/StreamingLabel/ExecutingLabel/CancelHint/EscToDismiss`

### Key Change from Old Architecture

In the old architecture, the Indicator Bar used a static `@` prefix for
Thinking and Streaming states. This has been replaced with the animated
braille spinner for all busy states.

---

## 27. Context Usage Bar

### Description

Visual bar chart showing context window utilization. Used in the `/context show`
modal overlay. The bar is drawn using Terminal.Gui's native drawing API with
`Theme.Chart.*` tokens for segment colors. Supports keyboard-driven segment
browsing via a `^` cursor beneath the bar.

### Mockup (80 columns)

```
  ████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
   ^
  Estimated usage by category
    ■ System prompt        2.1k tokens  (0.2%)
    ■ Tools                  458 tokens  (0.0%)
    ■ Messages             9.8k tokens  (1.0%)
    ■ Free space         887.6k tokens  (88.8%)
    ■ Compact buffer     100.0k tokens  (10.0%)
```

The `^` cursor indicates the focused segment. Left/Right arrows move
between segments; the corresponding legend row is highlighted with bold
text.

### When to Use

`/context show` modal overlay (Detail variant, pattern #11).

### Rendering

The context usage bar appears inside the `/context show` modal overlay. It
is rendered using Terminal.Gui's native drawing API (`SetAttribute`, `Move`,
`AddStr`) inside a custom `View`'s `OnDrawingContent` override. Each bar
segment is drawn by setting the attribute to the segment's `Theme.Chart.*`
color, then calling `AddStr` with the appropriate fill character. The legend
indicators use the same color attributes.

The bar is exactly 72 characters wide with a 2-space indent. Five segments
are drawn in order: System prompt, Tools, Messages, Free space, Compact
buffer. See `slash-context-show.md` for the full proportional sizing
algorithm and segment cursor interaction details.

### Style Tokens

- The modal window uses `Theme.Modal.BorderScheme` (blue border)
- `Theme.Semantic.Accent` -- system prompt bar segment
- `Theme.Chart.ToolsAttr` -- tools bar segment
- `Theme.Semantic.Success` -- messages bar segment
- `Theme.Chart.FreeSpaceAttr` -- free space bar segment
- `Theme.Chart.BufferAttr` -- compact buffer bar segment
- U+2588 full block for filled segments, U+2591 light shade for free space
- U+25A0 black square for legend indicators (drawn with segment color)
- `Theme.Semantic.Muted` for the segment cursor `^`

---

## 28. Interactive List

### Description

A `ListView` inside a modeless `Window` with keyboard navigation and an
Action Bar (pattern #29) at the bottom. This is the REPLACEMENT for Simple
Table (pattern #10) when the data is actionable -- i.e., the user needs to
select, open, edit, or delete individual items.

Columns are aligned text (no Spectre `Table` objects). Rows are navigable
with Up/Down arrows. Enter triggers the primary action, and single-letter
hotkeys trigger secondary actions.

### Mockup (80 columns)

```
+-- Projects -----------------------------------------------+
|                                                            |
|  Name              Dirs  Docker       Engine               |
|  ▶ my-project      3     python:3.12  InProcess            |
|    api-service     1     --           Container            |
|    _default        0     --           InProcess            |
|    data-pipeline   2     node:20      Container            |
|                                                            |
|  Enter: Open  e: Edit  d: Delete  n: New  Esc: Close      |
|                                                            |
+------------------------------------------------------------+
```

### Mockup -- Empty state (80 columns)

```
+-- JEA Profiles -------------------------------------------+
|                                                            |
|                                                            |
|           No profiles configured.                          |
|           Use /jea create to add one.                      |
|                                                            |
|                                                            |
|  n: New  Esc: Close                                        |
|                                                            |
+------------------------------------------------------------+
```

When the list has zero items, the list area shows a centered hint message
using `Theme.Semantic.Muted` (dark gray). The Action Bar still shows
applicable shortcuts (e.g., "n: New" remains available).

### When to Use

Any slash command output that lists entities the user can act on:
- `/conversations list` -- open, rename, delete conversations
- `/project list` -- open, edit, delete projects
- `/jea list` -- show, edit, delete JEA profiles
- `/provider list` -- show, remove providers
- `/agent list` -- show agent details

This replaces the Simple Table pattern (#10) for these use cases. Simple
Table remains for non-interactive, informational output inline in the
conversation view.

### Terminal.Gui Implementation

```csharp
var window = new Window
{
    Title = "Projects",
    X = Pos.Center(),
    Y = Pos.Center(),
    Width = Dim.Percent(80),
    Height = Dim.Percent(70),
    BorderStyle = LineStyle.Rounded,
};
window.Border?.SetScheme(Theme.Modal.BorderScheme);

// Column header (static label)
var header = new Label
{
    X = 1, Y = 1,
    Text = "Name              Dirs  Docker       Engine",
};
window.Add(header);

// List view for data rows
var listView = new ListView(items)
{
    X = 1, Y = 2,
    Width = Dim.Fill(1),
    Height = Dim.Fill(2), // leave room for action bar
};
window.Add(listView);

// Action bar at bottom
var actionBar = new Label
{
    X = 1, Y = Pos.AnchorEnd(2),
    Width = Dim.Fill(1),
    Text = "Enter: Open  e: Edit  d: Delete  n: New  Esc: Close",
};
window.Add(actionBar);
```

### Keyboard

| Key       | Action                                      |
|-----------|---------------------------------------------|
| Up / k    | Move selection up                           |
| Down / j  | Move selection down                         |
| Enter     | Primary action (open, show)                 |
| e         | Edit (secondary action)                     |
| d         | Delete (opens Delete Confirmation, #15)     |
| n         | New / Create                                |
| /         | Focus search/filter field (if present, #30) |
| Esc       | Close the window                            |

Single-letter hotkeys are handled in the window's `OnKeyDown` override.
They fire only when the `ListView` has focus (not when a sub-dialog is open).

### Style Tokens

- `Theme.Modal.BorderScheme` (blue border) for the window
- `Theme.List.SelectedBackground` -- background color for the highlighted row
- `Theme.List.SelectedText` -- text color for the highlighted row
- `Theme.Semantic.Muted` (dark gray) for column headers and em-dash empty cells
- `Theme.Semantic.Default` (white) for data cell text
- `Theme.Symbols.Arrow` (`\u25b6`) for the current-row indicator
- `\u2014` em-dash for empty / not-set cells

### Edge Cases

- **Single item**: Still shows the list with one row; action bar is full
- **Many items (> viewport)**: `ListView` scrolls natively; consider adding
  a Scroll Position Indicator (pattern #33)
- **Narrow terminal (< 80 cols)**: Truncate columns right-to-left (keep
  Name always visible, drop Engine, then Docker, then Dirs)
- **Long entity names**: Truncate with ellipsis at column width

### Accessibility

In accessible mode, the list renders as a numbered text list:

```
=== Projects ===
1. my-project (3 dirs, InProcess)
2. api-service (1 dir, Container)
Actions: Enter=Open, e=Edit, d=Delete, n=New, Esc=Close
=== End Projects ===
```

Screen readers announce each row as the user navigates.

---

## 29. Action Bar

### Description

A horizontal row of keyboard shortcut hints at the bottom of a `Window` or
`Dialog`. Provides discoverable, at-a-glance guidance for available actions.

### Mockup (80 columns)

```
  Enter: Open  e: Edit  d: Delete  n: New  Esc: Close
```

### Mockup (compact, < 60 columns)

```
  Enter: Open  d: Delete  Esc: Close
```

At narrow widths, less-important hints are hidden. The priority order
(right-most items are dropped first) is:

1. Esc: Close (always shown)
2. Enter: primary action (always shown)
3. Secondary actions in importance order (shown if space permits)

### Format

Each shortcut is rendered as `Key: Label` with a double-space separator
between entries. The key name uses `Theme.Semantic.Muted` (dark gray) and
the label uses `Theme.Semantic.Default` (white) for contrast.

Alternative format for emphasis: key drawn with `Theme.Semantic.Accent`
(blue) and label drawn with `Theme.Semantic.Muted` (dark gray).

### When to Use

- At the bottom of every Interactive List window (pattern #28)
- At the bottom of Form Dialogs (pattern #31) to show navigation hints
- At the bottom of Multi-Step Wizards (pattern #32) to show step navigation
- Anywhere a window has keyboard shortcuts that are not obvious

### Terminal.Gui Implementation

The Action Bar is a `View` (or `Label`) positioned at the bottom of the
containing window:

```csharp
var actionBar = new View
{
    X = 1,
    Y = Pos.AnchorEnd(2),
    Width = Dim.Fill(1),
    Height = 1,
};
```

Drawing is done via `OnDrawingContent`:
- Clear the row with `Theme.Semantic.Muted`
- For each shortcut hint, draw the key with `Theme.Semantic.Muted` and the
  label with `Theme.Semantic.Default`, separated by `: `
- Double-space (`"  "`) between entries
- Measure total width; drop rightmost entries until they fit

### Responsive Behavior

The action bar measures the available width and renders as many hints as
fit. Hints are ordered by priority (most important first). When the terminal
is too narrow for all hints:

1. Drop the rightmost (least important) hint
2. Repeat until all remaining hints fit within the available width
3. Never drop Esc/Close -- it is always the last entry and always shown

### Style Tokens

- `Theme.Semantic.Muted` (dark gray) for key names and separators
- `Theme.Semantic.Default` (white) for action labels
- Double-space (`"  "`) separator between entries
- Positioned at `Y = Pos.AnchorEnd(2)` (one row above the window border)

### Accessibility

In accessible mode, the action bar renders as a single line of text:
`Actions: Enter=Open, e=Edit, d=Delete, n=New, Esc=Close`. Screen readers
announce the full action list.

---

## 30. Search/Filter Field

### Description

A `TextField` positioned at the top of a list view that provides real-time
filtering. Each keystroke narrows the visible items. Used inside Interactive
List windows (pattern #28) when the list may be long.

### Mockup (80 columns) -- Inactive (placeholder)

```
+-- Conversations ------------------------------------------+
|                                                            |
|  [Type to filter...]                                       |
|                                                            |
|  Name                     Messages  Created                |
|  ▶ Auth module refactor   42        2026-02-28 14:30       |
|    API design review      18        2026-02-27 09:15       |
|    Docker setup            6        2026-02-26 16:45       |
|    Initial project setup  12        2026-02-25 11:00       |
|                                                            |
|  Enter: Open  r: Rename  d: Delete  /: Filter  Esc: Close |
|                                                            |
+------------------------------------------------------------+
```

### Mockup -- Active (with filter text)

```
+-- Conversations ------------------------------------------+
|                                                            |
|  [docker                                                ]  |
|                                                            |
|  Name                     Messages  Created                |
|  ▶ Docker setup            6        2026-02-26 16:45       |
|                                                            |
|                                                            |
|                                                            |
|                                                            |
|  Enter: Open  r: Rename  d: Delete  Esc: Clear  Esc: Close|
|                                                            |
+------------------------------------------------------------+
```

### Mockup -- No matches

```
+-- Conversations ------------------------------------------+
|                                                            |
|  [xyzzy                                                 ]  |
|                                                            |
|                                                            |
|           No matching conversations.                       |
|                                                            |
|                                                            |
|                                                            |
|  Esc: Clear filter  Esc Esc: Close                         |
|                                                            |
+------------------------------------------------------------+
```

When the filter has no matches, the list area shows a centered hint message
in `Theme.Semantic.Muted` (dark gray). The action bar updates to show that
Esc clears the filter first.

### When to Use

Inside an Interactive List window (pattern #28) when the list may contain
more than ~10 items. Not needed for short lists (< 10 items).

- `/conversations list` -- may have many conversations
- `/agent list` -- typically short, filter not needed
- `/project list` -- typically short, filter not needed

### Behavior

1. The filter field is initially inactive, showing placeholder text
   (`Theme.Semantic.Muted`, italic) like "Type to filter..."
2. When the user presses `/` (while the list has focus), focus moves to the
   filter field
3. Each keystroke updates the filter; the list is filtered in real-time
   using case-insensitive substring matching on the primary column (Name)
4. Down arrow or Enter moves focus from the filter field back to the list
5. First Esc (while filter field has text): clears the filter text and
   restores the full list, focus returns to the list
6. Second Esc (or Esc with empty filter): dismisses the window

### Terminal.Gui Implementation

```csharp
var filterField = new TextField
{
    X = 1, Y = 1,
    Width = Dim.Fill(1),
    Height = 1,
};
filterField.HasFocus = false; // Initially unfocused

// Placeholder via OnDrawingContent when Text is empty
// Filter on TextChanged event:
filterField.TextChanged += (sender, args) =>
{
    var filter = filterField.Text?.ToString() ?? "";
    listView.SetSource(
        allItems.Where(i => i.Name.Contains(filter,
            StringComparison.OrdinalIgnoreCase)).ToList());
};
```

### Keyboard

| Key             | Context         | Action                          |
|-----------------|-----------------|---------------------------------|
| / (slash)       | List focused    | Move focus to filter field      |
| Any printable   | Filter focused  | Add character, filter list      |
| Backspace       | Filter focused  | Remove character, update filter |
| Down / Enter    | Filter focused  | Move focus back to list         |
| Esc             | Filter has text | Clear filter, return to list    |
| Esc             | Filter empty    | Dismiss window                  |

### Style Tokens

- `Theme.Input.Text` (white) for filter text
- `Theme.Semantic.Muted` with italic for placeholder text ("Type to filter...")
- `Theme.Semantic.Muted` (dark gray) for "No matching..." empty state
- Position: `X = 1, Y = 1` inside the window (above the column header)

### Edge Cases

- **Empty initial list**: Filter field is hidden; only the empty state message
  shows (from Interactive List pattern #28)
- **Filter clears to empty**: Restores full unfiltered list
- **Filter field width**: Fills the window width minus borders
- **Special characters in filter**: Treated as literal characters, no regex

### Accessibility

In accessible mode, the filter field is announced as "Filter: {text}" and
the filtered count is announced: "Showing 3 of 42 conversations."

---

## 31. Form Dialog

### Description

A `Dialog` containing labeled `TextField`s, `ListView` selectors, and
Ok/Cancel buttons for structured data input. This REPLACES Spectre suspension
prompts for multi-field text input, selection, and confirmation flows.

### Mockup (80 columns) -- Single field

```
+-- Create Project -----------------------------------------+
|                                                            |
|  Name:  [                                               ]  |
|                                                            |
|                              [ Cancel ]  [ Create ]        |
|                                                            |
+------------------------------------------------------------+
```

### Mockup (80 columns) -- Multi-field

```
+-- Create Project -----------------------------------------+
|                                                            |
|  Name:          [my-project                             ]  |
|  Docker image:  [python:3.12                            ]  |
|  Engine:        [InProcess                              ]  |
|                                                            |
|  System prompt:                                            |
|  [You are an expert Python developer working on         ]  |
|  [the my-project application.                           ]  |
|  [                                                      ]  |
|                                                            |
|                              [ Cancel ]  [ Create ]        |
|                                                            |
+------------------------------------------------------------+
```

### Mockup -- With validation error

```
+-- Create Project -----------------------------------------+
|                                                            |
|  Name:          [                                       ]  |
|                 Name cannot be empty.                       |
|  Docker image:  [python:3.12                            ]  |
|                                                            |
|                              [ Cancel ]  [ Create ]        |
|                                                            |
+------------------------------------------------------------+
```

Validation error text is drawn with `Theme.Semantic.Error` (bright red)
immediately below the invalid field.

### When to Use

- `/project create` -- multi-field entity creation
- `/provider setup` -- API key, model, and endpoint configuration
- `/jea create` -- profile name and initial commands
- Any single-field or multi-field input that was previously handled by
  Spectre text prompts during suspension

### Terminal.Gui Implementation

```csharp
var dialog = new Dialog
{
    Title = "Create Project",
    Width = Dim.Percent(70),
    Height = Dim.Auto(DimAutoStyle.Content),
    BorderStyle = LineStyle.Rounded,
};
dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

// Labels and fields
var nameLabel = new Label("Name:") { X = 2, Y = 1 };
var nameField = new TextField
{
    X = 18, Y = 1,
    Width = Dim.Fill(2),
};

var dockerLabel = new Label("Docker image:") { X = 2, Y = 3 };
var dockerField = new TextField
{
    X = 18, Y = 3,
    Width = Dim.Fill(2),
};

dialog.Add(nameLabel, nameField, dockerLabel, dockerField);

var cancelButton = new Button("Cancel");
var createButton = new Button("Create") { IsDefault = true };
dialog.AddButton(cancelButton);
dialog.AddButton(createButton);

// Validation on Create
createButton.Accepting += (sender, args) =>
{
    if (string.IsNullOrWhiteSpace(nameField.Text?.ToString()))
    {
        // Show inline validation error
        args.Cancel = true;
    }
};
```

### Layout Rules

1. **Labels**: Left-aligned at `X = 2`. Text uses `Theme.Semantic.Default`
   with `TextStyle.Bold`.
2. **Fields**: Aligned at a consistent X offset (typically `X = 18` or
   computed from the longest label + 2). `Width = Dim.Fill(2)` to maintain
   right margin.
3. **Multi-line fields** (System prompt): A `TextView` placed below its
   label, spanning the full width with `Height = 3` or more.
4. **Validation errors**: A `Label` below the field, `Theme.Semantic.Error`
   (bright red). Hidden when valid. Shown on Create/Ok button press.
5. **Buttons**: Standard Dialog button area at the bottom.

### Keyboard

| Key        | Action                                    |
|------------|-------------------------------------------|
| Tab        | Move to next field or button               |
| Shift+Tab  | Move to previous field or button           |
| Enter      | Confirm (when Ok/Create button focused)    |
| Esc        | Cancel and close dialog                    |
| Ctrl+A     | Select all text in focused `TextField`     |

### Style Tokens

- `Theme.Modal.BorderScheme` (blue border) for the dialog
- `Theme.Semantic.Default` with `TextStyle.Bold` for field labels
- `Theme.Input.Text` (white) for `TextField` text
- `Theme.Semantic.Error` (bright red) for validation error messages
- `Theme.Semantic.Muted` (dark gray) for hint text below optional fields

### Edge Cases

- **Optional fields**: Show `(optional)` hint in `Theme.Semantic.Muted`
  after the label
- **Default values**: Pre-fill `TextField.Text` with the default
- **Long labels**: If labels are long, stack label above field instead of
  side-by-side
- **Narrow terminal (< 80 cols)**: Switch to stacked layout (label on one
  line, field on the next) when `width < Theme.Layout.StandardWidth`
- **Secret fields** (API keys): Use a `TextField` with `Secret = true`

### Accessibility

Labels are associated with their fields via Tab order. Screen readers
announce the label when the field gains focus. Validation errors are
announced immediately when they appear.

---

## 32. Multi-Step Wizard

### Description

A `Dialog` that progresses through numbered steps, with a step indicator at
the top, step-specific content in the middle, and Back/Next/Cancel buttons
at the bottom. Used for multi-field workflows that benefit from guided,
sequential input.

### Mockup (80 columns) -- Step 1

```
+-- Provider Setup -----------------------------------------+
|                                                            |
|  Step 1 of 3: Choose Provider                              |
|  --------------------------------------------------------  |
|                                                            |
|    Anthropic                                               |
|  ▶ Gemini                                                  |
|    OpenAI                                                  |
|    Ollama                                                  |
|                                                            |
|                                                            |
|  [ Cancel ]                                    [ Next > ]  |
|                                                            |
+------------------------------------------------------------+
```

### Mockup (80 columns) -- Step 2

```
+-- Provider Setup -----------------------------------------+
|                                                            |
|  Step 2 of 3: Authentication                               |
|  --------------------------------------------------------  |
|                                                            |
|  API key:  [************************************        ]  |
|                                                            |
|  Model:    [gemini-2.5-pro                              ]  |
|                                                            |
|                                                            |
|  [ Cancel ]                          [ < Back ] [ Next > ] |
|                                                            |
+------------------------------------------------------------+
```

### Mockup (80 columns) -- Step 3 (Summary)

```
+-- Provider Setup -----------------------------------------+
|                                                            |
|  Step 3 of 3: Confirm                                      |
|  --------------------------------------------------------  |
|                                                            |
|  Provider:   Gemini                                        |
|  Model:      gemini-2.5-pro                                |
|  API key:    ****...****                                   |
|                                                            |
|  This will set Gemini as the active provider.              |
|                                                            |
|  [ Cancel ]                          [ < Back ] [ Done ]   |
|                                                            |
+------------------------------------------------------------+
```

### When to Use

- `/provider setup` -- provider selection, authentication, confirmation
- `/project create` (future) -- if project creation grows beyond 2-3 fields
- Any workflow with 3-6 sequential steps that benefit from guided input

Do NOT use for:
- Simple 1-2 field inputs (use Form Dialog, pattern #31)
- Flows with fewer than 3 steps (use Form Dialog)
- Flows with more than 6 steps (break into sub-wizards)

### Anatomy

1. **Step indicator**: `"Step N of M: {step title}"` drawn with
   `Theme.Semantic.Default` (white, bold) at `Y = 1`. Below it, a dim
   horizontal rule (`Theme.Symbols.Rule`) separates the indicator from
   the step content.
2. **Content area**: The middle region holds step-specific views --
   `ListView` for selections, `TextField`/`TextView` for input, `Label`
   for summaries. Each step swaps the content area's children.
3. **Button bar**: Cancel (always), Back (step 2+), Next/Done (always).
   "Done" replaces "Next" on the final step.

### Terminal.Gui Implementation

```csharp
var dialog = new Dialog
{
    Title = "Provider Setup",
    Width = Dim.Percent(70),
    Height = Dim.Percent(60),
    BorderStyle = LineStyle.Rounded,
};
dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

// Step indicator
var stepLabel = new Label
{
    X = 2, Y = 1,
    Width = Dim.Fill(2),
};

// Step separator
var stepRule = new Label
{
    X = 2, Y = 2,
    Width = Dim.Fill(2),
    // Draw dim rule characters
};

// Content area (swapped per step)
var contentArea = new View
{
    X = 2, Y = 4,
    Width = Dim.Fill(2),
    Height = Dim.Fill(2),
};

dialog.Add(stepLabel, stepRule, contentArea);

// Navigation buttons
var cancelButton = new Button("Cancel");
var backButton = new Button("< Back");
var nextButton = new Button("Next >") { IsDefault = true };
dialog.AddButton(cancelButton);
dialog.AddButton(backButton);
dialog.AddButton(nextButton);
```

Step transitions are managed by a state machine:

```csharp
private void GoToStep(int step)
{
    _currentStep = step;
    stepLabel.Text = $"Step {step} of {_totalSteps}: {_stepTitles[step - 1]}";
    backButton.Visible = step > 1;
    nextButton.Text = step == _totalSteps ? "Done" : "Next >";

    contentArea.RemoveAll();
    contentArea.Add(_stepViews[step - 1]);
    contentArea.SetNeedsDraw();
}
```

### Keyboard

| Key        | Action                                    |
|------------|-------------------------------------------|
| Tab        | Move between fields within the step       |
| Shift+Tab  | Move to previous field or button          |
| Enter      | Confirm (Next/Done when button focused)   |
| Esc        | Cancel entire wizard                      |
| Alt+B      | Back (same as clicking Back button)       |
| Alt+N      | Next (same as clicking Next button)       |

### Step Validation

Each step validates its inputs before allowing Next. If validation fails:
- The Next button does not advance
- Validation errors appear inline (same pattern as Form Dialog, #31)
- Focus moves to the first invalid field

### Style Tokens

- `Theme.Modal.BorderScheme` (blue border) for the dialog
- `Theme.Semantic.Default` with `TextStyle.Bold` for step indicator text
- `Theme.Semantic.Muted` (dark gray) for the step separator rule
- `Theme.Symbols.Rule` (`\u2500`) for the separator character
- Step content uses the same tokens as Form Dialog (#31) for fields
- Step content uses the same tokens as Selection Prompt (#12) for lists

### Edge Cases

- **Resize during wizard**: Content area adjusts via `Dim.Fill`; list views
  remain scrollable
- **Back from step 3**: Restores step 2 with previously entered values
  preserved
- **Cancel at any step**: Confirmation dialog if any data has been entered;
  immediate close if no data entered
- **Narrow terminal (< 80 cols)**: Step indicator wraps to two lines;
  content area adjusts

### Accessibility

In accessible mode, step transitions are announced: "Step 2 of 3:
Authentication". Each field is announced with its label when focused.
The step indicator is always the first element read by a screen reader.

---

## 33. Scroll Position Indicator

### Description

A small position display showing the current scroll location within any
scrollable view. Provides orientation in long content without consuming
significant screen space.

### Format Options

Three display formats, chosen based on context:

| Format           | Example     | Best For                     |
|------------------|-------------|------------------------------|
| Line / Total     | `42/350`    | Conversation view, code      |
| Percentage       | `12%`       | Modal overlays, long text    |
| Proportional bar | `[===    ]` | Narrow spaces, visual scan   |

### Mockup -- Conversation view (bottom-right corner)

```
  I can see the project structure. Let me examine the main
  configuration file to understand the current settings,
  then I'll make the changes you requested.

  I'll start by looking at the authentication module...

  The auth module currently uses a basic token-based approach.
  I recommend switching to OAuth2 for better security.         42/350
```

The indicator appears in the bottom-right corner of the scrollable view,
overlaid on the content. It is only visible when the content exceeds the
viewport height (i.e., when there is something to scroll).

### Mockup -- Modal overlay (bottom-right, percentage)

```
+-- Help ---------------------------------------------------+
|                                                            |
|  /quit                   Exit the session (also: /exit)    |
|  /project                Manage projects                   |
|    create [name]           Create a new project            |
|    list                    List all projects               |
|    show [name]             Show project details            |
|    edit [name]             Edit project settings           |
|    delete [name]           Delete a project                |
|                                                        35% |
+------------------------------------------------------------+
```

### Mockup -- Not visible (content fits viewport)

When all content is visible within the viewport (no scrolling needed), the
indicator is hidden entirely. No "100%" or "1/1" -- just absence.

### When to Use

- **Conversation view**: Line/Total format (`42/350`) when content exceeds
  the viewport. Updates on each scroll event and when new content arrives.
- **Modal overlays**: Percentage format (`35%`) when modal content exceeds
  the window height. Applies to all three modal variants (Text, List,
  Detail -- pattern #11).
- **Interactive List** (pattern #28): Line/Total format for lists with many
  rows (e.g., `3/42`).

### Terminal.Gui Implementation

The indicator is drawn as part of the view's `OnDrawingContent` method,
after the main content is drawn. It overlays the bottom-right corner:

```csharp
// In OnDrawingContent, after drawing main content:
if (ContentSize.Height > Viewport.Height)
{
    var position = Viewport.Y;
    var total = ContentSize.Height;
    var text = $"{position + Viewport.Height}/{total}";

    // Draw in the bottom-right corner
    var x = Viewport.Width - text.Length - 1;
    var y = Viewport.Height - 1;
    SetAttribute(Theme.Semantic.Muted);
    Move(x, y);
    AddStr(text);
}
```

### Calculation

- **Line/Total**: `{topVisibleLine + viewportHeight}/{totalLines}`
- **Percentage**: `{(topVisibleLine + viewportHeight) * 100 / totalLines}%`
- **Proportional bar**: Map the viewport position to a fixed-width bar
  (e.g., 8 characters wide)

### Style Tokens

- `Theme.Semantic.Muted` (dark gray) for the indicator text
- Positioned at `(Viewport.Width - textLength - 1, Viewport.Height - 1)`
- Only rendered when `ContentSize.Height > Viewport.Height`

### Edge Cases

- **At top of content**: Shows `12/350` (viewport bottom line / total)
- **At bottom of content**: Shows `350/350` (or `100%`)
- **Content exactly fills viewport**: Indicator is hidden
- **Viewport height changes** (resize): Indicator recalculates
- **Very long content**: Line count may be 5+ digits; ensure the indicator
  does not overlap meaningful content (shift left if needed)
- **Streaming content**: Total line count changes as tokens arrive; the
  indicator updates each time the conversation view redraws

### Accessibility

In accessible mode, the scroll position is not rendered visually (it would
add noise for screen readers). Instead, a keyboard shortcut (e.g.,
Ctrl+Shift+P) announces the current position: "Line 42 of 350."
