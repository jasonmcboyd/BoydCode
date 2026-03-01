# Accessibility

This document audits the current accessibility state of the BoydCode terminal
UI across screen reader compatibility, color dependency, non-interactive mode
support, narrow terminal behavior, and platform-specific concerns.

All file references are relative to the repository root.

---

## 1. Current State Audit

### 1.1 Screen Reader Compatibility

Screen readers for terminal applications (NVDA with terminal support, JAWS,
VoiceOver, Orca) work by reading the text content of the terminal buffer.
Features that modify in-place content or use ANSI escape sequences for cursor
positioning create problems.

#### Features That Work With Screen Readers

| Feature | Why It Works | Files |
|---|---|---|
| Static text output (assistant responses, error messages) | Written as plain text lines that enter the terminal buffer sequentially | `SpectreUserInterface.cs` |
| Status messages (`Success`, `Error`, `Warning`, `Usage`) | Single-line output with text content (markup tags stripped by screen readers that process plain text) | `SpectreHelpers.cs` |
| Help table | Spectre.Console renders as text-mode table with ASCII borders | `HelpSlashCommand.cs` |
| Data tables (project list, session list, JEA profiles) | Same as help table; readable as structured text | All slash commands |
| Banner text | ASCII art lines are read sequentially; content is decorative but harmless | `ChatCommand.cs` |
| Confirmation prompts | Text-based input with clear labels | Via `SpectreHelpers.Confirm` |
| Panel content (tool preview, crash panel) | Rendered as bordered text; borders are readable characters | `SpectreUserInterface.cs`, `Program.cs` |

#### Features That Break With Screen Readers

| Feature | Problem | Severity | Files |
|---|---|---|---|
| **Execution spinner** | Overwrites the same line 10 times/second with ANSI cursor movement; creates rapid-fire screen reader announcements of "Executing..." | High | `ExecutionWindow.cs:311-345` |
| **Split-pane layout (scroll regions)** | ANSI scroll region commands (`\x1b[1;Nr`) are not interpreted by screen readers; cursor positioning to fixed rows creates disorienting jumps | High | `TerminalLayout.cs` |
| **Streaming token output** | Tokens arrive character-by-character via `AppendToOutput`; screen readers may announce each fragment | Medium | `SpectreUserInterface.cs:215-234` |
| **Cancel hint (in-place overwrite)** | Non-layout mode: hint is written with `\r` and cleared with spaces; screen reader may not detect the transient text | Medium | `SpectreUserInterface.cs:455-492` |
| **Thinking indicator** | Layout mode: overwrites a line with `\x1b[2K`; Non-layout: `\r` overwrite | Medium | `SpectreUserInterface.cs:253-280` |
| **Output window scrolling (non-layout)** | Uses `\x1b[NA` (cursor up) to rewrite visible lines; screen reader sees rapid content changes | Medium | `ExecutionWindow.cs:427-463` |
| **Selection prompts** | Spectre.Console renders interactive selection with cursor positioning; most screen readers can navigate these, but vim key remapping (j/k) is not announced | Low | `SpectreHelpers.cs` |
| **Context chart (stacked bar)** | Uses Unicode block characters that screen readers may announce as "full block" repeated 72 times | Low | `ContextSlashCommand.cs:216-283` |
| **Queue count indicator** | Written with ANSI positioning to the end of the input line; may not be announced | Low | `TerminalLayout.cs:328-337` |

### 1.2 NO_COLOR Support

The application does **not** check for the `NO_COLOR` environment variable
(`https://no-color.org/`). There is no code anywhere in the codebase that
reads `NO_COLOR`.

However, Spectre.Console itself respects terminal capabilities:
- When stdout is redirected (piped), Spectre.Console's `AnsiConsole` detects
  this and strips ANSI escape sequences from markup output.
- The `_stderr` `IAnsiConsole` instance in `SpectreUserInterface` is
  configured with `new AnsiConsoleOutput(Console.Error)` and independently
  detects stderr capabilities.

**Raw ANSI sequences are not stripped when piped.** The `TerminalLayout` and
`ExecutionWindow` classes write raw `\x1b[...]` sequences via
`System.Console.Write()`, bypassing Spectre.Console entirely. These sequences
would appear as garbage text if stdout were redirected.

### 1.3 Piped/Redirected Output Degradation

| Feature | Behavior When Piped | Files |
|---|---|---|
| Layout mode | Disabled (`CanUseLayout` checks `IsOutputRedirected` and `IsInputRedirected`) | `TerminalLayout.cs:386-401` |
| Spectre markup output | Markup tags stripped; plain text only | Spectre.Console built-in |
| Raw ANSI in non-layout mode | Sequences pass through as literal text (e.g., spinner `\r` rewrite) | `ExecutionWindow.cs`, `SpectreUserInterface.cs` |
| Spinner output | Writes `\r  {frame} Executing... ({elapsed})` to stdout; each frame is a new line when piped | `ExecutionWindow.cs:333` |
| Tables | Rendered as plain text tables (no color, but borders preserved) | Spectre.Console built-in |
| Panels | Rendered as plain text boxes | Spectre.Console built-in |

---

## 2. Non-Interactive Mode

### 2.1 Detection

Non-interactive mode is detected via
`AnsiConsole.Profile.Capabilities.Interactive`, which returns `false` when
stdin is not a terminal (e.g., piped input, CI environment).

### 2.2 Prompt Fallbacks

| Prompt | Non-Interactive Behavior | Implementation |
|---|---|---|
| `GetUserInputAsync` | Falls back to `Console.ReadLine()`; EOF becomes `/quit` | `SpectreUserInterface.cs:35-38` |
| `/project create` | Requires `<name>` argument; shows usage message if missing | `ProjectSlashCommand.cs:95-98` |
| `/project show` | Requires `<name>` argument or active project; shows usage if both missing | `ProjectSlashCommand.cs:217-220` |
| `/project edit` | Blocked entirely: `"/project edit requires an interactive terminal."` | `ProjectSlashCommand.cs:370-374` |
| `/project delete` | Requires `<name>` argument; skips confirmation (proceeds to delete) | `ProjectSlashCommand.cs:477-479,539` |
| `/provider setup` | Requires `<name>` argument; blocked: `"/provider setup requires an interactive terminal."` | `ProviderSlashCommand.cs:117-121,141-144` |
| `/provider remove` | Requires `<name>` argument; shows usage if missing | `ProviderSlashCommand.cs:201-205` |
| `/sessions delete` | Requires `<id>` argument; skips confirmation | `SessionsSlashCommand.cs:180-184,201-213` |
| `LoginCommand` | Blocked entirely: `"Login requires an interactive terminal."` | `LoginCommand.cs:33-37` |

### 2.3 Features Skipped

| Feature | Reason | Implementation |
|---|---|---|
| Layout activation | `ActivateLayout` returns early when `!IsInteractive` | `SpectreUserInterface.cs:496` |
| Async input reader | Never started; fallback prompt used | `SpectreUserInterface.cs:41-45` |
| Status line display | Rendered via fallback `AnsiConsole.MarkupLine` (one-time, not fixed position) | `SpectreUserInterface.cs:49-51` |
| Queue count indicator | Not applicable (no async input) | `TerminalLayout.cs:307` |
| Contained output window | `useContainedOutput` requires `IsInteractive && Capabilities.Ansi` | `SpectreUserInterface.cs:305` |

### 2.4 Behavioral Differences

- **Confirmation prompts are skipped** in non-interactive mode for destructive
  operations (`/project delete`, `/sessions delete`). The operation proceeds
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

Source: `ChatCommand.cs:270-306`

### 3.4 Layout Minimum Height

The split-pane layout requires a minimum of 10 rows (`MinTerminalHeight`).
Below this threshold, `CanUseLayout()` returns false and the application
falls back to inline output mode.

Layout space allocation:
- Output scroll region: rows 1 through `height - 3`
- Separator: row `height - 2`
- Input line: row `height - 1`
- Status line: row `height`

At exactly 10 rows, the output area has 7 rows of scrollable space.

---

## 4. Color Dependency

### 4.1 Places Where Color Is the ONLY Indicator

| Location | What Color Conveys | Missing Non-Color Indicator | Files |
|---|---|---|---|
| Provider status in list | `[green bold]active[/]` vs `[dim]ready[/]` vs empty | The text "active" and "ready" provides meaning, but the empty state for unconfigured has no text at all | `ProviderSlashCommand.cs:92-96` |
| Directory access level in show | `[yellow]ReadOnly[/]` vs `[green]ReadWrite[/]` | The text itself provides meaning; color is redundant (accessible) | `ProjectSlashCommand.cs:294-296` |
| Directory access level in edit | `[yellow]ReadOnly[/]` vs `[green]ReadWrite[/]` | Same as above (accessible) | `ProjectSlashCommand.cs:620-622` |
| Git info column | `[red]missing[/]` vs `[cyan]{branch}[/]` vs `[dim]git[/]` vs `[dim]--[/]` | Text provides meaning (accessible) | `ProjectSlashCommand.cs:298-304` |
| Context usage threshold | `[green]` / `[yellow]` / `[red]` on token count | The percentage number is also shown, so color is supplementary (accessible) | `ContextSlashCommand.cs:131-136` |
| Stacked bar chart segments | Different colors for different categories | The legend provides text labels alongside color squares (partially accessible) | `ContextSlashCommand.cs:216-283` |
| Selection prompt highlight | `Color.Green` for current selection | Spectre.Console also uses a `>` cursor indicator (accessible) | `SpectreHelpers.cs:231,249,268` |
| Cancel hint | `[dim italic yellow]` | Text content is self-explanatory (accessible) | `SpectreUserInterface.cs:465,469` |

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

## 5. Recommendations

### 5.1 High Priority

| # | Recommendation | Effort | Impact |
|---|---|---|---|
| 1 | **Add `NO_COLOR` environment variable support.** Check for `NO_COLOR` at startup; when set, configure `AnsiConsole.Profile` to disable colors and disable all raw ANSI escape sequences. This is a community standard (`https://no-color.org/`) that many accessibility tools depend on. | Medium | High |
| 2 | **Route all errors to stderr.** Replace `SpectreHelpers.Error()` stdout writes and raw `AnsiConsole.MarkupLine("[red]...")` calls with `_ui.RenderError()` or a new stderr-aware helper. This ensures errors are visible when stdout is piped and follows Unix conventions. | Medium | High |
| 3 | **Disable spinner in non-ANSI terminals.** When `AnsiConsole.Profile.Capabilities.Ansi` is false, skip the spinner entirely and show a single "Executing..." line. The current `\r`-based overwrite produces garbage when ANSI is not supported. | Low | High |

### 5.2 Medium Priority

| # | Recommendation | Effort | Impact |
|---|---|---|---|
| 4 | **Add `--no-layout` flag.** Allow users to explicitly disable the split-pane layout. This is useful for screen reader users who need simple sequential output. The flag would set `_useLayout = false` regardless of `CanUseLayout()`. | Low | Medium |
| 5 | **Reduce spinner frame rate for screen readers.** When a screen reader is detected (or when an env var like `BOYDCODE_ACCESSIBLE` is set), reduce the spinner to a 2-second update interval with a simple text message ("Still executing... 5.0s"). | Low | Medium |
| 6 | **Make stacked bar chart width dynamic.** Currently hardcoded to 72 characters. Should adapt to `Math.Min(termWidth - 8, 72)` to prevent wrapping on terminals narrower than 80 columns. | Low | Medium |
| 7 | **Add `--plain` output mode.** When set, strip all Spectre markup and render plain text only. Useful for piping, logging, and accessibility tools. This would affect all output paths. | High | Medium |
| 8 | **Add text-only fallback for context chart.** When color is unavailable, render the stacked bar as labeled segments: `[System: 15%][Tools: 5%][Messages: 30%][Free: 50%]`. | Low | Medium |

### 5.3 Low Priority

| # | Recommendation | Effort | Impact |
|---|---|---|---|
| 9 | **Replace lowercase `v` with Unicode checkmark.** The success indicator `v` (U+0076) should be `\u2713` (check mark) for clarity. All major modern terminals support it. | Low | Low |
| 10 | **Add ARIA-like labels for dynamic content.** When the spinner starts, write a screen-reader-friendly line like "Tool execution started" before the spinning begins. When it ends, write "Tool execution completed." These lines would be `[dim]` or hidden from sighted users. | Medium | Low |
| 11 | **Document keyboard shortcuts in `/help`.** The vim-style j/k remapping and Esc/Ctrl+C cancellation are not documented in the help output. Add a "Keyboard shortcuts" section or a `/keys` command. | Low | Low |
| 12 | **Test with Windows Narrator.** Verify that the basic chat flow (type message, see response) works acceptably with Windows Narrator in Windows Terminal. | Low (testing) | Low |

---

## 6. Platform-Specific Concerns

### 6.1 Windows Terminal

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

### 6.2 Legacy Windows Console Host (conhost.exe)

| Aspect | Status | Notes |
|---|---|---|
| ANSI/VT sequences | Partial support | .NET runtime enables VT mode when possible, but some older builds may not support it. `EnableVirtualTerminalProcessing()` in `TerminalLayout.cs` attempts a harmless VT probe. |
| Unicode (Braille spinners) | Partial support | Depends on the console font. Consolas and Cascadia support Braille. Raster fonts do not. |
| Scroll regions | May not work | `\x1b[1;Nr` may be ignored, causing layout corruption |
| Color | 16 colors only | Spectre.Console automatically degrades to nearest 4-bit color |
| Scrollback on resize | May lose content | conhost has a fixed-size buffer |
| Recommendation | Use Windows Terminal | Legacy conhost is not a supported target |

### 6.3 macOS Terminal.app

| Aspect | Status | Notes |
|---|---|---|
| ANSI/VT sequences | Full support | Terminal.app has full VT100/VT220 support |
| Unicode | Full support | macOS ships with comprehensive Unicode fonts |
| Scroll regions | Full support | Standard VT feature |
| Color | 256 colors | No truecolor in older versions; Spectre degrades gracefully |
| Resize detection | Works via `Console.WindowHeight/Width` | .NET reads `TIOCGWINSZ` ioctl |
| `Console.CancelKeyPress` | Works | Standard .NET behavior on macOS |

### 6.4 iTerm2 (macOS)

| Aspect | Status | Notes |
|---|---|---|
| All features | Full support | iTerm2 has excellent VT and Unicode support |
| Truecolor | Full support | Better than Terminal.app |
| Scrollback | Unlimited (configurable) | No content loss on resize |
| Accessibility | iTerm2 has its own screen reader integration | Better than Terminal.app for VoiceOver |

### 6.5 Linux Terminal Emulators

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

### 6.6 SSH Sessions

| Aspect | Status | Notes |
|---|---|---|
| Terminal capabilities | Depends on client terminal | The SSH client's terminal determines capabilities |
| TERM variable | Must be set correctly | `xterm-256color` is recommended |
| Resize | `SIGWINCH` forwarded by SSH | .NET detects resize via `Console.WindowHeight/Width` |
| Latency | May affect spinner appearance | High-latency connections may see jerky spinner updates |
| `Console.CancelKeyPress` | Works | SSH forwards Ctrl+C correctly |

### 6.7 Platform Summary

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
