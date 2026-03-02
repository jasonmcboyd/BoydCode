# Style Tokens (Prescriptive)

This document defines the REQUIRED visual language for BoydCode v2. Every color,
weight, symbol, spacing value, and border style has exactly one semantic meaning.
When building new UI, find the existing token. Do not invent new ones.

This replaces the previous descriptive style-tokens document. All values here
are prescriptive -- they describe what the app SHOULD use, not what it currently
uses.

---

## 1. Semantic Color System

### 1.1 The Six Colors

BoydCode uses exactly six semantic colors. Every use of color in the application
maps to one of these. No exceptions.

| Token    | Spectre Markup | Spectre Object     | Meaning                                |
|----------|----------------|--------------------|----------------------------------------|
| success  | `[green]`      | `Color.Green`      | Completed, allowed, ready, confirmed   |
| error    | `[red]`        | `Color.Red`        | Failed, denied, broken, destructive    |
| warning  | `[yellow]`     | `Color.Yellow`     | Caution, degraded, attention needed    |
| info     | `[cyan]`       | `Color.Cyan`       | Data values, identifiers, paths, names |
| accent   | `[blue]`       | `Color.Blue`       | Brand, interactive elements, commands  |
| muted    | `[dim]`        | `Style.Plain.Dim()`| Metadata, hints, secondary, disabled   |

### 1.2 Color Rules

1. **Never use color alone.** Every colored element MUST have a text label,
   symbol, or structural cue that conveys the same meaning without color.
   This serves NO_COLOR users and colorblind users (~8% of males).

2. **One color per semantic.** Green means success. Period. Do not use green
   for "active provider" or "editable field" -- those are not "success."

3. **Stick to ANSI 4-bit colors.** The six tokens above all map to standard
   ANSI colors that adapt to the user's terminal theme. Do not use extended
   colors like `mediumpurple1` or `darkorange` -- they have fixed RGB values
   that clash with many terminal themes.

4. **No color combinations.** Do not combine colors (e.g., `[yellow on red]`).
   Use weight (bold, dim) for emphasis within a color.

5. **Respect NO_COLOR.** When the `NO_COLOR` environment variable is set,
   Spectre.Console strips all color automatically. Design every element to
   remain meaningful without color.

### 1.3 Color Modifiers

Colors can be combined with weight modifiers for emphasis:

| Combination      | Markup           | Usage                                 |
|------------------|------------------|---------------------------------------|
| Error emphasis   | `[red bold]`     | Error prefix: `[red bold]Error:[/]`   |
| Brand primary    | `[bold cyan]`    | "BOYD" in startup banner              |
| Brand secondary  | `[bold blue]`    | "CODE" in startup banner              |
| Warning emphasis | `[yellow bold]`  | "Not configured" in startup banner    |

No other color + modifier combinations are permitted. If you need emphasis,
use `[bold]` without color.

### 1.4 Threshold Colors

For numeric indicators that change color at breakpoints:

| Condition    | Token   | Example                  |
|--------------|---------|--------------------------|
| < 50%        | success | Context usage bar (green) |
| 50% - 79%    | warning | Context usage bar (yellow)|
| >= 80%       | error   | Context usage bar (red)   |

### 1.5 NO_COLOR Compliance

When `NO_COLOR` is set or the terminal reports no color support:

- All `[color]` markup is stripped (Spectre handles this automatically)
- `[bold]` and `[dim]` MAY still render as font weights (terminal-dependent)
- Status symbols (checkmark, cross) remain visible as text
- View borders render as ASCII fallbacks
- All information remains conveyed through text, symbols, and structure

### 1.6 Extended Color Exception: Grey23 Background

The User Message Block uses `Color.Grey23` as a background tint. This is the
ONLY permitted use of an extended (non-4-bit) color in the application.

```csharp
new Panel(new Markup($"> {Markup.Escape(text)}"))
    .Border(BoxBorder.None)
    .Padding(1, 0, 1, 0)
    .Style(new Style(background: Color.Grey23));
```

**Rationale**: A subtle background tint is the most effective way to visually
distinguish user messages from assistant text without adding borders, color to
the text, or other visual clutter. Grey23 is a near-black shade that works on
both dark and light terminal themes -- on dark themes it appears as a slightly
lighter stripe, on light themes as a slightly darker stripe. The effect is
subtle enough to degrade gracefully:

- **NO_COLOR / ColorSystem.NoColors**: Background tint is not rendered. The
  `>` prefix still identifies user messages.
- **Standard 16-color terminals**: Grey23 may render as the closest available
  color (typically unchanged foreground). The `>` prefix is the fallback.
- **Accessible mode**: No background tint; plain `> {text}`.

No other extended colors are permitted. Do not add RGB backgrounds, gradients,
or 256-color foregrounds elsewhere.

---

## 2. Typography Hierarchy

### 2.1 Four Weight Levels

Every text element in the application uses exactly one of these four levels:

```
Level 1: [bold]            Headings, entity names, primary actions
Level 2: (plain/unstyled)  Body text, standard content, responses
Level 3: [dim]             Metadata, timestamps, secondary info, labels
Level 4: [dim italic]      Hints, ephemeral status, contextual tips
```

### 2.2 Level Usage Rules

**Level 1 -- Bold**
- Section headings in modal overlays
- Entity names in confirmation messages: `Project [bold]my-project[/] created.`
- Table column headers
- The user prompt indicator `>`
- Panel headers
- Primary actions in help text

**Level 2 -- Plain**
- All conversation body text (user messages, assistant responses)
- Tool output content
- Table cell values
- Prompt response text
- Error message body (after the red prefix)

**Level 3 -- Dim**
- Info grid labels: `[dim]Provider[/]  Gemini`
- Timestamps and durations: `[dim]2.3s[/]`
- Token counts: `[dim]4,521 in / 892 out[/]`
- Status bar content
- Separator rule styles
- Line counts: `[dim]42 lines[/]`
- Empty state placeholders: `[dim](not set)[/]`
- Keyboard hint labels

**Level 4 -- Dim Italic**
- "Type a message to start, or /help for available commands."
- "Press Esc again to cancel"
- "/expand to show full output"
- Session resume notice

### 2.3 Prohibited Combinations

- Do not use `[italic]` without `[dim]`. Italic alone has no defined meaning.
- Do not use `[underline]`. It conflicts with hyperlink styling in some terminals.
- Do not use `[strikethrough]`. It has poor terminal support.
- Do not use `[bold dim]`. These are contradictory.

---

## 3. Status Symbols

### 3.1 Symbol Inventory

Every status indicator uses a Unicode symbol paired with colored text:

| Name     | Character | Codepoint | Markup               | Meaning              |
|----------|-----------|-----------|----------------------|----------------------|
| check    | \u2713    | U+2713    | `[green]\u2713[/]`   | Success, allowed     |
| cross    | \u2717    | U+2717    | `[red]\u2717[/]`     | Error, denied        |
| warning  | !         | U+0021    | `[yellow]![/]`       | Warning, caution     |
| bullet   | \u2022    | U+2022    | `\u2022`             | List item            |
| arrow    | \u25b6    | U+25B6    | `[dim]\u25b6[/]`     | Current/active       |
| dash     | \u2014    | U+2014    | `[dim]\u2014[/]`     | Not set, empty       |
| dash     | \u2014    | U+2014    | `[dim]\u2014[/]`     | Not set, empty       |

### 3.2 Activity Indicator

The Activity region (formerly "Indicator Bar") uses an animated braille spinner
for ALL busy states. The `@` static prefix from the old architecture is removed.
The Activity region shows a dim horizontal rule when idle and an animated
spinner during active turns. Between turns, there is no activity indicator.

- **All busy states** (Thinking, Streaming, Executing): An 8-frame braille
  spinner animates at 100ms per frame, providing consistent animated feedback
  that communicates active processing.

| State       | Activity Region Content                          | Color    |
|-------------|--------------------------------------------------|----------|
| Thinking    | `⠿ Thinking...` (spinner animates)               | `[yellow]`|
| Streaming   | `⠿ Streaming...` (spinner animates)              | `[cyan]` |
| Executing   | `⠿ Executing... (2.3s)` (spinner animates)       | `[cyan]` |
| Cancel hint | `Press Esc again to cancel`                      | `[yellow]`|
| Modal open  | `Esc to dismiss`                                 | `[dim]`  |

Braille spinner frame sequence (⠿ ⠻ ⠽ ⠾ ⠷ ⠯ ⠟ ⠾), 100ms per frame. The
spinner runs continuously during all active states.

**Separator**: Below the Activity region sits a static `new Rule().RuleStyle("dim")`.
It never changes content.

In accessible mode (`BOYDCODE_ACCESSIBLE=1`), all animated indicators are
replaced with static text: `[Thinking...]`, `[Streaming...]`,
`[Executing... (2.3s)]`.

### 3.3 Deprecated Symbols

The following symbols from v1 are removed or replaced in v2:

| Old Symbol | Replacement     | Reason                              |
|------------|-----------------|-------------------------------------|
| `v` (Latin)| `\u2713` (check)| Proper checkmark, universally clear |
| `*` (asterisk) | `\u25b6` (arrow) | Clearer "current item" indicator |
| `--` (double hyphen) | `\u2014` (em dash) | Proper typographic dash |

Note: The braille spinner is now used for ALL active states (Thinking, Streaming,
and Executing). The `@` static prefix for Thinking/Streaming has been replaced
by the animated braille spinner to provide consistent visual feedback.

### 3.4 Context Chart Symbols

Used exclusively in `/context show` visualization:

| Symbol      | Codepoint | Usage                     |
|-------------|-----------|---------------------------|
| Full block  | U+2588    | Filled bar segments       |
| Light shade | U+2591    | Free space segments       |
| Black square| U+25A0    | Legend color indicators    |

These are the only permitted characters for data visualization. No sparklines,
no ASCII art graphs.

---

## 4. Spacing and Padding

### 4.1 Indentation

| Level   | Width | Usage                                        |
|---------|-------|----------------------------------------------|
| Level 0 | 0     | Error/warning prefixes, rule separators     |
| Level 1 | 2     | Conversation content, status messages, tool results |
| Level 2 | 4     | Nested content within panels, sub-items     |

All indentation uses spaces, never tabs.

### 4.2 Vertical Spacing

| Pattern                           | Blank Lines | Implementation            |
|-----------------------------------|-------------|---------------------------|
| Between conversation turns        | 1           | Empty row in content      |
| Between text blocks within a turn | 0           | Continuous rendering      |
| Before section divider (Rule)     | 1           | `SpectreHelpers.Section()`|
| After section divider (Rule)      | 0           | Content follows directly  |
| After streaming complete          | 1           | Single blank line         |
| Between tool preview and result   | 0           | Continuous rendering      |
| Between content and modal         | 0           | Modal replaces content    |

### 4.3 Panel Padding

All panels use consistent internal padding:

| Panel Type        | Horizontal | Vertical | Implementation                              |
|-------------------|------------|----------|---------------------------------------------|
| Modal overlay     | 2          | 1        | 1 from Window border + 1 from content offset|
| Tool preview      | 1          | 0        | Content offset within conversation view      |
| Conversation msg  | 0          | 0        | No padding (borderless)                      |
| Crash panel       | 1          | 1        | Spectre Panel (rendered outside TUI)         |
| Info display      | 1          | 0        | Content offset                               |

### 4.4 Grid Column Spacing

| Grid Type       | Col 0 Pad        | Col 1 Pad     | Usage               |
|-----------------|-------------------|---------------|---------------------|
| Info grid       | PadLeft(2) PadRight(1) | PadRight(4) | Key-value display |
| Status bar      | PadLeft(0) PadRight(1) | PadRight(0) | Bottom bar        |

---

## 5. Border Styles

### 5.1 Three Permitted Borders

| Style          | Usage                                          | Implementation                    |
|----------------|------------------------------------------------|-----------------------------------|
| Rounded        | Modal overlays, tool call badges, detail panels| `LineStyle.Rounded` (Terminal.Gui)|
| None           | Conversation messages (invisible container)    | No border                         |
| Simple (Table) | All data tables                                | Column-aligned text               |

### 5.2 Border Colors

| Context            | Color (semantic) | Implementation                                      |
|--------------------|------------------|-----------------------------------------------------|
| Modal overlay      | Accent (blue)    | `Border.SetScheme(new Scheme(new Attribute(Blue)))` |
| Tool call badge    | Muted (grey)     | Native drawing with `DarkGray` attribute             |
| Crash panel        | Error (red)      | Spectre `Color.Red` (rendered outside TUI)           |
| All other panels   | Default (white)  | Inherited from parent scheme                         |

### 5.3 Rules (Horizontal Lines)

| Context            | Style                   | Implementation                  |
|--------------------|-------------------------|---------------------------------|
| Section divider    | Dim, left-justified     | Native drawing with dim rule    |
| Layout separator   | Dim, no title           | Dim `─` characters (static)     |
| Banner separator   | Dim, no title           | Dim `─` characters              |

---

## 6. Prompt Styling

### 6.1 Input Prompt

The input view is always visible in its dedicated screen region.

| Context          | Display       | Implementation                    |
|------------------|---------------|-----------------------------------|
| Idle (awaiting)  | `> `          | Input view with cursor            |
| Fallback mode    | `[bold blue]>[/] ` | Blocking line input          |

During active turns, the input view accepts typeahead input in dim
styling: `[dim]> {typed text}[/]`. When messages are queued, a yellow badge
`[N queued]` appears. The input handler routes display updates to the
input view during active turns.

### 6.2 Interactive Prompt Labels

All interactive prompts follow this convention:

- Field name highlighted in green: `"Project [green]name[/]:"`
- Optional hint in dim: `"Path [dim](Enter to finish)[/]:"`
- Default value shown: `"Model [dim](gemini-2.5-pro)[/]:"`
- Secret fields: `.Secret()` on TextPrompt
- Validation errors in red: `"[red]Name cannot be empty[/]"`

### 6.3 Selection Prompt Highlight

All SelectionPrompt and MultiSelectionPrompt use:
```csharp
.HighlightStyle(new Style(Color.Green))
```

---

## 7. Composite Patterns

### 7.1 Status Message Pattern

All status messages use these exact formats. SpectreHelpers methods enforce them.

```
Success:  "  [green]\u2713[/] {escaped message}"
Error:    "[red bold]Error:[/] {escaped message}"
Warning:  "[yellow]![/] [yellow]Warning:[/] {escaped message}"
Usage:    "[yellow]Usage:[/] {escaped message}"
Dim:      "[dim]{escaped message}[/]"
Cancelled:"[dim]Cancelled.[/]"
```

Note: Success uses U+2713 checkmark (not lowercase "v"). Error uses bold red
prefix. Warning includes the `!` symbol.

### 7.2 Error with Suggestion Pattern

```
[red bold]Error:[/] {error description}
  [yellow]Suggestion:[/] [dim]{suggestion text}[/]
```

### 7.3 Inline Entity Reference

Entity names referenced in messages use `[bold]`:
```
[green]\u2713[/] Project [bold]{name}[/] created.
[red bold]Error:[/] Project [bold]{name}[/] not found.
```

### 7.4 Tool Result Summary

```
Success:   "  [green]\u2713[/] [dim]Shell[/]  [dim]42 lines | 0.3s[/]"
Error:     "  [red]\u2717[/] [dim]Shell[/]  [dim]42 lines | 0.3s[/]"
Expand:    "  [dim italic]/expand to show full output[/]"
No output: "  [green]\u2713[/] [dim]Shell[/]  [dim]{truncated result}[/]"
```

### 7.5 Empty State Labels

All empty/unset values use the same pattern -- parenthesized, dim:

| Value        | Markup                |
|--------------|-----------------------|
| Not set      | `[dim](not set)[/]`   |
| None         | `[dim](none)[/]`      |
| Default      | `[dim](default)[/]`   |
| Ambient      | `[dim](ambient)[/]`   |
| Global       | `[dim](global)[/]`    |
| N/A          | `[dim]\u2014[/]`      |

The em dash (`\u2014`) is used in table cells where a text label would be verbose.
The parenthesized labels are used in prose context.

---

## 8. Responsive Tiers

### 8.1 Width Tiers

| Tier     | Width Range | Status Bar          | Conversation Margin | Tool Preview |
|----------|-------------|---------------------|---------------------|--------------|
| Full     | >= 120 col  | Full pipe-separated | 2-space indent      | Full width   |
| Standard | 80-119 col  | Abbreviated items   | 2-space indent      | Full width   |
| Compact  | < 80 col    | Minimal (provider + model only) | 1-space indent | Wrapped |

### 8.2 Height Tiers

The view hierarchy has 4 fixed rows (Activity + Input + Separator + StatusBar)
with the Conversation region taking all remaining vertical space.

| Tier     | Height Range | Banner          | Conversation Height | Status |
|----------|-------------|-----------------|---------------------|--------|
| Full     | >= 30 rows   | ASCII art       | Height - 4          | Full   |
| Standard | 15-29 rows   | Compact one-line| Height - 4          | Full   |
| Minimal  | 10-14 rows   | None            | Height - 4          | Abbreviated |
| Fallback | < 10 rows    | None            | No persistent view hierarchy | Inline |

### 8.3 Detection

```csharp
var width = Console.WindowWidth;
var widthTier = width >= 120 ? "full" : width >= 80 ? "standard" : "compact";

int height;
try { height = Console.WindowHeight; }
catch { height = 24; }
var heightTier = height >= 30 ? "full"
    : height >= 15 ? "standard"
    : height >= 10 ? "minimal"
    : "fallback";
```

---

## 9. Accessibility Requirements

### 9.1 Accessible Mode

When `BOYDCODE_ACCESSIBLE=1` environment variable is set:

- All animated indicators replaced with static text: `[Working]` not `@`
- All color removed (equivalent to NO_COLOR)
- All Unicode decorative characters replaced with ASCII equivalents
- All panels use ASCII borders (`BoxBorder.Ascii`)
- Tables use plain text separators
- Modal overlays announced with `===` delimiters
- Screen reader-friendly: output is sequential, no cursor repositioning

### 9.2 NO_COLOR Behavior

When `NO_COLOR` environment variable is set:

- All `[color]` markup stripped (Spectre.Console automatic)
- `[bold]` and `[dim]` may still render (terminal-dependent)
- Unicode symbols preserved (checkmark, cross, etc.)
- View structure preserved
- All information conveyed through text and structure

### 9.3 Non-Interactive Fallback

When the terminal does not support interactive input (e.g., piped stdin):

- No persistent view hierarchy (output renders sequentially)
- No persistent display (one-shot output)
- No async input handler (blocking line input)
- No modal overlays (slash commands render sequentially)
- No animated indicators (static text)
- All prompts require flag-based alternatives
