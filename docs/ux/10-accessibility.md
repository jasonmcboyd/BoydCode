# Accessibility

This document audits the current accessibility state of the BoydCode terminal
UI across screen reader compatibility, color dependency, non-interactive mode
support, narrow terminal behavior, and platform-specific concerns.

---

## 1. Current State Audit

### 1.1 Screen Reader Compatibility

Screen readers for terminal applications (NVDA with terminal support, JAWS,
VoiceOver, Orca) work by reading the text content of the terminal buffer.
Features that modify in-place content or use ANSI escape sequences for cursor
positioning create problems.

#### Features That Work With Screen Readers

| Feature | Why It Works |
|---|---|
| Static text output (assistant responses, error messages) | Written as plain text lines that enter the terminal buffer sequentially |
| Status messages (`Success`, `Error`, `Warning`, `Usage`) | Single-line output with text content (markup tags stripped by screen readers that process plain text) |
| Help table | Rendered as text-mode table with ASCII borders |
| Data tables (project list, session list, JEA profiles) | Same as help table; readable as structured text |
| Banner text | ASCII art lines are read sequentially; content is decorative but harmless |
| Confirmation prompts | Text-based input with clear labels |
| Panel content (tool preview, crash panel) | Rendered as bordered text; borders are readable characters |

#### Features That Break With Screen Readers

| Feature | Problem | Severity |
|---|---|---|
| **Execution spinner** | Overwrites the same line repeatedly with cursor movement; creates rapid-fire screen reader announcements of "Executing..." | High |
| **Split-pane layout** | Cursor positioning to fixed rows creates disorienting jumps for screen readers that expect sequential text output | High |
| **Streaming token output** | Tokens arrive character-by-character; screen readers may announce each fragment | Medium |
| **Cancel hint (in-place overwrite)** | Hint is written transiently and cleared; screen reader may not detect the transient text | Medium |
| **Thinking indicator** | Overwrites a line in place; screen reader may miss or repeatedly announce it | Medium |
| **Output window scrolling** | In-place rewriting of visible lines; screen reader sees rapid content changes | Medium |
| **Selection prompts** | Interactive selection with cursor positioning; most screen readers can navigate these, but custom key bindings (j/k) are not announced | Low |
| **Context chart (stacked bar)** | Uses Unicode block characters that screen readers may announce as "full block" repeated 72 times | Low |
| **Queue count indicator** | Written with positioning to the end of the input line; may not be announced | Low |

### 1.2 NO_COLOR Support

The application does **not** check for the `NO_COLOR` environment variable
(`https://no-color.org/`). There is no code anywhere in the codebase that
reads `NO_COLOR`.

However, the rendering framework respects terminal capabilities:
- When stdout is redirected (piped), the rendering pipeline detects this and
  strips ANSI escape sequences from markup output.
- Error output independently detects stderr capabilities.

**Raw ANSI sequences are not stripped when piped.** Some components write raw
ANSI escape sequences directly, bypassing the rendering pipeline. These
sequences would appear as garbage text if stdout were redirected.

### 1.3 Piped/Redirected Output Degradation

| Feature | Behavior When Piped |
|---|---|
| Layout mode | Disabled (checks whether output and input are redirected) |
| Markup output | Markup tags stripped; plain text only |
| Raw ANSI in non-layout mode | Sequences pass through as literal text (e.g., spinner carriage-return rewrite) |
| Spinner output | Each frame becomes a new line when piped |
| Tables | Rendered as plain text tables (no color, but borders preserved) |
| Panels | Rendered as plain text boxes |

---

## 2. Non-Interactive Mode

### 2.1 Detection

Non-interactive mode is detected when stdin is not a terminal (e.g., piped
input, CI environment).

### 2.2 Prompt Fallbacks

| Prompt | Non-Interactive Behavior |
|---|---|
| User input | Falls back to reading from stdin; EOF becomes `/quit` |
| `/project create` | Requires `<name>` argument; shows usage message if missing |
| `/project show` | Requires `<name>` argument or active project; shows usage if both missing |
| `/project edit` | Blocked entirely: `"/project edit requires an interactive terminal."` |
| `/project delete` | Requires `<name>` argument; skips confirmation (proceeds to delete) |
| `/provider setup` | Requires `<name>` argument; blocked: `"/provider setup requires an interactive terminal."` |
| `/provider remove` | Requires `<name>` argument; shows usage if missing |
| `/conversations delete` | Requires `<id>` argument; skips confirmation |
| `boydcode login` | Blocked entirely: `"Login requires an interactive terminal."` |

### 2.3 Features Skipped

| Feature | Reason |
|---|---|
| Layout activation | Returns early when not interactive |
| Async input reader | Never started; fallback prompt used |
| Status line display | Rendered as one-time text output, not a fixed-position status bar |
| Queue count indicator | Not applicable (no async input) |
| Contained output window | Requires interactive terminal with ANSI support |

### 2.4 Behavioral Differences

- **Confirmation prompts are skipped** in non-interactive mode for destructive
  operations (`/project delete`, `/conversations delete`). The operation proceeds
  without asking. This is by design for CI/scripting but could be dangerous.
- **Multi-step wizards do not run.** After `/project create`, the "Configure
  project settings now?" prompt is skipped; the project is created with
  defaults only.

---

## 3. Narrow Terminal Behavior

### 3.1 Terminal Width Thresholds

The application has a single explicit width adaptation: the output window
truncates lines to `termWidth - 6` characters. Most other rendering relies
on Spectre.Console's built-in wrapping and the terminal's native line
wrapping.

### 3.2 Behavior by Width

#### 40 Columns

| Component | Behavior | Impact |
|---|---|---|
| ASCII art banner | Lines wrap; BOYD/CODE letters are corrupted | Visual corruption; still functional |
| Info grid | Columns overlap; labels and values may merge | Hard to read |
| Data tables | Columns compressed; content truncated by Spectre | Functional but cramped |
| Tool preview panels | Panel borders consume 4+ columns; content severely truncated | Minimal useful content visible |
| Status line | Truncated to terminal width | Functional |
| Input line | Text truncated to `width - 4` characters | Usable for short inputs |
| Stacked bar chart | Fixed 72-character width; extends beyond terminal, wraps | Broken layout |
| Selection prompts | Spectre handles wrapping; usable | Functional |
| Error messages | Wrap naturally | Functional |

#### 60 Columns

| Component | Behavior | Impact |
|---|---|---|
| ASCII art banner | Still wraps (banner is ~68+ chars wide per line) | Visual corruption |
| Info grid | Tight but readable for short values | Mostly functional |
| Data tables | Functional with some truncation | Usable |
| Stacked bar chart | Extends beyond terminal; wraps | Broken layout |
| Everything else | Functional | OK |

#### 80 Columns (Target Minimum)

| Component | Behavior | Impact |
|---|---|---|
| ASCII art banner | Fits (banner lines are ~68-72 chars with indent) | Functional |
| Info grid | Comfortable layout | Good |
| Data tables | Full content visible for typical data | Good |
| Stacked bar chart | Fits (72 chars + 2 indent = 74 total) | Functional |
| Tool preview panels | Adequate content visible | Good |
| All other components | Normal operation | Good |

#### 120 Columns (Comfortable)

| Component | Behavior | Impact |
|---|---|---|
| All components | Ample space for all content | Excellent |
| ASCII art banner + sidebar metadata | Both visible on same lines | Full experience |
| Data tables | Room for long paths and descriptions | Excellent |

#### 200+ Columns (Wide)

| Component | Behavior | Impact |
|---|---|---|
| All components | No degradation | Excellent |
| Separator line | Extends to full width (`new string('\u2500', width)`) | Correct |
| Tables | Spectre auto-expands to fill; no max-width constraint | Very wide tables may reduce readability |
| Stacked bar chart | Fixed at 72 characters; does not expand | Could use more width |

### 3.3 Compact Banner

The banner switches to a compact single-line format when terminal height is
less than 30 rows:

- Full: Multi-line ASCII art (13+ lines)
- Compact: `BOYDCODE v0.1 AI Coding Assistant` (1 line)

### 3.4 Layout Minimum Height

The application layout requires a minimum of 10 rows. Below this threshold,
the application falls back to inline output mode.

Layout space allocation:
- Conversation view: rows 1 through `height - 3`
- Separator: row `height - 2`
- Input area: row `height - 1`
- Status bar: row `height`

At exactly 10 rows, the conversation view has 7 rows of scrollable space.

---

## 4. Color Dependency

### 4.1 Places Where Color Is the ONLY Indicator

| Location | What Color Conveys | Missing Non-Color Indicator |
|---|---|---|
| Provider status in list | Green bold "active" vs dim "ready" vs empty | The text "active" and "ready" provides meaning, but the empty state for unconfigured has no text at all |
| Directory access level in show | Yellow "ReadOnly" vs green "ReadWrite" | The text itself provides meaning; color is redundant (accessible) |
| Directory access level in edit | Yellow "ReadOnly" vs green "ReadWrite" | Same as above (accessible) |
| Git info column | Red "missing" vs cyan branch name vs dim markers | Text provides meaning (accessible) |
| Context usage threshold | Green / yellow / red on token count | The percentage number is also shown, so color is supplementary (accessible) |
| Stacked bar chart segments | Different colors for different categories | The legend provides text labels alongside color squares (partially accessible) |
| Selection prompt highlight | Green for current selection | Also uses a `>` cursor indicator (accessible) |
| Cancel hint | Dim italic yellow text | Text content is self-explanatory (accessible) |

**Assessment:** No critical functionality depends solely on color. All
colored indicators either have text alternatives or the color reinforces
meaning already conveyed by text. The application is broadly color-accessible.

### 4.2 Semantic Color Usage

The application uses color semantically and consistently:

| Color | Meaning | Used For |
|---|---|---|
| Green | Success, positive, allowed | Success checkmarks, allowed commands, ReadWrite, active status |
| Red | Error, failure, denied | Error messages, denied commands, missing directories |
| Yellow | Warning, caution, read-only | Warnings, ReadOnly access, cancel hints |
| Cyan | Data values, identifiers | Model names, paths, branch names, provider names |
| Blue | Input, brand, structural | Input prompt, banner, help table border |
| Dim | Metadata, tertiary info | Labels, hints, timestamps, empty states |

This is a well-structured semantic color system that does not use color for
decorative purposes.

---

## 5. Interactive Component Accessibility

### 5.1 ListView Navigation (Interactive List Windows, Pattern #28)

When an Interactive List window is open, screen readers must announce
navigation and selection state clearly.

| Event | Screen Reader Announcement |
|---|---|
| Row focus changes (Up/Down) | Announce all visible column values for the selected row, plus position: "my-project, 3 directories, InProcess -- item 2 of 5" |
| Enter pressed (primary action) | Announce the action taken: "Opening my-project" |
| Single-letter hotkey pressed | Announce the action: "Editing my-project" or "Deleting my-project" |
| Window opens | Announce the window title and item count: "Projects -- 5 items" |
| Window closes (Esc) | Announce dismissal: "Projects window closed" |
| Empty list | Announce the empty state message: "No projects configured" |
| Scroll changes (Page Up/Down) | Announce new position: "Showing items 6 through 10 of 42" |

**Implementation notes:**

- Each row should be constructed as a single readable string with field
  values separated by commas, avoiding decorative characters that screen
  readers would announce individually (em dashes, arrows).
- The `ListView` item count is announced when the window opens. Position
  within the list ("item 3 of 12") is announced on each selection change.
- The Action Bar (pattern #29) is announced once when the window opens:
  "Actions: Enter equals Open, e equals Edit, d equals Delete, n equals
  New, Esc equals Close."
- In accessible mode (`BOYDCODE_ACCESSIBLE=1`), the Interactive List
  renders as a numbered text list (see pattern #28 accessibility section).

### 5.2 Dialog Accessibility (Form Dialog, Pattern #31)

Dialogs must follow standard form accessibility conventions for screen
readers.

| Requirement | Detail |
|---|---|
| **Field labels** | Each `TextField` and `TextView` has a `Label` associated by Tab order. Screen readers announce the label when the field gains focus: "Name, text field" |
| **Button labels** | Buttons are announced with their text: "Create button", "Cancel button". The default button (Enter) is identified: "Create button, default" |
| **Focus order** | Tab order matches visual top-to-bottom, left-to-right layout. Screen readers follow the same traversal |
| **Validation errors** | When a validation error appears below a field, it is announced immediately: "Error: Name cannot be empty." Focus remains on the invalid field |
| **Dialog title** | Announced when the dialog opens: "Create Project dialog" |
| **Optional fields** | The "(optional)" hint after a label is read as part of the label: "Docker image, optional, text field" |
| **Secret fields** | `TextField` with `Secret = true` is announced as "API key, password field" -- typed characters are not spoken |

### 5.3 Multi-Step Wizard Accessibility (Pattern #32)

Wizards extend dialog accessibility with step-level announcements.

| Event | Screen Reader Announcement |
|---|---|
| Wizard opens | "Provider Setup dialog, Step 1 of 3, Choose Provider" |
| Step transition (Next) | "Step 2 of 3, Authentication" -- focus moves to the first field in the new step |
| Step transition (Back) | "Step 1 of 3, Choose Provider" -- focus returns to the first field, preserving previous values |
| Final step (Done) | "Step 3 of 3, Confirm" -- announces the summary content sequentially |
| Cancel from any step | "Provider Setup canceled" |
| Alt+B / Alt+N | Screen reader announces "Back button pressed" or "Next button pressed" before the step transition announcement |

### 5.4 Interactive List Accessibility Summary

The following table summarizes the screen reader contract for all
interactive components:

| Component | Opens With | Navigated By | Position Announcement | Actions Announced |
|---|---|---|---|---|
| Interactive List (#28) | Title + item count | Up/Down, Home/End, Page | "item N of M" | Action Bar read once on open |
| Form Dialog (#31) | Title + "dialog" | Tab/Shift+Tab | Field label on focus | Button labels on focus |
| Multi-Step Wizard (#32) | Title + step indicator | Tab within step, Alt+B/N between steps | Step N of M + step title | Next/Back/Cancel/Done |
| Search/Filter (#30) | "Filter, text field" | Type to filter, Esc to clear | "Showing N of M items" | -- |

---

## 6. Recommendations

### 6.1 High Priority

| # | Recommendation | Effort | Impact |
|---|---|---|---|
| 1 | **Add `NO_COLOR` environment variable support.** Check for `NO_COLOR` at startup; when set, disable all ANSI color output and raw ANSI escape sequences. This is a community standard (`https://no-color.org/`) that many accessibility tools depend on. | Medium | High |
| 2 | **Route all errors to stderr.** Ensure all error output goes through the user interface error rendering pipeline, which should write to stderr. This ensures errors are visible when stdout is piped and follows Unix conventions. | Medium | High |
| 3 | **Disable spinner in non-ANSI terminals.** When ANSI capabilities are not available, skip the spinner entirely and show a single "Executing..." line. In-place overwrites produce garbage when ANSI is not supported. | Low | High |

### 6.2 Medium Priority

| # | Recommendation | Effort | Impact |
|---|---|---|---|
| 4 | **Add `--no-layout` flag.** Allow users to explicitly disable the split-pane layout. This is useful for screen reader users who need simple sequential output. The flag would force fallback to inline output mode. | Low | Medium |
| 5 | **Reduce spinner frame rate for screen readers.** When a screen reader is detected (or when an env var like `BOYDCODE_ACCESSIBLE` is set), reduce the spinner to a 2-second update interval with a simple text message ("Still executing... 5.0s"). | Low | Medium |
| 6 | **Make stacked bar chart width dynamic.** Currently hardcoded to 72 characters. Should adapt to `Math.Min(termWidth - 8, 72)` to prevent wrapping on terminals narrower than 80 columns. | Low | Medium |
| 7 | **Add `--plain` output mode.** When set, strip all Spectre markup and render plain text only. Useful for piping, logging, and accessibility tools. This would affect all output paths. | High | Medium |
| 8 | **Add text-only fallback for context chart.** When color is unavailable, render the stacked bar as labeled segments: `[System: 15%][Tools: 5%][Messages: 30%][Free: 50%]`. | Low | Medium |

### 6.3 Low Priority

| # | Recommendation | Effort | Impact |
|---|---|---|---|
| 9 | **Replace lowercase `v` with Unicode checkmark.** The success indicator `v` (U+0076) should be `\u2713` (check mark) for clarity. All major modern terminals support it. | Low | Low |
| 10 | **Add ARIA-like labels for dynamic content.** When the spinner starts, write a screen-reader-friendly line like "Tool execution started" before the spinning begins. When it ends, write "Tool execution completed." These lines would be `[dim]` or hidden from sighted users. | Medium | Low |
| 11 | **Document keyboard shortcuts in `/help`.** The vim-style j/k remapping and Esc/Ctrl+C cancellation are not documented in the help output. Add a "Keyboard shortcuts" section or a `/keys` command. | Low | Low |
| 12 | **Test with Windows Narrator.** Verify that the basic chat flow (type message, see response) works acceptably with Windows Narrator in Windows Terminal. | Low (testing) | Low |

---

## 7. Platform-Specific Concerns

### 7.1 Windows Terminal

| Aspect | Status | Notes |
|---|---|---|
| ANSI/VT sequences | Full support | Windows Terminal enables VT processing by default |
| Unicode (Braille spinners, box drawing) | Full support | All characters render correctly |
| Scroll regions | Full support | `\x1b[1;Nr` works correctly |
| Color | Full 256-color and truecolor | All Spectre colors render correctly |
| Resize detection | Works via `Console.WindowHeight/Width` polling | No `SIGWINCH` equivalent; polled on each write |
| Scrollback on resize | Preserved | Content above the scroll region survives resize |
| `Console.CancelKeyPress` | Works | Ctrl+C correctly intercepted |
| Vim key remapping | Works | `Console.ReadKey(intercept: true)` returns correct key info |

### 7.2 Legacy Windows Console Host (conhost.exe)

| Aspect | Status | Notes |
|---|---|---|
| ANSI/VT sequences | Partial support | .NET runtime enables VT mode when possible, but some older builds may not support it. The application attempts a harmless VT probe at startup. |
| Unicode (Braille spinners) | Partial support | Depends on the console font. Consolas and Cascadia support Braille. Raster fonts do not. |
| Scroll regions | May not work | `\x1b[1;Nr` may be ignored, causing layout corruption |
| Color | 16 colors only | Spectre.Console automatically degrades to nearest 4-bit color |
| Scrollback on resize | May lose content | conhost has a fixed-size buffer |
| Recommendation | Use Windows Terminal | Legacy conhost is not a supported target |

### 7.3 macOS Terminal.app

| Aspect | Status | Notes |
|---|---|---|
| ANSI/VT sequences | Full support | Terminal.app has full VT100/VT220 support |
| Unicode | Full support | macOS ships with comprehensive Unicode fonts |
| Scroll regions | Full support | Standard VT feature |
| Color | 256 colors | No truecolor in older versions; Spectre degrades gracefully |
| Resize detection | Works via `Console.WindowHeight/Width` | .NET reads `TIOCGWINSZ` ioctl |
| `Console.CancelKeyPress` | Works | Standard .NET behavior on macOS |

### 7.4 iTerm2 (macOS)

| Aspect | Status | Notes |
|---|---|---|
| All features | Full support | iTerm2 has excellent VT and Unicode support |
| Truecolor | Full support | Better than Terminal.app |
| Scrollback | Unlimited (configurable) | No content loss on resize |
| Accessibility | iTerm2 has its own screen reader integration | Better than Terminal.app for VoiceOver |

### 7.5 Linux Terminal Emulators

#### GNOME Terminal / Tilix / Terminator

| Aspect | Status | Notes |
|---|---|---|
| All features | Full support | VTE-based terminals have excellent VT support |
| Unicode | Full support | Depends on installed fonts; most distros ship with Noto or DejaVu |
| Color | Truecolor | Fully supported |

#### xterm

| Aspect | Status | Notes |
|---|---|---|
| ANSI/VT sequences | Full support | xterm is the reference VT implementation |
| Unicode | Requires `uxterm` or proper font config | Plain `xterm` may not render Braille characters |
| Color | 256 colors (xterm-256color) | Must use correct TERM value |

#### Linux Console (tty1-tty6)

| Aspect | Status | Notes |
|---|---|---|
| ANSI/VT sequences | Basic support | No scroll region support |
| Unicode | Very limited | Only ASCII and Latin-1 without `fbterm` |
| Color | 8/16 colors | Spectre degrades to 4-bit |
| Scroll regions | Not supported | Layout mode would fail; fallback to inline |
| Recommendation | Not a supported target | Use a graphical terminal emulator |

### 7.6 SSH Sessions

| Aspect | Status | Notes |
|---|---|---|
| Terminal capabilities | Depends on client terminal | The SSH client's terminal determines capabilities |
| TERM variable | Must be set correctly | `xterm-256color` is recommended |
| Resize | `SIGWINCH` forwarded by SSH | .NET detects resize via `Console.WindowHeight/Width` |
| Latency | May affect spinner appearance | High-latency connections may see jerky spinner updates |
| `Console.CancelKeyPress` | Works | SSH forwards Ctrl+C correctly |

### 7.7 Platform Summary

| Platform | Layout Mode | Spinner | Unicode | Color | Overall |
|---|---|---|---|---|---|
| Windows Terminal | Full | Full | Full | Full | Fully supported |
| Legacy conhost | Degraded | Degraded | Partial | 16 colors | Not recommended |
| macOS Terminal.app | Full | Full | Full | 256 colors | Fully supported |
| iTerm2 | Full | Full | Full | Truecolor | Fully supported |
| GNOME Terminal | Full | Full | Full | Truecolor | Fully supported |
| xterm | Full | Full | Partial | 256 colors | Mostly supported |
| Linux console (tty) | Disabled | Degraded | Minimal | 16 colors | Not supported |
| SSH (modern client) | Full | Full | Full | Depends | Fully supported |
