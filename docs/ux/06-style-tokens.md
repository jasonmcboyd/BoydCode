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

| Token    | Theme Constant          | Terminal.Gui                                    | Meaning                                |
|----------|-------------------------|-------------------------------------------------|----------------------------------------|
| success  | `Theme.Semantic.Success`| `new Attribute(ColorName16.Green, Color.None)`  | Completed, allowed, ready, confirmed   |
| error    | `Theme.Semantic.Error`  | `new Attribute(ColorName16.BrightRed, Color.None)` | Failed, denied, broken, destructive |
| warning  | `Theme.Semantic.Warning`| `new Attribute(ColorName16.Yellow, Color.None)` | Caution, degraded, attention needed    |
| info     | `Theme.Semantic.Info`   | `new Attribute(ColorName16.Cyan, Color.None)`   | Data values, identifiers, paths, names |
| accent   | `Theme.Semantic.Accent` | `new Attribute(ColorName16.Blue, Color.None)`   | Brand, interactive elements, commands  |
| muted    | `Theme.Semantic.Muted`  | `new Attribute(ColorName16.DarkGray, Color.None)` | Metadata, hints, secondary, disabled |

### 1.2 Color Rules

1. **Never use color alone.** Every colored element MUST have a text label,
   symbol, or structural cue that conveys the same meaning without color.
   This serves NO_COLOR users and colorblind users (~8% of males).

2. **One color per semantic.** Green means success. Period. Do not use green
   for "active provider" or "editable field" -- those are not "success."

3. **Prefer ANSI 4-bit colors.** The six tokens above all map to standard
   ANSI colors that adapt to the user's terminal theme. Custom RGB colors
   have fixed values that may clash with terminal themes. They are permitted
   only when approved under the criteria in section 1.6 (Custom Color
   Criteria). Do not use Spectre.Console named colors like `mediumpurple1`
   or `darkorange` -- use `Terminal.Gui.Drawing.Color` RGB constructors for
   any approved custom color.

4. **No color combinations.** Do not combine colors (e.g., `[yellow on red]`).
   Use weight (bold, dim) for emphasis within a color.

5. **Respect NO_COLOR.** When the `NO_COLOR` environment variable is set,
   Terminal.Gui honors the environment variable and omits color output.
   Design every element to remain meaningful without color.

### 1.3 Color Modifiers

Colors can be combined with weight modifiers for emphasis:

| Combination      | Theme Constant / Attribute                                          | Usage                              |
|------------------|---------------------------------------------------------------------|------------------------------------|
| Error emphasis   | `new Attribute(ColorName16.BrightRed, Color.None, TextStyle.Bold)` | Error prefix label                 |
| Brand primary    | `Theme.Banner.BoydArt` (`BrightCyan`, Bold)                        | "BOYD" in startup banner           |
| Brand secondary  | `Theme.Banner.CodeArt` (`BrightBlue`, Bold)                        | "CODE" in startup banner           |
| Warning emphasis | `Theme.Semantic.Warning`                                            | "Not configured" in startup banner |

No other color + modifier combinations are permitted. If you need emphasis,
apply `TextStyle.Bold` to the relevant `Attribute` without changing the color.

### 1.4 Threshold Colors

For numeric indicators that change color at breakpoints:

| Condition    | Token   | Example                  |
|--------------|---------|--------------------------|
| < 50%        | success | Context usage bar (green) |
| 50% - 79%    | warning | Context usage bar (yellow)|
| >= 80%       | error   | Context usage bar (red)   |

### 1.5 NO_COLOR Compliance

When `NO_COLOR` is set or the terminal reports no color support:

- Terminal.Gui omits all color; all `Attribute` color components are ignored
- `TextStyle.Bold` and `TextStyle.Underline` MAY still render (terminal-dependent)
- Status symbols (checkmark, cross) remain visible as text
- View borders render as ASCII fallbacks
- All information remains conveyed through text, symbols, and structure

### 1.6 Custom Color Criteria

The six semantic colors (section 1.1) use ANSI 4-bit `ColorName16` values that
adapt to the user's terminal theme. Custom RGB colors bypass this adaptation
and render with fixed values regardless of theme. They are permitted only when
all of the following criteria are met:

1. **No semantic color fits.** The visual element does not convey success,
   error, warning, info, accent, or muted meaning. It is purely structural
   (e.g., a background tint) or categorical (e.g., a chart segment).
2. **Graceful degradation.** The element MUST remain functional when color is
   absent (NO_COLOR, accessible mode, 16-color terminals). Text labels,
   symbols, or structural cues carry the meaning; the color is supplementary.
3. **Minimal contrast risk.** The chosen RGB value works acceptably on both
   dark and light terminal themes (near-black backgrounds are safe; saturated
   foreground colors are risky).
4. **Approved in this section.** Every custom RGB color MUST be listed in the
   approved table below. Do not introduce new RGB colors without adding them
   here first.

#### Approved Custom Colors

| Token                          | RGB Value           | Theme Constant               | Purpose                                  | Degradation                                                |
|--------------------------------|---------------------|------------------------------|------------------------------------------|------------------------------------------------------------|
| User message background        | `new Color(50,50,50)` | `Theme.User.Background`    | Subtle tint distinguishing user messages | `>` prefix identifies user messages without color          |
| Status bar background          | `new Color(30,30,30)` | `Theme.StatusBar.Background` | Differentiate status bar from content  | Status bar position (bottom row) identifies it structurally |
| Chart: Tools segment           | `new Color(147,112,219)` | `Theme.Chart.Tools`     | `/context show` bar -- tools category    | Legend text label identifies category without color        |
| Chart: Compact buffer segment  | `new Color(255,140,0)`  | `Theme.Chart.Buffer`    | `/context show` bar -- compact buffer    | Legend text label identifies category without color        |
| Chart: Free space segment      | `new Color(128,128,128)` | `Theme.Chart.FreeSpace` | `/context show` bar -- available tokens  | Light shade character (U+2591) distinguishes from filled   |

#### Implementation Notes

`ConversationBlockRenderer` applies `Theme.User.Text` and `Theme.User.Prefix`
when drawing `UserMessageBlock` rows via `SetAttribute` / `AddStr`.
`ChatStatusBar` uses `Theme.StatusBar.Background` for the bottom bar.
Chart colors are used by the `/context show` renderer for bar segments and
legend indicators.

**User message background rationale**: RGB `(50, 50, 50)` is a near-black
shade that works on both dark and light terminal themes -- on dark themes it
appears as a slightly lighter stripe, on light themes as a slightly darker
stripe. The effect is subtle enough to degrade gracefully:

- **NO_COLOR / no color support**: Background tint is not rendered. The
  `>` prefix still identifies user messages.
- **Standard 16-color terminals**: RGB color falls back to the nearest palette
  entry; the `>` prefix remains the primary differentiator.
- **Accessible mode**: No background tint; plain `> {text}`.

**Chart color rationale**: Data visualization categories (Tools, Compact buffer,
Free space) do not map to semantic meanings like success/error/warning. They
need distinct hues to be visually differentiable from each other and from the
semantic colors already used for System prompt (blue/accent) and Messages
(green/success) segments. The legend provides text labels alongside every
colored indicator, so the chart remains readable without color.

Do not add RGB backgrounds, gradients, or 256-color foregrounds beyond those
listed in the approved table above.

### 1.7 Chart Color Palette

The `/context show` stacked bar chart uses five colored segments. Two segments
reuse semantic colors; three use custom RGB values (approved in section 1.6).

| Segment        | Theme Constant           | Color Source                        | Bar Character |
|----------------|--------------------------|-------------------------------------|---------------|
| System prompt  | `Theme.Semantic.Accent`  | `ColorName16.Blue` (semantic)       | U+2588 (full block) |
| Tools          | `Theme.Chart.Tools`      | `new Color(147,112,219)` (custom)   | U+2588 (full block) |
| Messages       | `Theme.Semantic.Success` | `ColorName16.Green` (semantic)      | U+2588 (full block) |
| Free space     | `Theme.Chart.FreeSpace`  | `new Color(128,128,128)` (custom)   | U+2591 (light shade) |
| Compact buffer | `Theme.Chart.Buffer`     | `new Color(255,140,0)` (custom)     | U+2588 (full block) |

The chart palette is a closed set. Do not add new chart colors without updating
this table and the approved custom colors in section 1.6. Every chart segment
is accompanied by a legend row with a text label, so the chart remains
readable without color (see section 3.4 for chart symbols).

```csharp
// Theme.cs
internal static class Chart
{
  internal static readonly Color Tools     = new(147, 112, 219);
  internal static readonly Color FreeSpace = new(128, 128, 128);
  internal static readonly Color Buffer    = new(255, 140, 0);

  // Attributes for bar drawing
  internal static readonly Attribute ToolsAttr     = new(Tools, Color.None);
  internal static readonly Attribute FreeSpaceAttr = new(FreeSpace, Color.None);
  internal static readonly Attribute BufferAttr    = new(Buffer, Color.None);
}
```

Segments that reuse semantic colors use `Theme.Semantic.Accent` (System prompt)
and `Theme.Semantic.Success` (Messages) directly -- they are not duplicated in
`Theme.Chart`.

---

## 2. Typography Hierarchy

### 2.1 Four Weight Levels

Every text element in the application uses exactly one of these four levels:

```
Level 1: Bold (TextStyle.Bold)              Headings, entity names, primary actions
Level 2: Plain (no TextStyle)               Body text, standard content, responses
Level 3: Muted / dim (Theme.Semantic.Muted) Metadata, timestamps, secondary info
Level 4: Muted + italic                     Hints, ephemeral status, contextual tips
```

### 2.2 Level Usage Rules

**Level 1 -- Bold**
- Section headings in modal overlays
- Entity names in confirmation messages: `Project my-project created.` (name drawn Bold)
- Table column headers
- The user prompt indicator `>`
- Window title bars
- Primary actions in help text

**Level 2 -- Plain**
- All conversation body text (user messages, assistant responses)
- Tool output content
- Table cell values
- Prompt response text
- Error message body (after the red prefix)

**Level 3 -- Muted (`Theme.Semantic.Muted`)**
- Info grid labels: `Provider  Gemini` (label drawn with `Theme.Semantic.Muted`)
- Timestamps and durations: `2.3s`
- Token counts: `4,521 in / 892 out`
- Status bar hint text (`Theme.StatusBar.Hint`)
- Separator rule characters
- Line counts: `42 lines`
- Empty state placeholders: `(not set)`
- Keyboard hint labels

**Level 4 -- Muted Italic**
- "Type a message to start, or /help for available commands."
- "Press Esc again to cancel"
- "/expand to show full output"
- Session resume notice

### 2.3 Prohibited Combinations

- Do not use `TextStyle.Italic` without muted color. Italic alone has no defined meaning.
- Do not use `TextStyle.Underline` for emphasis. Underline is reserved for cursor display (`Theme.Input.Cursor`).
- Do not use `TextStyle.Strikethrough`. It has poor terminal support.
- Do not combine `TextStyle.Bold` with a muted/dim attribute. These are contradictory.

---

## 3. Status Symbols

### 3.1 Symbol Inventory

Every status indicator uses a Unicode symbol paired with a semantic color attribute.
Symbols are defined as constants in `Theme.Symbols`.

| Name     | Char | Codepoint | Theme Constant          | Attribute                  | Meaning          |
|----------|------|-----------|-------------------------|----------------------------|------------------|
| check    | ✓    | U+2713    | `Theme.Symbols.Check`   | `Theme.Semantic.Success`   | Success, allowed |
| cross    | ✗    | U+2717    | `Theme.Symbols.Cross`   | `Theme.Semantic.Error`     | Error, denied    |
| warning  | !    | U+0021    | (literal `!`)           | `Theme.Semantic.Warning`   | Warning, caution |
| bullet   | •    | U+2022    | (literal `\u2022`)      | default                    | List item        |
| arrow    | ▶    | U+25B6    | (literal `\u25b6`)      | `Theme.Semantic.Muted`     | Current/active   |
| dash     | —    | U+2014    | (literal `\u2014`)      | `Theme.Semantic.Muted`     | Not set, empty   |
| rule     | ─    | U+2500    | `Theme.Symbols.Rule`    | `Theme.Semantic.Muted`     | Horizontal rule  |

### 3.2 Activity Indicator

The Activity region (formerly "Indicator Bar") uses an animated braille spinner
for ALL busy states. The `@` static prefix from the old architecture is removed.
The Activity region shows a dim horizontal rule when idle and an animated
spinner during active turns. Between turns, there is no activity indicator.

- **All busy states** (Thinking, Streaming, Executing): A 10-frame braille
  spinner animates at 100ms per frame, providing consistent animated feedback
  that communicates active processing.

| State       | Activity Region Content                    | Theme Attribute            |
|-------------|--------------------------------------------|----------------------------|
| Thinking    | `⠋ Thinking...` (spinner animates)         | `Theme.Semantic.Warning`   |
| Streaming   | `⠋ Streaming...` (spinner animates)        | `Theme.Semantic.Info`      |
| Executing   | `⠋ Executing... (2.3s)` (spinner animates) | `Theme.Semantic.Info`      |
| Cancel hint | `Press Esc again to cancel`                | `Theme.Semantic.Warning`   |
| Modal open  | `Esc to dismiss`                           | `Theme.Semantic.Muted`     |

Braille spinner frame sequence (`Theme.Symbols.SpinnerFrames`):
`⠋ ⠙ ⠹ ⠸ ⠼ ⠴ ⠦ ⠧ ⠇ ⠏` (10 frames, U+280B through the set).
Interval: `Theme.Layout.SpinnerIntervalMs` (100ms per frame).
The spinner runs continuously during all active states.

**Separator**: Below the Activity region sits a static row of `Theme.Symbols.Rule`
characters drawn with `Theme.Semantic.Muted`. It never changes content.

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

### 3.5 Animation and Timing Tokens

All animation intervals and timing constants are defined in `Theme.Layout` to
ensure consistent behavior and easy adjustment. No hardcoded timing values
should appear outside `Theme.cs`.

| Token                           | Value  | Theme Constant                     | Usage                                          |
|---------------------------------|--------|------------------------------------|-------------------------------------------------|
| Spinner frame interval          | 100ms  | `Theme.Layout.SpinnerIntervalMs`   | Braille spinner animation rate (section 3.2)   |
| Cursor blink rate               | 500ms  | `Theme.Layout.CursorBlinkMs`       | Input cursor visibility toggle cycle           |
| Cancel hint timeout             | 1000ms | `Theme.Layout.CancelWindowMs`      | Double-Esc cancellation window duration        |

**Spinner**: The braille spinner (section 3.2) cycles through 10 frames at
100ms intervals. This rate is fast enough to convey activity without causing
visual fatigue. In accessible mode, the spinner is replaced with static text.

**Cursor blink**: The input cursor alternates between `Theme.Input.Cursor`
and `Theme.Input.CursorDim` every 500ms. The blink cycle stops when the
input view loses focus (cursor remains dim) or when the application is in
a busy state (cursor hidden).

**Cancel hint**: After the first Esc press during an active turn, the
activity bar shows `Theme.Text.CancelHint` for 1000ms. A second Esc within
this window triggers cancellation. After the window expires, the activity
bar returns to its previous state.

#### Progress Bar Characters

The context usage bar (section 3.4) and any future progress indicators use
these characters:

| Purpose       | Character    | Codepoint | Notes                                     |
|---------------|--------------|-----------|-------------------------------------------|
| Filled/used   | Full block   | U+2588    | Colored per segment (chart palette)       |
| Empty/free    | Light shade  | U+2591    | `Theme.Chart.FreeSpace` color             |

No other fill characters are permitted for progress or bar indicators.

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

| Pattern                           | Blank Lines | Implementation                                              |
|-----------------------------------|-------------|-------------------------------------------------------------|
| Between conversation turns        | 1           | Empty row in content                                        |
| Between text blocks within a turn | 0           | Continuous rendering                                        |
| Before section divider (Rule)     | 1           | `SeparatorBlock` + `SectionBlock` via `ConversationBlockRenderer`; `SpectreHelpers.Section()` in non-TUI fallback |
| After section divider (Rule)      | 0           | Content follows directly                                    |
| After streaming complete          | 1           | Single blank line                                           |
| Between tool preview and result   | 0           | Continuous rendering                                        |
| Between content and modal         | 0           | Modal replaces content                                      |

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

### 5.3 Focus Border

When a Terminal.Gui view gains keyboard focus, its border color changes to
indicate the active input target. Only one view has focus at a time.

| Context          | Border Color     | Theme Constant          |
|------------------|------------------|-------------------------|
| Focused view     | Accent (blue)    | `Theme.Focus.Border`    |
| Unfocused view   | Default / none   | Inherited from parent   |

Focus borders use the accent color because focus is an interactive-element
concept (see section 1.1, accent meaning). This applies to any bordered view
that can receive keyboard focus (e.g., input fields, list views inside modal
dialogs). Views without borders (e.g., the conversation view) do not show a
focus indicator.

### 5.4 Rules (Horizontal Lines)

| Context            | Style                   | Implementation                  |
|--------------------|-------------------------|---------------------------------|
| Section divider    | Dim, left-justified     | Native drawing with dim rule    |
| Layout separator   | Dim, no title           | Dim `─` characters (static)     |
| Banner separator   | Dim, no title           | Dim `─` characters              |

---

## 6. Prompt Styling

### 6.1 Input Prompt

The input view is always visible in its dedicated screen region.
The prompt prefix `> ` is defined as `Theme.Text.PromptPrefix`.

| Context          | Display    | Theme Reference                                       |
|------------------|------------|-------------------------------------------------------|
| Idle (awaiting)  | `> `       | `Theme.Input.Prompt` (`Blue`, `Bold`) + cursor        |
| Active (typing)  | `> {text}` | `Theme.Input.Disabled` (dim) for typeahead during turn|
| Fallback mode    | `> `       | Blocking line input; `Theme.Input.Prompt` attribute   |

During active turns, the input view accepts typeahead input drawn with
`Theme.Input.Disabled`. When messages are queued, a yellow badge
`[N queued]` appears drawn with `Theme.Semantic.Warning`. The input
handler routes display updates to the input view during active turns.

### 6.2 Interactive Prompt Labels

Interactive prompts run via `SpectreHelpers` during Terminal.Gui suspension
(the TUI is suspended before the prompt and resumed after). Prompts follow
this convention:

- Field name highlighted in green: `"Project [green]name[/]:"`
- Optional hint in dim: `"Path [dim](Enter to finish)[/]:"`
- Default value shown: `"Model [dim](gemini-2.5-pro)[/]:"`
- Secret fields: `.Secret()` on TextPrompt
- Validation errors in red: `"[red]Name cannot be empty[/]"`

These prompts render via Spectre.Console to the raw terminal (outside Terminal.Gui
views), so Spectre markup is valid here.

### 6.3 Selection Prompt Highlight

All `SelectionPrompt` and `MultiSelectionPrompt` use a green highlight style.
This is a Spectre.Console concern (prompts run outside Terminal.Gui):
```csharp
.HighlightStyle(new Style(Color.Green))
```

---

## 7. Composite Patterns

### 7.1 Status Message Pattern

All status messages use these exact formats. `SpectreHelpers` static methods
(`Success`, `Error`, `Warning`, `Usage`, `Dim`, `Cancelled`) are the
single point of enforcement. In TUI mode they route through
`ConversationView.AddBlock(new PlainTextBlock(...))` after stripping Spectre
markup; in non-TUI/piped mode they write directly via `AnsiConsole.MarkupLine`.

Visual format (as seen by the user):

```
Success:  "  ✓ {message}"          checkmark: Theme.Symbols.Check (U+2713), green
Error:    "Error: {message}"        prefix: bold red
Warning:  "! Warning: {message}"    prefix: yellow
Usage:    "Usage: {message}"        prefix: yellow
Dim:      "{message}"               muted text
Cancelled:"Cancelled."              muted text
```

Note: Success uses `Theme.Symbols.Check` (U+2713, not lowercase "v"). Error
uses a bold red prefix. Warning includes the `!` symbol.

### 7.2 Error with Suggestion Pattern

Visual format (as seen by the user):

```
Error: {error description}       — bold red "Error:" prefix
  Suggestion: {suggestion text}  — yellow "Suggestion:", muted suggestion text
```

### 7.3 Inline Entity Reference

Entity names referenced in messages are drawn bold:

```
✓ Project {name} created.          — check: green; name: bold
Error: Project {name} not found.   — prefix: bold red; name: bold
```

### 7.4 Tool Result Summary

Visual format (as seen by the user):

```
Success:   "  ✓ Shell  42 lines | 0.3s"   — check: green; label/meta: muted
Error:     "  ✗ Shell  42 lines | 0.3s"   — cross: red;   label/meta: muted
Expand:    "  /expand to show full output" — muted italic
No output: "  ✓ Shell  {truncated result}" — check: green; label/meta: muted
```

In TUI mode these are rendered as `ToolCallConversationBlock` rows by
`ConversationBlockRenderer` using `Theme.Semantic.Success`, `Theme.Semantic.Error`,
and `Theme.Semantic.Muted` attributes via the native drawing API.

### 7.5 Empty State Labels

All empty/unset values use the same pattern -- parenthesized, drawn with
`Theme.Semantic.Muted`:

| Value        | Display text    |
|--------------|-----------------|
| Not set      | `(not set)`     |
| None         | `(none)`        |
| Default      | `(default)`     |
| Ambient      | `(ambient)`     |
| Global       | `(global)`      |
| N/A          | `—` (U+2014)    |

The em dash (U+2014) is used in table cells where a text label would be verbose.
The parenthesized labels are used in prose context. Both are drawn with
`Theme.Semantic.Muted`.

### 7.6 Interactive List Pattern

Used inside modal windows for selectable item lists (e.g., future
`/conversations list` interactive mode, agent selection, profile pickers).
Interactive lists are Terminal.Gui `ListView` or custom scrollable views
inside a `Window` or `Dialog`.

| Element              | Theme Constant                    | Notes                                        |
|----------------------|-----------------------------------|----------------------------------------------|
| Selected row bg      | `Theme.List.SelectedBackground`   | Accent (blue) background for focused row     |
| Selected row text    | `Theme.List.SelectedText`         | White text on selected background            |
| Alternate row tint   | `Theme.List.AlternateRow`         | Optional subtle tint for even/odd rhythm     |
| Action bar text      | `Theme.List.ActionBar`            | Muted hint text at bottom of list window     |

**Selected row**: The currently focused row uses `Theme.List.SelectedBackground`
(accent blue) with `Theme.List.SelectedText` (white). This is visually
consistent with the modal border color (also accent blue) and provides clear
keyboard navigation feedback.

**Alternate row tint**: Optional. When enabled, even-numbered rows use a subtle
background tint (`Theme.List.AlternateRow`) for visual rhythm in long lists.
This is a structural aid, not a semantic signal. Lists with fewer than ~8 items
SHOULD NOT use alternate row tinting -- it adds noise without benefit.

**Action bar**: The bottom row of a list window shows available actions
(e.g., `Enter:Select  Esc:Close  /:Filter`). Drawn with
`Theme.List.ActionBar` (delegates to `Theme.Semantic.Muted`).

**NO_COLOR / accessible mode**: Selected row is indicated by a `>` prefix
marker in column 0. Alternate row tinting is disabled. Action bar text
remains visible as plain text.

```csharp
// Theme.cs
internal static class List
{
  internal static readonly Color SelectedBg = new Color(ColorName16.Blue);
  internal static readonly Attribute SelectedBackground = new(SelectedBg, SelectedBg);
  internal static readonly Attribute SelectedText = new(ColorName16.White, SelectedBg);
  internal static readonly Attribute AlternateRow = new(ColorName16.White, Color.None);
  internal static Attribute ActionBar => Semantic.Muted;
}
```

---

## 8. Responsive Tiers

### 8.1 Width Tiers

Breakpoints are defined in `Theme.Layout`: `FullWidth` (120), `StandardWidth` (80).

| Tier     | Width Range                              | Status Bar                      | Conversation Margin | Key hints text            |
|----------|------------------------------------------|---------------------------------|---------------------|---------------------------|
| Full     | >= `Theme.Layout.FullWidth` (120 col)    | Full pipe-separated             | 2-space indent      | `Theme.Text.HintsWide`    |
| Standard | >= `Theme.Layout.StandardWidth` (80 col) | Abbreviated items               | 2-space indent      | `Theme.Text.HintsMedium`  |
| Compact  | < `Theme.Layout.StandardWidth` (80 col)  | Minimal (provider + model only) | 1-space indent      | `Theme.Text.HintsNarrow`  |

### 8.2 Height Tiers

The view hierarchy has 4 fixed rows (Activity + Input + Separator + StatusBar)
with the Conversation region taking all remaining vertical space.
Height breakpoints are defined in `Theme.Layout`: `FullHeightThreshold` (30),
`CompactHeightThreshold` (15), `MinimalHeightThreshold` (10).

| Tier     | Height Range                                         | Banner           | Conversation Height | Status      |
|----------|------------------------------------------------------|------------------|---------------------|-------------|
| Full     | >= `Theme.Layout.FullHeightThreshold` (30 rows)      | ASCII art        | Height - 4          | Full        |
| Standard | >= `Theme.Layout.CompactHeightThreshold` (15 rows)   | Compact one-line | Height - 4          | Full        |
| Minimal  | >= `Theme.Layout.MinimalHeightThreshold` (10 rows)   | None             | Height - 4          | Abbreviated |
| Fallback | < `Theme.Layout.MinimalHeightThreshold` (10 rows)    | None             | No persistent view hierarchy | Inline |

### 8.3 Detection

Terminal.Gui provides the terminal dimensions via `Application.Driver.Cols` and
`Application.Driver.Rows`. The view hierarchy reacts to `SizeChanging` events.
`BannerRenderer` selects the appropriate banner tier by comparing
`Application.Driver.Cols` against `Theme.Layout.FullWidth` and
`Theme.Layout.StandardWidth`:

```csharp
var width = Application.Driver.Cols;
var heightTier = Application.Driver.Rows >= Theme.Layout.FullHeightThreshold ? "full"
    : Application.Driver.Rows >= Theme.Layout.CompactHeightThreshold ? "standard"
    : Application.Driver.Rows >= Theme.Layout.MinimalHeightThreshold ? "minimal"
    : "fallback";
```

---

## 9. Accessibility Requirements

### 9.1 Accessible Mode

When `BOYDCODE_ACCESSIBLE=1` environment variable is set:

- All animated indicators replaced with static text: `[Working]` not a spinner
- All color removed (equivalent to NO_COLOR)
- All Unicode decorative characters replaced with ASCII equivalents
- All view borders use ASCII characters instead of box-drawing Unicode
- Tables use plain text separators
- Modal overlays announced with `===` delimiters
- Screen reader-friendly: output is sequential, no cursor repositioning

### 9.2 NO_COLOR Behavior

When `NO_COLOR` environment variable is set:

- Terminal.Gui omits all color; all `Attribute` foreground/background components
  are ignored at render time
- `TextStyle.Bold` and `TextStyle.Underline` may still render (terminal-dependent)
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

---

## 10. Code Reference

This section is a lookup table: given a visual element, find the `Theme.*`
constant to use. All constants live in
`src/BoydCode.Presentation.Console/Terminal/Theme.cs`.

### 10.1 Semantic Colors

| Visual meaning                        | Constant                  | Terminal.Gui `Attribute`                               |
|---------------------------------------|---------------------------|--------------------------------------------------------|
| Success / allowed / confirmed         | `Theme.Semantic.Success`  | `new Attribute(ColorName16.Green, Color.None)`         |
| Error / failed / denied               | `Theme.Semantic.Error`    | `new Attribute(ColorName16.BrightRed, Color.None)`     |
| Warning / caution / degraded          | `Theme.Semantic.Warning`  | `new Attribute(ColorName16.Yellow, Color.None)`        |
| Info / data value / path / identifier | `Theme.Semantic.Info`     | `new Attribute(ColorName16.Cyan, Color.None)`          |
| Accent / brand / interactive          | `Theme.Semantic.Accent`   | `new Attribute(ColorName16.Blue, Color.None)`          |
| Muted / dim / secondary / disabled    | `Theme.Semantic.Muted`    | `new Attribute(ColorName16.DarkGray, Color.None)`      |
| Default body text                     | `Theme.Semantic.Default`  | `new Attribute(ColorName16.White, Color.None)`         |

### 10.2 User Message Block

| Element               | Constant               | Notes                                      |
|-----------------------|------------------------|--------------------------------------------|
| Row background color  | `Theme.User.Background`| `new Color(50, 50, 50)` — approved in 1.6  |
| Message text          | `Theme.User.Text`      | White on `Theme.User.Background`           |
| `>` prefix character  | `Theme.User.Prefix`    | DarkGray on `Theme.User.Background`        |

### 10.3 Tool Call Badge

| Element       | Constant              | Notes                          |
|---------------|-----------------------|--------------------------------|
| Box border    | `Theme.ToolBox.Border`| Delegates to `Semantic.Muted`  |

### 10.4 Chat Input

| Element              | Constant                      | Notes                                        |
|----------------------|-------------------------------|----------------------------------------------|
| Prompt `> ` text     | `Theme.Input.Prompt`          | Blue, Bold                                   |
| Prompt prefix string | `Theme.Text.PromptPrefix`     | Literal `"> "`                               |
| Input text           | `Theme.Input.Text`            | White, normal                                |
| Cursor (active)      | `Theme.Input.Cursor`          | White, Underline                             |
| Cursor (inactive)    | `Theme.Input.CursorDim`       | DarkGray, Underline                          |
| Disabled / typeahead | `Theme.Input.Disabled`        | DarkGray                                     |
| Cleared row fill     | `Theme.Input.Clear`           | White (used to blank the row)                |
| Scroll indicator     | `Theme.Input.ScrollIndicator` | DarkGray                                     |

### 10.5 Status Bar

| Element              | Constant                   | Notes                              |
|----------------------|----------------------------|------------------------------------|
| Bar background       | `Theme.StatusBar.Background`| `new Color(30, 30, 30)`           |
| Session info text    | `Theme.StatusBar.Status`   | White on bar background            |
| Key hint text        | `Theme.StatusBar.Hint`     | DarkGray on bar background         |
| Empty fill           | `Theme.StatusBar.Fill`     | Background on background           |

### 10.6 Banner

| Element                      | Constant                         | Notes                          |
|------------------------------|----------------------------------|--------------------------------|
| "BOYD" ASCII art             | `Theme.Banner.BoydArt`           | BrightCyan                     |
| "CODE" ASCII art             | `Theme.Banner.CodeArt`           | BrightBlue                     |
| Info grid label (left col)   | `Theme.Banner.InfoLabel`         | Delegates to `Semantic.Muted`  |
| Info grid value (right col)  | `Theme.Banner.InfoValue`         | Delegates to `Semantic.Info`   |
| Provider/model ready status  | `Theme.Banner.StatusReady`       | Delegates to `Semantic.Success`|
| "Not configured" status      | `Theme.Banner.StatusNotConfigured`| Delegates to `Semantic.Warning`|
| Version string               | `Theme.Banner.Version`           | Delegates to `Semantic.Muted`  |

### 10.7 Modal Windows

| Element               | Constant                 | Notes                                      |
|-----------------------|--------------------------|--------------------------------------------|
| Window border scheme  | `Theme.Modal.BorderScheme`| `new Scheme(new Attribute(Blue, None))`   |

### 10.8 Symbols

| Symbol                  | Constant                       | Char | Codepoint |
|-------------------------|--------------------------------|------|-----------|
| Checkmark               | `Theme.Symbols.Check`          | ✓    | U+2713    |
| Cross                   | `Theme.Symbols.Cross`          | ✗    | U+2717    |
| Horizontal rule char    | `Theme.Symbols.Rule`           | ─    | U+2500    |
| Box top-left corner     | `Theme.Symbols.BoxTopLeft`     | ┌    | U+250C    |
| Box top-right corner    | `Theme.Symbols.BoxTopRight`    | ┐    | U+2510    |
| Box bottom-left corner  | `Theme.Symbols.BoxBottomLeft`  | └    | U+2514    |
| Box bottom-right corner | `Theme.Symbols.BoxBottomRight` | ┘    | U+2518    |
| Box vertical bar        | `Theme.Symbols.BoxVertical`    | │    | U+2502    |
| Left arrow              | `Theme.Symbols.ArrowLeft`      | ←    | U+2190    |
| Right arrow             | `Theme.Symbols.ArrowRight`     | →    | U+2192    |
| Spinner frame array     | `Theme.Symbols.SpinnerFrames`  | ⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏ | 10 frames |

### 10.9 Layout Constants

| Constant                          | Value | Purpose                                     |
|-----------------------------------|-------|---------------------------------------------|
| `Theme.Layout.FullWidth`          | 120   | Width breakpoint: full → standard tier      |
| `Theme.Layout.StandardWidth`      | 80    | Width breakpoint: standard → compact tier   |
| `Theme.Layout.FullHeightThreshold`| 30    | Height breakpoint: full banner              |
| `Theme.Layout.CompactHeightThreshold` | 15 | Height breakpoint: compact one-line banner |
| `Theme.Layout.MinimalHeightThreshold` | 10 | Height breakpoint: no banner               |
| `Theme.Layout.SpinnerIntervalMs`  | 100   | Milliseconds per spinner frame              |
| `Theme.Layout.CursorBlinkMs`      | 500   | Milliseconds per cursor blink cycle         |
| `Theme.Layout.CancelWindowMs`     | 1000  | Double-Esc cancellation window (ms)         |
| `Theme.Layout.MaxInputHistory`    | 100   | Max entries in input history ring buffer    |
| `Theme.Layout.MaxConversationBlocks` | 2000 | Max `ConversationBlock` records in view   |
| `Theme.Layout.CommandPad`         | 24    | Column width for slash command name column  |
| `Theme.Layout.InfoLabelPad`       | 10    | Column width for info grid label column     |

### 10.10 Text Constants

| Constant                        | Value (abbreviated)                          | Usage                             |
|---------------------------------|----------------------------------------------|-----------------------------------|
| `Theme.Text.PromptPrefix`       | `"> "`                                       | Input prompt prefix               |
| `Theme.Text.ThinkingLabel`      | `"Thinking..."`                              | Activity bar — thinking state     |
| `Theme.Text.StreamingLabel`     | `"Streaming..."`                             | Activity bar — streaming state    |
| `Theme.Text.ExecutingLabel`     | `"Executing..."`                             | Activity bar — executing state    |
| `Theme.Text.EscToDismiss`       | `"Esc to dismiss"`                           | Activity bar — modal open state   |
| `Theme.Text.CancelHint`         | `"Press Esc again to cancel"`                | Activity bar — cancel hint state  |
| `Theme.Text.ExpandHint`         | `"/expand to show full output"`              | Tool result overflow hint         |
| `Theme.Text.HintsWide`          | `"Esc:Cancel  PgUp/PgDn:Scroll  ..."`        | Status bar — full width           |
| `Theme.Text.HintsMedium`        | `"Esc:Cancel  PgUp/Dn:Scroll  /quit:Exit"`   | Status bar — standard width       |
| `Theme.Text.HintsNarrow`        | `"/help  /quit"`                             | Status bar — compact width        |
| `Theme.Text.BannerHintWide`     | `"Type a message to start, or /help..."`     | Banner startup hint — full width  |
| `Theme.Text.BannerHintMedium`   | `"Type a message to start, or /help..."`     | Banner startup hint — standard    |
| `Theme.Text.BannerHintNarrow`   | `"Type a message, or /help"`                 | Banner startup hint — compact     |

### 10.11 Chart Colors

| Element                   | Constant                   | Notes                                |
|---------------------------|----------------------------|--------------------------------------|
| System prompt segment     | `Theme.Semantic.Accent`    | Reuses semantic blue                 |
| Tools segment color       | `Theme.Chart.Tools`        | `new Color(147, 112, 219)` — purple  |
| Tools segment attribute   | `Theme.Chart.ToolsAttr`    | Tools color on `Color.None`          |
| Messages segment          | `Theme.Semantic.Success`   | Reuses semantic green                |
| Free space segment color  | `Theme.Chart.FreeSpace`    | `new Color(128, 128, 128)` — grey    |
| Free space attribute      | `Theme.Chart.FreeSpaceAttr`| FreeSpace color on `Color.None`      |
| Buffer segment color      | `Theme.Chart.Buffer`       | `new Color(255, 140, 0)` — orange    |
| Buffer segment attribute  | `Theme.Chart.BufferAttr`   | Buffer color on `Color.None`         |

### 10.12 Interactive List

| Element                   | Constant                        | Notes                                    |
|---------------------------|---------------------------------|------------------------------------------|
| Selected row background   | `Theme.List.SelectedBackground` | Blue bg on blue bg (highlight bar)       |
| Selected row text         | `Theme.List.SelectedText`       | White text on blue bg                    |
| Alternate row tint        | `Theme.List.AlternateRow`       | White on `Color.None` (optional)         |
| Action bar text           | `Theme.List.ActionBar`          | Delegates to `Semantic.Muted`            |

### 10.13 Focus Indicators

| Element                   | Constant              | Notes                                      |
|---------------------------|-----------------------|--------------------------------------------|
| Focused view border       | `Theme.Focus.Border`  | `new Attribute(ColorName16.Blue, Color.None)` — accent blue |

### 10.14 ColorName16 Reference

Complete mapping of the `Terminal.Gui.Drawing.ColorName16` enum values used
throughout this document. These are the ANSI 4-bit colors that adapt to the
user's terminal color scheme.

| ColorName16 Value   | ANSI Code | Typical Appearance   | Semantic Usage in BoydCode               |
|---------------------|-----------|----------------------|------------------------------------------|
| `Black`             | 0         | Black                | (not used directly)                      |
| `Red`               | 1         | Dark red             | (not used; BrightRed preferred for error)|
| `Green`             | 2         | Green                | Success, allowed, confirmed              |
| `Yellow`            | 3         | Yellow / brown       | Warning, caution, attention              |
| `Blue`              | 4         | Blue                 | Accent, brand, interactive, focus        |
| `Magenta`           | 5         | Magenta              | (not used)                               |
| `Cyan`              | 6         | Cyan                 | Info, data values, identifiers           |
| `White`             | 7         | Light gray / white   | Default body text, input text            |
| `BrightBlack`       | 8         | Dark gray            | (alias for `DarkGray`)                   |
| `BrightRed`         | 9         | Bright red           | Error, failed, denied                    |
| `BrightGreen`       | 10        | Bright green         | (not used)                               |
| `BrightYellow`      | 11        | Bright yellow        | (not used)                               |
| `BrightBlue`        | 12        | Bright blue          | Banner "CODE" art                        |
| `BrightMagenta`     | 13        | Bright magenta       | (not used)                               |
| `BrightCyan`        | 14        | Bright cyan          | Banner "BOYD" art                        |
| `BrightWhite`       | 15        | Bright white         | (not used)                               |
| `DarkGray`          | 8         | Dark gray            | Muted, dim, secondary, disabled          |

**Note**: `DarkGray` and `BrightBlack` are the same ANSI code (8). The codebase
uses `DarkGray` consistently. Terminal appearance of all values depends on the
user's terminal color scheme -- these names describe typical defaults, not
guaranteed colors.
