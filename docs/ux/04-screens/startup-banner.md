# Screen: Startup Banner (Prescriptive)

## Overview

The startup banner is the first visual the user encounters when launching BoydCode.
It has three jobs:

1. **Establish identity** -- the user should know within one second that they are
   in BoydCode, not another tool.
2. **Communicate readiness** -- the user should see at a glance whether the app
   is configured and ready, or requires setup.
3. **Orient** -- the user should see the active provider, model, project, engine,
   and directories so they know what context they are working in.

The banner is the first content block in the conversation view's scroll buffer.
It is rendered once during startup and is never redrawn. This is intentional: the
banner is static content, not an updating region.

In the persistent TUI architecture, the banner is appended to the conversation
view before the first input prompt appears. The user can scroll up to review the
banner at any time during the session.

This spec is PRESCRIPTIVE -- it describes what the screen SHOULD look like.

---

## Lifecycle

```
1. App launches         -> Terminal dimensions detected
2. Provider activated   -> Provider/model/project metadata resolved
3. TUI application starts -> View hierarchy created
4. Banner rendered      -> ASCII art (or compact), info grid, status, hint
5. Engine initialized   -> (after banner, before first prompt)
6. Input view ready     -> `> _` in the input view
```

The banner is rendered synchronously during application startup. It completes
before the execution engine is created and before the first input prompt appears.
The banner content is appended to the conversation view's scroll buffer as the
first content block.

---

## Layout (120 columns) -- Full Banner (Height >= 30)

When the terminal has 30 or more rows, the full ASCII art banner is shown.

```
(blank line)
  ██████╗  ██████╗ ██╗   ██╗██████╗            Users:      1
  ██╔══██╗██╔═══██╗╚██╗ ██╔╝██╔══██╗           Revenue:    $0
  ██████╔╝██║   ██║ ╚████╔╝ ██║  ██║           Valuation:  $0,000,000,000
  ██╔══██╗██║   ██║  ╚██╔╝  ██║  ██║           Commas:     tres
  ██████╔╝╚██████╔╝   ██║   ██████╔╝           Status:     pre-unicorn
  ╚═════╝  ╚═════╝    ╚═╝   ╚═════╝
                   ██████╗  ██████╗ ██████╗ ███████╗
                  ██╔════╝ ██╔═══██╗██╔══██╗██╔════╝
                  ██║      ██║   ██║██║  ██║█████╗
                  ██║      ██║   ██║██║  ██║██╔══╝
                  ╚██████╗ ╚██████╔╝██████╔╝███████╗
                   ╚═════╝  ╚═════╝ ╚═════╝ ╚══════╝
  v0.1  Artificial Intelligence, Personal Edition
(blank line)
──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
(blank line)
  Provider  Gemini                  Project  my-project
  Model     gemini-2.5-pro          Engine   InProcess
  cwd       C:\Users\jason\source\repos\my-project
  Git       C:\Users\jason\source\repos\my-project (main)
(blank line)
  ✓ Ready  Commands run in a constrained PowerShell runspace.
(blank line)
  Type a message to start, or /help for available commands.
(blank line)
```

### Anatomy

The full banner has 7 visual sections.

1. **Leading blank line** -- Provides breathing room at the top of the
   conversation view.

2. **BOYD block** (6 rows) -- `[bold cyan]` block-letter ASCII art at 2-space
   indent. The first 5 rows carry a dim metadata sidebar right-justified after
   a gap. The sidebar values (Users, Revenue, Valuation, Commas, Status) are
   static joke text -- they never change and serve purely as brand personality.
   The 6th row has no sidebar.

3. **CODE block** (6 rows) -- `[bold blue]` block-letter ASCII art. The block
   is indented to start beneath the "O-Y-D" columns of the BOYD block, creating
   a staggered two-line wordmark. No sidebar.

4. **Tagline** -- `[dim]v{version}  Artificial Intelligence, Personal Edition[/]`
   at 2-space indent. Version is the assembly version.

5. **Rule separator** -- A dim horizontal rule spanning the full width.
   No title. Separated by blank lines above and below.

6. **Info grid** -- Configuration key-value display using the Info Grid component
   pattern (#9 in 07-component-patterns.md). Labels in `[dim]`, values in
   `[cyan]`. Layout:
   - Row 1: Provider + Project (two pairs, side by side)
   - Row 2: Model + Engine (two pairs, side by side)
   - Row 3: cwd (single value, full width)
   - Row 4: Docker image (only if project has `DockerImage` set)
   - Row 5: Git repo root + branch (only if CWD is inside a git repository)

7. **Status footer** -- One of:
   - Configured: `  [green]✓[/] [dim]Ready  {engine description}[/]`
   - Not configured: `  [yellow bold]Not configured[/] [dim]Run[/] [bold]/provider setup[/] [dim]or pass[/] [bold]--api-key[/]`

8. **Hint line** (only when configured) --
   `  [dim italic]Type a message to start, or /help for available commands.[/]`

9. **Trailing blank line** -- Provides visual separation at the end of the
   banner block.

### Metadata Sidebar

The sidebar text is appended to each BOYD art row with enough whitespace to
push it past the art block. The sidebar values are:

```
Users:      1
Revenue:    $0
Valuation:  $0,000,000,000
Commas:     tres
Status:     pre-unicorn
```

All sidebar text uses `[dim]` styling. The sidebar is purely decorative -- it
communicates brand personality (self-deprecating humor about a solo project).
It contains no functional information.

**Width constraint**: The BOYD art is ~48 characters. The sidebar adds ~30
characters. With 2-space indent and padding, the total line width is ~77
characters, fitting within 80 columns.

### Brand Color Rationale

- `[bold cyan]` for BOYD: Cyan is the `info` token, which maps to "identifiers,
  names, data values." The product name is an identifier.
- `[bold blue]` for CODE: Blue is the `accent` token, which maps to "brand,
  interactive elements, commands." The second word provides visual contrast
  against the first.
- These are the ONLY two permitted color+bold combinations for brand display
  (see 06-style-tokens.md Section 1.3).

---

## Layout (120 columns) -- Compact Banner (Height 15-29)

When the terminal has 15 to 29 rows, ASCII art is replaced with a single-line
brand wordmark. This saves 12 rows of vertical space.

```
(blank line)
  BOYDCODE  v0.1  AI Coding Assistant
(blank line)
──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
(blank line)
  Provider  Gemini                  Project  my-project
  Model     gemini-2.5-pro          Engine   InProcess
  cwd       C:\Users\jason\source\repos\my-project
  Git       C:\Users\jason\source\repos\my-project (main)
(blank line)
  ✓ Ready  Commands run in a constrained PowerShell runspace.
(blank line)
  Type a message to start, or /help for available commands.
(blank line)
```

### Compact Wordmark

The compact line uses inline markup instead of block letters:

```
  [bold cyan]BOYD[/][bold blue]CODE[/]  [dim]v{version}  AI Coding Assistant[/]
```

Two spaces separate the wordmark from the version/tagline. The tagline is
shortened to "AI Coding Assistant" (from "Artificial Intelligence, Personal
Edition") to respect the compact context.

Everything below the Rule separator is identical to the full banner.

---

## Layout (120 columns) -- Minimal Height (Height 10-14)

When the terminal has only 10 to 14 rows, the banner is omitted entirely. The
info grid and status footer still render, but without the brand display or rule
separator.

```
  Provider  Gemini                  Project  my-project
  Model     gemini-2.5-pro          Engine   InProcess
  cwd       C:\Users\jason\source\repos\my-project
(blank line)
  ✓ Ready  Commands run in a constrained PowerShell runspace.
(blank line)
```

**Rationale**: At 10-14 rows, every row is precious. The brand display and
decorative separator consume space that conversation content needs. The user
still sees enough configuration context to orient, and the status footer
confirms readiness.

No hint line is shown at this tier -- the user has limited vertical space and
likely knows the product.

---

## Layout (120 columns) -- Fallback (Height < 10)

When the terminal has fewer than 10 rows, the banner is a single status line:

```
  ✓ BoydCode ready (Gemini, gemini-2.5-pro, my-project)
```

Or if not configured:

```
  BoydCode: Not configured. Run /provider setup or pass --api-key
```

No info grid, no brand display, no rule separator. At this height tier, the
app falls back to minimal output (see chat-loop.md Non-layout state).

---

## Layout (80 columns) -- Full Banner

At 80 columns, the full ASCII art banner fits because the BOYD art + sidebar
is approximately 77 characters wide. The info grid wraps more aggressively.

```
(blank line)
  ██████╗  ██████╗ ██╗   ██╗██████╗           Users:      1
  ██╔══██╗██╔═══██╗╚██╗ ██╔╝██╔══██╗          Revenue:    $0
  ██████╔╝██║   ██║ ╚████╔╝ ██║  ██║          Valuation:  $0B
  ██╔══██╗██║   ██║  ╚██╔╝  ██║  ██║          Commas:     tres
  ██████╔╝╚██████╔╝   ██║   ██████╔╝          Status:     pre
  ╚═════╝  ╚═════╝    ╚═╝   ╚═════╝
                   ██████╗  ██████╗ ██████╗ ███████╗
                  ██╔════╝ ██╔═══██╗██╔══██╗██╔════╝
                  ██║      ██║   ██║██║  ██║█████╗
                  ██║      ██║   ██║██║  ██║██╔══╝
                  ╚██████╗ ╚██████╔╝██████╔╝███████╗
                   ╚═════╝  ╚═════╝ ╚═════╝ ╚══════╝
  v0.1  Artificial Intelligence, Personal Edition
(blank line)
──────────────────────────────────────────────────────────────────────────────────
(blank line)
  Provider  Gemini         Project  my-project
  Model     gemini-2.5-    Engine   InProcess
            pro
  cwd       C:\Users\jason\source\repos\my-project
  Git       C:\Users\jason\source\repos\my-project
            (main)
(blank line)
  ✓ Ready  Commands run in a constrained PowerShell
            runspace.
(blank line)
  Type a message to start, or /help for commands.
(blank line)
```

### 80-Column Adaptations

1. **Sidebar text truncated**: "Valuation" shows `$0B` instead of
   `$0,000,000,000`. "Status" shows `pre` instead of `pre-unicorn`. This
   keeps sidebar lines under 80 characters.

2. **Info grid wrapping**: Long values (model names, file paths) wrap within
   their grid column. The key column maintains its width; the value column
   absorbs the wrapping.

3. **Hint line shortened**: "or /help for available commands" becomes
   "or /help for commands" to avoid wrapping.

---

## Layout (80 columns) -- Compact Banner

```
(blank line)
  BOYDCODE  v0.1  AI Coding Assistant
(blank line)
──────────────────────────────────────────────────────────────────────────────────
(blank line)
  Provider  Gemini         Project  my-project
  Model     gemini-2.5-    Engine   InProcess
            pro
  cwd       C:\Users\jason\source\repos\my-project
(blank line)
  ✓ Ready  Commands run in a constrained PowerShell
            runspace.
(blank line)
  Type a message to start, or /help for commands.
(blank line)
```

The compact wordmark is well under 80 columns (approximately 40 characters).
Info grid behavior is identical to the 80-column full banner.

---

## Layout (< 80 columns) -- Compact Width

At terminals narrower than 80 columns, the full ASCII art does NOT render
regardless of height. The compact wordmark is always used because the ASCII
art lines are approximately 77 characters wide and would wrap.

```
(blank line)
  BOYDCODE  v0.1
(blank line)
────────────────────────────────────────────────────────
(blank line)
 Provider  Gemini
 Model     gemini-2.5-pro
 Project   my-project
 Engine    InProcess
 cwd       C:\Users\jason\repos\my-project
(blank line)
 ✓ Ready
(blank line)
```

### Compact Width Adaptations

1. **Wordmark only shows version**: The tagline is dropped entirely. The
   wordmark is `[bold cyan]BOYD[/][bold blue]CODE[/]  [dim]v{version}[/]`.

2. **Info grid becomes single-column**: The paired rows (Provider/Project,
   Model/Engine) are split into individual rows. 1-space indent per
   the compact tier in 06-style-tokens.md Section 8.1.

3. **Status footer simplified**: Only the checkmark and "Ready" text. The
   engine description is dropped to avoid wrapping.

4. **No hint line**: At compact width, the user has likely used the app before
   (power users who run terminals at narrow widths).

---

## Variant: Container Engine

When the project is configured for Docker execution, two differences appear.

### Additional Info Grid Row

```
  Provider  Gemini                  Project  my-project
  Model     gemini-2.5-pro          Engine   Container
  Docker    python:3.12-slim
  cwd       C:\Users\jason\source\repos\my-project
```

The "Docker" row appears between Engine and cwd. The value is the Docker image
name from `Project.DockerImage`. Style: label in `[dim]`, value in `[cyan]`.

### Container Status Footer

```
  ✓ Ready  Commands execute inside a Docker container.
```

The engine description changes from the InProcess message to the container
message. The checkmark and Ready styling are identical.

---

## Variant: Not Configured

When no API key is available for the active provider, the status footer
changes to guide the user toward configuration.

### 120-Column Not Configured

```
  Provider  Gemini                  Project  my-project
  Model     gemini-2.5-pro          Engine   InProcess
  cwd       C:\Users\jason\source\repos\my-project
(blank line)
  Not configured  Run /provider setup or pass --api-key
(blank line)
```

Markup:
```
  [yellow bold]Not configured[/]  [dim]Run[/] [bold]/provider setup[/] [dim]or pass[/] [bold]--api-key[/]
```

### Behavior

- No hint line is shown when not configured -- the status footer IS the
  guidance. Showing both would be redundant.
- The info grid still renders with the provider and model the app will use
  once configured. This reassures the user that their `--provider` flag or
  project binding took effect.
- The session loop still starts, but the first LLM call will fail with a
  clear error. The user can fix configuration in-session via `/provider setup`.

---

## Variant: Session Resume

When `--resume <SESSION_ID>` is passed, a resume notice appears after the
hint line (or after the status footer if no hint line is shown).

### 120-Column Resume

```
  ✓ Ready  Commands run in a constrained PowerShell runspace.
(blank line)
  Type a message to start, or /help for available commands.
(blank line)
  Resumed session abc12345 (24 messages from 2026-02-25 14:30)
(blank line)
```

Markup:
```
  [dim italic]Resumed session {sessionId} ({messageCount} messages from {timestamp})[/]
```

The session ID is truncated to 8 characters for display. The timestamp uses
the local culture's date/time format. The message count is the total messages
in the resumed conversation.

---

## Variant: Missing Directory Warning

When a project's configured directory does not exist on the filesystem, a
warning renders BEFORE the banner.

```
[yellow]![/] [yellow]Warning:[/] Directory does not exist: C:\Users\jason\repos\old-project

(blank line)
  ██████╗  ██████╗ ██╗   ██╗██████╗            Users:      1
  ...
```

The warning uses the standard Warning status message pattern
(07-component-patterns.md #7). It is NOT inside the banner -- it precedes it.
The banner's info grid still shows the directory, but the user has been alerted
that it will not be accessible.

---

## States

| State | Height Tier | Width | Condition | Visual |
|-------|-------------|-------|-----------|--------|
| Full + Configured | >= 30 | >= 80 | API key present | ASCII art, info grid, green Ready, hint |
| Full + Not Configured | >= 30 | >= 80 | No API key | ASCII art, info grid, yellow Not Configured |
| Compact + Configured | 15-29 | >= 80 | API key present | Wordmark line, info grid, green Ready, hint |
| Compact + Not Configured | 15-29 | >= 80 | No API key | Wordmark line, info grid, yellow Not Configured |
| Minimal + Configured | 10-14 | any | API key present | Info grid + Ready only |
| Minimal + Not Configured | 10-14 | any | No API key | Info grid + Not Configured only |
| Fallback + Configured | < 10 | any | API key present | Single status line |
| Fallback + Not Configured | < 10 | any | No API key | Single error-guidance line |
| Narrow + any | any | < 80 | any | Wordmark (no art), single-column grid |
| Container | any | any | Docker engine | Extra Docker row, container status |
| Git | any | any | CWD in git repo | Extra Git row with branch |
| Resume | any | any | --resume flag | Extra dim italic resume notice |
| Missing Dir | any | any | Dir not found | Warning before banner |

### Priority Rules

When multiple variants apply:

1. Height tier selects the banner variant (Full > Compact > Minimal > Fallback)
2. Width tier overrides to compact wordmark if < 80 columns
3. Container/Git/Resume are additive (they add rows or lines)
4. Missing Dir warning is independent (renders before banner)
5. Not Configured replaces Configured status (mutually exclusive)

---

## Rendering Notes

### Tier Detection

The banner implementation detects terminal dimensions at startup and selects the
appropriate tier:

- **Full** (height >= 30, width >= 80): ASCII art with sidebar
- **Compact** (height 15-29, width >= 80): Single-line wordmark
- **Minimal** (height 10-14): Info grid and status only
- **Fallback** (height < 10): Single status line
- **Narrow** (width < 80): Compact wordmark, single-column grid

If the terminal height cannot be determined, default to 24 rows (compact tier).

### Info Grid Layout

At standard width (>= 80 columns), the info grid uses a 4-column layout with
paired key-value rows. Labels render in `[dim]`, values in `[cyan]`. Single-value
rows (cwd, Docker, Git) span the full width.

At compact width (< 80 columns), the info grid uses a 2-column layout with each
key-value pair on its own row.

### Content Composition

The banner is composed as a sequence of styled content blocks (art lines, rule,
grid, status line, hint) and rendered into the conversation view as the first
content block. The content renderer handles all markup styling, escaping of
user-provided values, and width adaptation.

---

## Transition to First Prompt

After the banner renders, the following sequence occurs:

1. Execution engine is created via `IExecutionEngineFactory`
2. Session is created or resumed via `ISessionRepository`
3. The input view shows `> _` and accepts keyboard input
4. The user types their first message

The TUI application is already active when the banner renders. The banner is
the first content block in the conversation view's scroll buffer. It is rendered
once and never redrawn. The user can scroll up in the conversation view to see
the banner at any time during the session.

---

## Edge Cases

- **Terminal narrower than 48 columns**: The ASCII art lines are ~48 characters
  minimum (the BOYD block without sidebar). Below 48 columns, even the art
  wraps. Detection: if `width < 48`, always use the compact wordmark regardless
  of height tier. The wordmark "BOYDCODE  v0.1" is only ~20 characters.

- **Terminal wider than 200 columns**: The Rule separator stretches to full
  width. The ASCII art and info grid remain left-aligned. Excess width is empty
  space. No visual issues.

- **Non-interactive/piped environment**: Terminal dimension detection may fail.
  The fallback defaults to `height = 24`, selecting the compact banner tier.
  When output is piped, markup is stripped automatically. The ASCII art renders
  as plain text (box-drawing characters are not markup). The info grid renders
  as tab-separated values. The rule renders as dashes.

- **No git repository**: The Git row in the info grid is omitted entirely.
  No empty state placeholder is shown -- the row simply does not exist.

- **No Docker image**: The Docker row in the info grid is omitted entirely.
  The status footer uses the InProcess engine message.

- **Ambient project (_default)**: The Project value shows `[dim](default)[/]`
  using the empty state label pattern from 06-style-tokens.md Section 7.5.

- **Unknown provider on CLI**: An error is rendered before the banner using
  the Error Display component pattern (#22 in 07-component-patterns.md). The
  banner then renders with the fallback provider (Gemini).

- **Provider activation failure**: `isConfigured` remains false. The banner
  shows the "Not configured" status footer.

- **Unicode not supported**: When the terminal does not support Unicode, the
  block-drawing characters in the ASCII art may not render. In this case,
  fall back to the compact wordmark regardless of height tier.

- **Version string**: The version is read from the assembly at startup. If
  unavailable, use `"dev"` as the version string.

---

## Accessibility

### Screen Reader

- The ASCII art is decorative and conveys no functional information. Screen
  readers will read the block characters as noise. In accessible mode, the
  art is replaced with the compact wordmark: `BOYDCODE v{version}`.
- The info grid labels and values are read sequentially: "Provider Gemini
  Model gemini-2.5-pro Project my-project Engine InProcess."
- The status footer is read as: "Ready Commands run in a constrained PowerShell
  runspace" or "Not configured Run /provider setup or pass --api-key."

### NO_COLOR

- The `[bold cyan]` and `[bold blue]` art loses color but retains bold weight.
  The block characters are still visible as ASCII art.
- Info grid labels lose dim styling, values lose cyan coloring. Structure
  remains readable via the Grid layout.
- The checkmark symbol `✓` remains visible as a text character.
- "Not configured" loses yellow styling but retains bold weight.

### Accessible Mode (BOYDCODE_ACCESSIBLE=1)

Full accessible mode replaces the entire banner with a structured text block:

```
BOYDCODE v0.1

Provider: Gemini
Model: gemini-2.5-pro
Project: my-project
Engine: InProcess
Directory: C:\Users\jason\source\repos\my-project
Git: main

[OK] Ready. Commands run in a constrained PowerShell runspace.

Type a message to start, or /help for available commands.
```

- No ASCII art (including the decorative block characters)
- No color or weight markup
- No Unicode symbols (checkmark replaced with `[OK]`)
- Key-value pairs use colon separators, not grid alignment
- No Rule separator (ASCII dashes work, but the line adds noise)
- No sidebar jokes (these are visual-only brand elements)

---

## Performance

The banner renders in a single synchronous pass. No async operations, no
network calls. Expected render time: < 10ms.

Provider activation (API key validation, OAuth token refresh) happens BEFORE
the banner renders. If activation involves network calls (OAuth token refresh),
those complete before the banner -- the banner shows the result, not the
process. If activation takes longer than expected, the user sees a brief
pause before the banner appears. No spinner or progress indicator is shown
for this pause because:

1. It is typically < 200ms (local file reads for stored credentials)
2. OAuth token refresh adds 500ms-2s but happens rarely
3. A spinner before the banner would create a visual stutter (spinner appears,
   disappears, banner appears)

If activation consistently takes > 1 second, a future enhancement could show
a brief `Connecting...` line before the banner. This is not in v2 scope.

---

## Component Patterns Used

| Pattern | Reference | Usage |
|---------|-----------|-------|
| Info Grid | 07-component-patterns.md #9 | Configuration key-value display |
| Status Message | 07-component-patterns.md #7 | Ready/Not Configured footer |
| Banner | 07-component-patterns.md #24 | Full and compact brand display |
| Section Divider | 07-component-patterns.md #8 | Rule separator |
| Empty State | 07-component-patterns.md #21 | "(default)" for ambient project |
| Error Display | 07-component-patterns.md #22 | Pre-banner error/warning |

---

## Markup Tokens Used

| Token | Style Reference | Usage |
|-------|-----------------|-------|
| `[bold cyan]` | Brand primary (1.3) | "BOYD" ASCII art / wordmark |
| `[bold blue]` | Brand secondary (1.3) | "CODE" ASCII art / wordmark |
| `[dim]` | Muted (1.1) | Sidebar, tagline, info labels, engine desc |
| `[cyan]` | Info (1.1) | Info grid values |
| `[green]` + `✓` | Success (1.1) | "Ready" status |
| `[yellow bold]` | Warning emphasis (1.3) | "Not configured" status |
| `[bold]` | Level 1 (2.1) | "/provider setup", "--api-key" in guidance |
| `[dim italic]` | Level 4 (2.1) | Hint line, resume notice |
| Dim Rule | Muted (1.1) | Banner separator |
