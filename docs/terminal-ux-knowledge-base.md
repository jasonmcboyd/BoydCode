# Terminal & Console Application UX Knowledge Base

A comprehensive reference for designing and building excellent terminal user interfaces, with emphasis on .NET Spectre.Console applications. Compiled from authoritative sources across CLI design, TUI patterns, accessibility, and interactive terminal UX.

---

## Table of Contents

1. [CLI Design Guidelines](#1-cli-design-guidelines)
2. [Spectre.Console Patterns](#2-spectreconsole-patterns)
3. [TUI (Text User Interface) Design](#3-tui-text-user-interface-design)
4. [Console Color & Typography](#4-console-color--typography)
5. [Interactive Terminal Patterns](#5-interactive-terminal-patterns)
6. [Error Handling UX in CLIs](#6-error-handling-ux-in-clis)
7. [Progressive Disclosure in CLIs](#7-progressive-disclosure-in-clis)
8. [Terminal Animation & Feedback](#8-terminal-animation--feedback)
9. [ASCII Art & Box Drawing](#9-ascii-art--box-drawing)
10. [Accessibility in Terminal Apps](#10-accessibility-in-terminal-apps)

---

## 1. CLI Design Guidelines

### Core Philosophy

**Human-first, machines second.** Traditional UNIX commands were program-to-program tools. Modern CLIs should prioritize human users first, even when machine readability remains important. Today's command-line programs function as text-based user interfaces rather than pure scripting platforms.

**Simple, composable parts.** Follow UNIX principles by building modular programs with clean interfaces. Programs should work together through standard mechanisms: stdin/stdout/stderr, exit codes, signals, and plain text or JSON output. Designing for composability does not conflict with human-first design.

**Consistency across programs.** Terminal conventions are deeply ingrained in user muscle memory. Follow existing patterns for flags, arguments, and environment variables. When convention compromises usability, break it intentionally but document the decision clearly.

**Ease of discovery.** CLIs need not force memorization. Provide comprehensive help text, abundant examples, and contextual suggestions. Show what is possible, guide users toward next steps, and suggest corrections when input is invalid.

**Conversation as the norm.** CLI interaction is inherently conversational: users try commands, receive feedback, adjust, and retry. Design for this pattern by suggesting possible corrections, clarifying intermediate states, and confirming before destructive actions.

**Robustness (subjective and objective).** Software must be robust (handle unexpected input gracefully, remain idempotent where possible) and feel robust (respond quickly, explain errors, avoid scary stack traces). Simplicity enhances robustness by reducing special cases.

### The 10 Principles for Delightful CLIs (Atlassian)

1. **Align with established conventions** -- Leverage existing CLI patterns rather than reinventing the wheel. Users are already familiar with standard patterns from tools they use daily.

2. **Build --help into the CLI** -- Provide built-in documentation accessible through help commands. List all commands, subcommands, and short-name equivalents with descriptions.

3. **Show progress visually** -- Without a GUI to provide immediate visual feedback, CLIs must keep users informed about system status using progress bars, spinners, and step-based feedback.

4. **Create a reaction for every action** -- Every user action should produce an equal and appropriate reaction, clearly highlighting the current system status.

5. **Craft human-readable error messages** -- Transform technical errors into actionable, user-friendly messages with concrete resolution suggestions and links to additional resources.

6. **Support your skim-readers** -- Keep instructions to no more than 3 sentences (around 50-75 characters) per paragraph. Use text formatting, lists, icons, and visual hierarchy.

7. **Suggest the next best step** -- Identify typical usage patterns and suggest the logical next command after each execution, reducing documentation lookups.

8. **Consider your options** -- Rather than failing when required options are missing, prompt users to provide outstanding information. Supply sensible defaults.

9. **Provide an easy way out** -- Remind users there is a simple way to stop the task, and that returning to the prompt is only a short Ctrl+C away.

10. **Flags over args** -- Use labeled flags instead of positional arguments. Using flags means the user does not need to memorize argument order. Provide both short (`-e`) and long (`--environment`) versions.

### Output Design

- **Human-readable by default.** Detect whether output goes to a TTY (interactive terminal). Format output for humans unless piped.
- **Machine-readable where appropriate.** Streams of plain text compose naturally via pipes. Ensure output works with `grep`, `awk`, and other UNIX tools.
- **Provide --json for structured data.** Emit formatted JSON when `--json` is passed, enabling integration with tools like `jq`.
- **Display output on success.** Traditional silent-on-success conventions make programs appear broken. Show brief confirmation of what happened.
- **Explain state changes.** When operations modify the system, describe what changed. Users need to model internal state mentally.
- **Suggest next commands.** When commands form workflows, suggest what to run next. This teaches users and reveals functionality.

### Arguments and Flags

- **Prefer flags to arguments.** Flags are clearer and easier to extend in future versions.
- **Provide full-length versions.** Include both `-h` and `--help`. Full versions are essential in scripts for clarity.
- **Reserve single-letter flags.** Use one-letter flags only for commonly used options.
- **Make defaults the right choice.** If something is not the default, you are degrading the experience for most users.
- **Prompt for missing input.** When users do not provide required arguments or flags, prompt for them interactively (unless stdin is non-interactive).
- **Never require prompts.** Always provide a way to pass input via flags or arguments for automation.
- **Never accept secrets in flags.** Secrets passed via flags leak into `ps` output and shell history. Accept sensitive data only via files (`--password-file`) or stdin.

### Standard Flag Conventions

| Flag | Purpose | Notes |
|------|---------|-------|
| `-a`, `--all` | All items | Universal convention |
| `-d`, `--debug` | Debugging output | Verbose internal info |
| `-f`, `--force` | Override safety protections | Skip confirmations |
| `--json` | JSON output format | Machine-readable |
| `-h`, `--help` | Help (only) | Do not overload |
| `-n`, `--dry-run` | Preview changes without executing | Show what would happen |
| `--no-input` | Disable all prompts | For CI/scripting |
| `-o`, `--output` | Output file | Redirect output |
| `-q`, `--quiet` | Suppress non-essential output | For scripts |
| `--version` | Version information | Universal convention |

### Subcommand Design

- **Consistent naming across subcommands.** Use the same flag names for the same purposes. Format output consistently.
- **Consistent naming levels.** Either `noun verb` or `verb noun` works, but be consistent across all subcommands.
- **Avoid ambiguous names.** Do not have both `update` and `upgrade` -- confusion results.
- **Do not allow arbitrary abbreviations.** Allow explicit, stable aliases (e.g., `install` and `i`), but do not allow automatic prefix matching.
- **Avoid catch-all subcommands.** Do not assume omitted subcommands mean a "default" command. This breaks future extensibility.

### Naming Conventions (Heroku Style Guide)

- **Topics** are plural nouns (e.g., `apps`, `config`)
- **Commands** are verbs (e.g., `create`, `list`)
- Single lowercase word without spaces, hyphens, or underscores
- Multiple words: use kebab-case (e.g., `pg:credentials:repair-default`)
- Descriptions: lowercase, no period, fit on 80-character width screens

### Streams: stdout, stderr, stdin

| Category | Stream | Use Case |
|----------|--------|----------|
| Primary output data | stdout | Normal command output, results |
| Warnings | stderr | Non-fatal issues |
| Errors | stderr | Failures |
| Action status/spinners | stderr | Out-of-band progress information |

**Golden rules:**
- Errors go to stderr, output to stdout
- Accept data via stdin for piping flexibility
- If stdout is redirected, strip markup and render plain text

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | General error |
| 2 | Usage error (invalid arguments) |
| 64-78 | POSIX sysexits.h categories (from sendmail heritage) |

**Best practices:**
- Zero means success, non-zero means failure
- Map different failure categories to different non-zero codes
- Create searchable error codes for documentation lookup
- Document all exit codes used by the application
- POSIX systems: unsigned 8-bit integers (0-255)
- Windows: 32-bit integers (-2,147,483,648 to 2,147,483,647)

### Signals and Control Characters

- **Ctrl+C (SIGINT):** Exit as quickly as possible. Announce cleanup, then clean up with timeouts so it cannot hang forever.
- **Double Ctrl+C:** If cleanup takes long, hitting Ctrl+C again should force immediate termination. Communicate this: "Gracefully stopping... (press Ctrl+C again to force)".
- **Design for interrupted cleanup.** Programs should start correctly even if the previous invocation was interrupted (crash-only software).
- **Ctrl+Z (SIGTSTP):** Suspends the process. Not termination -- the program is paused but not killed.

### Configuration Precedence

Apply configuration in this order (highest to lowest priority):
1. Command-line flags
2. Environment variables
3. Project-level config (e.g., `.env`)
4. User-level config
5. System-wide config

### Environment Variables

- Use for context-dependent behavior
- For naming: uppercase letters, numbers, and underscores only
- Favor single-line values (multi-line causes usability issues with `env`)
- Respect `NO_COLOR` (disable color), `FORCE_COLOR` (enable color)
- Never read secrets from environment variables (they leak to child processes, `docker inspect`, `systemctl show`)

### Robustness

- **Respond within 100ms.** Print something immediately. A program that responds fast feels robust.
- **Show progress for long operations.** Programs that produce no output for minutes appear broken.
- **Implement timeouts.** Allow network timeouts to be configurable with reasonable defaults.
- **Enable recovery.** Let users resume after transient failures. Re-running should pick up where the command left off.
- **Embrace crash-only design.** Minimize cleanup requirements so programs can exit immediately on failure. Defer cleanup to the next run.
- **Anticipate misuse.** Users wrap programs in scripts, run them on poor connections, launch many instances simultaneously, and use them in untested environments.

### Anti-Patterns

- Dumping pages of help text as the first thing a user sees
- Requiring all flags upfront instead of prompting interactively
- Showing generic "invalid command" with no guidance
- Always returning exit code 0 regardless of outcome
- Writing errors to stdout instead of stderr
- Clearing the screen (users value their scrollback history)
- Secret data collection without user consent

### Key Sources

- [Command Line Interface Guidelines (clig.dev)](https://clig.dev/)
- [10 Design Principles for Delightful CLIs (Atlassian)](https://www.atlassian.com/blog/it-teams/10-design-principles-for-delightful-clis)
- [UX Patterns for CLI Tools (Lucas F. Costa)](https://www.lucasfcosta.com/blog/ux-patterns-cli-tools)
- [Better CLI Design Guide](https://bettercli.org/)
- [Heroku CLI Style Guide](https://devcenter.heroku.com/articles/cli-style-guide)
- [CLI Guidelines GitHub Repository](https://github.com/cli-guidelines/cli-guidelines)

---

## 2. Spectre.Console Patterns

### Core Architecture

Spectre.Console is a .NET library for building beautiful, cross-platform console applications, heavily inspired by Python's Rich library. It provides:

- **Renderables/Widgets:** Table, Panel, Tree, Grid, Columns, Rule, FigletText, Calendar, BarChart, BreakdownChart, Canvas, Layout
- **Live display contexts:** Live, Status, Progress
- **Prompts:** TextPrompt, SelectionPrompt, MultiSelectionPrompt, ConfirmationPrompt
- **Markup system:** Rich text formatting with `[tag]content[/]` syntax
- **Profile detection:** Automatic capability detection for color, Unicode, interactivity

### Capability Detection

Spectre.Console examines the environment to determine terminal capabilities:

```csharp
// Profile properties
AnsiConsole.Profile.Width          // Terminal width in columns
AnsiConsole.Profile.Capabilities.Interactive  // Whether prompts can wait for input
AnsiConsole.Profile.Capabilities.Unicode     // Whether Unicode box-drawing is available
AnsiConsole.Profile.Capabilities.Ansi        // Whether ANSI escape sequences work
AnsiConsole.Profile.Capabilities.ColorSystem // None, Standard, EightBit, TrueColor
```

**Automatic adaptations:**
- Tables and panels adjust to terminal width on each render
- In CI environments, interactive mode is automatically disabled
- Color is downgraded or disabled based on terminal capabilities
- Box-drawing falls back from Unicode to ASCII when Unicode is unavailable

**Manual overrides:**
```csharp
var settings = new AnsiConsoleSettings
{
    Ansi = AnsiSupport.Yes,
    ColorSystem = ColorSystemSupport.TrueColor,
    Interactive = InteractionSupport.No,
};
var console = AnsiConsole.Create(settings);
```

### Markup System

**Syntax:** `[style]text[/]` where style can be colors, decorations, or combinations.

```csharp
// Basic usage
AnsiConsole.MarkupLine("[bold red]Error:[/] Something went wrong");
AnsiConsole.MarkupLine("[green]Success![/]");
AnsiConsole.MarkupLine("[dim]This is secondary information[/]");

// Combined styles
AnsiConsole.MarkupLine("[bold underline blue]Important[/]");
```

**Escaping (critical for safety):**

Any user input containing `[` or `]` will cause a runtime error while rendering. Three approaches to escaping:

1. **Double square brackets:** `[[` renders as `[`, `]]` renders as `]`
2. **Markup.Escape():** `Markup.Escape(userInput)` escapes all markup characters
3. **MarkupInterpolated (recommended):** Automatically escapes all interpolation holes

```csharp
// UNSAFE -- user input could contain markup-like syntax
AnsiConsole.MarkupLine($"[blue]{userInput}[/]");  // DANGEROUS

// Safe approach 1: manual escaping
AnsiConsole.MarkupLine($"[blue]{Markup.Escape(userInput)}[/]");

// Safe approach 2: MarkupInterpolated (auto-escapes holes)
AnsiConsole.MarkupInterpolated($"[blue]{userInput}[/]");

// Safe approach 3: Markup.FromInterpolated for IRenderable contexts
table.AddRow(Markup.FromInterpolated($"[green]{userInput}[/]"));
```

**Caveat:** `MarkupInterpolated` only escapes strings; non-string objects with special characters in their `ToString()` output may not be escaped.

### Widget Composition

**Table:** Structured multi-column data with borders and alignment.
```csharp
var table = new Table()
    .Border(TableBorder.Rounded)
    .AddColumn("[bold]Name[/]")
    .AddColumn("[bold]Status[/]");
table.AddRow("Service A", "[green]Running[/]");
table.AddRow("Service B", "[red]Stopped[/]");
AnsiConsole.Write(table);
```

**Panel:** Bordered container for grouping related content.
```csharp
var panel = new Panel(new Markup("[bold]Welcome[/]\nVersion 1.0"))
    .Header("[blue]My App[/]")
    .Border(BoxBorder.Rounded)
    .Padding(1, 0);
AnsiConsole.Write(panel);
```

**Tree:** Hierarchical data display.
```csharp
var tree = new Tree("Root")
    .AddNode("[yellow]Child 1[/]")
    .AddNode("[yellow]Child 2[/]", n => n.AddNode("Grandchild"));
AnsiConsole.Write(tree);
```

**Grid:** Invisible table for aligned multi-column layouts (no borders).
```csharp
var grid = new Grid();
grid.AddColumn(new GridColumn().NoWrap());
grid.AddColumn();
grid.AddRow("[dim]Name:[/]", "[cyan]My Project[/]");
grid.AddRow("[dim]Version:[/]", "[cyan]1.0.0[/]");
AnsiConsole.Write(grid);
```

**Columns:** Auto-flowing items into columns based on terminal width.
```csharp
AnsiConsole.Write(new Columns(
    new Panel("Left").Expand(),
    new Panel("Right").Expand()
));
```

**Layout:** Named regions for complex dashboard-style layouts.
```csharp
var layout = new Layout("Root")
    .SplitColumns(
        new Layout("Left"),
        new Layout("Right"));
layout["Left"].Update(new Panel("Content A"));
layout["Right"].Update(new Panel("Content B"));
AnsiConsole.Write(layout);
```

**Rule:** Horizontal divider for visual section separation.
```csharp
AnsiConsole.Write(new Rule("[bold]Section Title[/]").LeftJustified().RuleStyle("dim"));
```

**FigletText:** Large ASCII art text for banners (use sparingly).
```csharp
AnsiConsole.Write(new FigletText("Welcome").Color(Color.Green));
```

### When to Use Each Widget

| Widget | Use Case | Avoid When |
|--------|----------|------------|
| Table | Structured multi-column data (lists, records) | Single item (use Panel or key-value markup) |
| Panel | Grouping related content, highlighting important info | Nesting panels inside panels (keep flat) |
| Tree | Hierarchical/nested data (file trees, dependency graphs) | Flat lists (use Table) |
| Grid | Aligned label-value pairs, form-like layouts | Bordered data display (use Table) |
| Columns | Side-by-side content that flows naturally | Precise layout control (use Layout) |
| Layout | Complex multi-region dashboards | Simple output (over-engineering) |
| Rule | Section separators, visual breaks | Every few lines (overuse removes meaning) |
| FigletText | Startup banners, celebration moments | Help output, frequent displays |

### Prompt Patterns

**TextPrompt with validation:**
```csharp
var name = AnsiConsole.Prompt(
    new TextPrompt<string>("Project [green]name[/]:")
        .DefaultValue("my-project")
        .Validate(n => n.Length > 0
            ? ValidationResult.Success()
            : ValidationResult.Error("[red]Name cannot be empty[/]")));
```

**SelectionPrompt with search:**
```csharp
var choice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Choose a [green]template[/]:")
        .PageSize(10)
        .EnableSearch()
        .AddChoices("console", "webapi", "classlib", "worker"));
```

**MultiSelectionPrompt:**
```csharp
var features = AnsiConsole.Prompt(
    new MultiSelectionPrompt<string>()
        .Title("Select [green]features[/]:")
        .NotRequired()
        .AddChoices("logging", "auth", "caching", "metrics"));
```

**Secret input:**
```csharp
var password = AnsiConsole.Prompt(
    new TextPrompt<string>("Enter [green]password[/]:")
        .Secret());
```

**Confirmation:**
```csharp
if (AnsiConsole.Confirm("Delete this project?", defaultValue: false))
{
    // proceed with deletion
}
```

### Live Display Patterns

**Live display for streaming content:**
```csharp
await AnsiConsole.Live(new Markup(""))
    .StartAsync(async ctx =>
    {
        var buffer = new StringBuilder();
        await foreach (var token in stream)
        {
            buffer.Append(token);
            ctx.UpdateTarget(new Markup(Markup.Escape(buffer.ToString())));
        }
    });
// After live context ends, render final static result
AnsiConsole.MarkupLine("[green]v[/] Response complete.");
```

**Live display configuration:**
```csharp
AnsiConsole.Live(table)
    .AutoClear(false)                              // Keep content after completion
    .Overflow(VerticalOverflow.Ellipsis)            // Show truncation indicator
    .Cropping(VerticalOverflowCropping.Top)         // Keep recent content visible
    .StartAsync(async ctx => { /* ... */ });
```

**Live display returning a value:**
```csharp
var result = AnsiConsole.Live(new Text("Processing..."))
    .Start(ctx =>
    {
        // ... work ...
        return computedValue;
    });
```

### Status vs Progress

**Status (spinner):** For indeterminate operations where you cannot track completion percentage.
```csharp
await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("Connecting...", async ctx =>
    {
        await ConnectAsync();
        ctx.Status("Authenticating...");
        await AuthenticateAsync();
    });
```

**Progress (progress bar):** For measurable, trackable operations.
```csharp
await AnsiConsole.Progress()
    .AutoClear(false)
    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(),
             new PercentageColumn(), new SpinnerColumn())
    .StartAsync(async ctx =>
    {
        var task = ctx.AddTask("Downloading");
        while (!task.IsFinished)
        {
            await Task.Delay(100);
            task.Increment(1.5);
        }
    });
```

**When to use which:**

| Indicator | Duration | Progress Known? | Example |
|-----------|----------|-----------------|---------|
| Nothing | < 200ms | N/A | Instant operations |
| Status/Spinner | < 10s | No | API calls, auth |
| Progress bar | > 10s | Yes | File downloads, batch processing |
| Indeterminate progress | > 10s | No | Long unknown operations |

### Critical Best Practices (Official)

1. **Thread safety:** Live display is NOT thread safe. Do not use it with other interactive components simultaneously.
2. **No mixing live widgets:** Do not use Status and Progress at the same time. Rendering corruption results.
3. **Single-thread rendering:** Create work threads within the Start method, but render on the main thread.
4. **16 ANSI colors for portability:** Stick to the standard 16 ANSI colors and Spectre.Console will use the color configured in the user's terminal theme.
5. **Extended Unicode opt-in:** Allow users to opt in to extended Unicode characters or configure them manually for the widest terminal support.

### Adaptive Layout Pattern

```csharp
var width = AnsiConsole.Profile.Width;
if (width >= 120)
{
    // Wide layout: side-by-side columns
    AnsiConsole.Write(new Columns(leftPanel, rightPanel));
}
else
{
    // Narrow layout: stacked
    AnsiConsole.Write(leftPanel);
    AnsiConsole.Write(rightPanel);
}
```

### Key Sources

- [Spectre.Console Documentation](https://spectreconsole.net/)
- [Spectre.Console Best Practices (GitHub)](https://github.com/spectreconsole/spectre.console/blob/main/docs/input/best-practices.md)
- [Spectre.Console Examples Repository](https://github.com/spectreconsole/examples)
- [Spectre.Console Live Display](https://spectreconsole.net/console/live/live-display)
- [MarkupInterpolated API](https://spectreconsole.net/api/spectre.console/ansiconsole/e39ddb49)
- [Spectre.Console and String Interpolation (Thirty25)](https://thirty25.blog/blog/2021/12/spectre-console-and-interpolatedstringhandlers)

---

## 3. TUI (Text User Interface) Design

### Fundamental Distinctions

TUIs are distinct from CLIs. CLIs handle input and output line-by-line. TUIs employ the full terminal screen for interactive, multi-region displays supporting cursor movement and structured navigation. TUIs are more similar to GUIs than to CLIs but rendered entirely with text characters.

### Core Design Patterns

**Single Responsibility Views:** Each view should do one thing well. Do not combine listing and editing in the same view. Separate concerns just as you would in GUI design.

**Clear Navigation Paths:** Users should always know how to go back. ESC as "go back" is a widely understood convention. Document all available keyboard shortcuts.

**Visible Keyboard Shortcuts:** Always display available commands, typically at the bottom of the screen. Memory should not be a prerequisite for using the application.

**Consistent Styling:** Define your color scheme and text styles once, then reuse them. This creates visual cohesion and makes updates easier.

**Responsive to Terminal Size:** Design for 80x24 minimum, but take advantage of larger terminals when available. Test at both extremes.

### Terminal vs GUI Paradigm Differences

| Concept | GUI | Terminal |
|---------|-----|----------|
| Navigation | Screens, pages, tabs | Top-to-bottom stream, subcommands |
| Layout | Pixel-level, responsive grids | Character grid, column-based |
| Input | Mouse-first, touch | Keyboard-first, mouse secondary |
| Feedback | Modals, toasts, animations | Inline text, spinners, markup |
| State | Persistent visible UI | Scrollback history, stateless runs |
| Color | Millions, gradients | 16 standard, 256 extended, 24-bit optional |

### When TUI vs CLI

**Use a TUI when:**
- Users interact repeatedly in a session (chat, file management)
- Multiple regions of information need simultaneous display
- Real-time updates are core to the experience
- Keyboard-driven navigation adds significant efficiency

**Use a CLI when:**
- Operations are single-shot (build, deploy, convert)
- Output needs to be piped to other tools
- Scripting and automation are primary use cases
- The operation is simple enough for one command

### Layout Principles

- **Rows over columns for most content.** Terminal text flows naturally top-to-bottom.
- **Reserve columns for related data.** Side-by-side display works for comparisons, dashboards.
- **Fixed regions for status.** Status bars, input areas, and key maps belong in fixed screen positions (typically bottom).
- **Scrollable content areas.** Main content should scroll independently of fixed regions.
- **Minimum viable dimensions.** Design for 80 columns x 24 rows as baseline. Gracefully degrade below this.

### Anti-Patterns

- Building "screens" and "pages" with navigation stacks (terminals lack this concept)
- "Menus" as primary navigation (use subcommands or prompt chains instead)
- Modal dialogs (there is no windowing system; use inline confirmation)
- Pixel-thinking in a character grid (whitespace and alignment replace precise positioning)
- Over-drawing (constantly redrawing the entire screen causes flicker)

### Key Sources

- [Awesome TUIs (GitHub)](https://github.com/rothgar/awesome-tuis)
- [A Designer's Guide to Loving the Terminal](https://www.alexchantastic.com/designers-guide-to-the-terminal)
- [Anatomy of a Textual User Interface](https://textual.textualize.io/blog/2024/09/15/anatomy-of-a-textual-user-interface/)
- [Text-based User Interface (Wikipedia)](https://en.wikipedia.org/wiki/Text-based_user_interface)

---

## 4. Console Color & Typography

### How Terminal Colors Work

Terminals support three color specification tiers:

| Tier | Colors | Specification | Portability |
|------|--------|---------------|-------------|
| 4-bit (ANSI 16) | 16 named colors | `\033[31m` (red foreground) | Universal |
| 8-bit (256) | 256 indexed colors | `\033[38;5;{n}m` | Most modern terminals |
| 24-bit (True Color) | 16.7 million | `\033[38;2;{r};{g};{b}m` | Modern terminals only |

**The 16 ANSI colors are not fixed.** Each terminal emulator maps the 16 color names to different hex values. "Blue" in one terminal is a different shade than "blue" in another. This is a feature, not a bug: it allows users to customize their color scheme.

### Color Best Practices

**Use the standard 16 ANSI colors for maximum portability.** When you stick to these, Spectre.Console tells the terminal to use the color configured in the user's terminal theme. The user's chosen theme determines the actual appearance.

**Use color semantically, not decoratively.** Color should communicate meaning:

| Color | Semantic Meaning | Example |
|-------|-----------------|---------|
| Green | Success, positive | "v Operation complete" |
| Red | Error, danger | "Error: File not found" |
| Yellow | Warning, caution | "Warning: Disk space low" |
| Blue/Cyan | Informational, links, commands | "Run `app login` to authenticate" |
| Dim/Gray | Secondary, metadata | Timestamps, IDs, paths |
| Bold | Primary emphasis | Key values, headings |

**Never use color as the sole differentiator.** Always pair color with text, symbols, or position. A colorblind user should be able to understand the output without color.

### Colors to Avoid

- **Blue on dark backgrounds:** The default blue in many terminals is nearly invisible on black/dark backgrounds. This is the single most common readability complaint.
- **Bright yellow and bold yellow:** Insufficient contrast against light backgrounds (macOS default terminal uses light backgrounds).
- **Gray on Solarized Dark:** Gray matches the background color in Solarized Dark, rendering text invisible.
- **Rainbow text:** Using many colors without semantic meaning. If everything is colored, colors become meaningless.

### The NO_COLOR Standard

Applications must respect the `NO_COLOR` environment variable. When set (to any non-empty value), disable all ANSI color output.

**Color detection priority:**
1. Check `NO_COLOR` -- if set, disable all color
2. Check `FORCE_COLOR` -- if set, enable color even when not a TTY
3. Check `CLICOLOR_FORCE` -- alternative force-color standard
4. Check if stdout is a TTY -- disable color if piped/redirected
5. Check `TERM=dumb` -- disable color for dumb terminals
6. Check application-specific flags (`--no-color`, `--color`)

### Typography in the Terminal

**Information hierarchy through weight:**
- `[bold]` -- Primary information, headings, key values
- Plain text -- Standard content, descriptions
- `[dim]` -- Tertiary information, metadata, timestamps, paths

**Whitespace is your primary layout tool:**
- A well-placed blank line often communicates separation better than a Panel border
- Indentation (2-4 spaces) creates visual grouping without borders
- Consistent spacing between sections creates rhythm

**Monospace constraints:**
- All characters occupy the same width (by definition)
- Alignment is trivial with spaces
- CJK characters and some Unicode symbols occupy double width
- Tab characters render inconsistently across terminals

### Font and Character Considerations

- All terminal content renders in the user's chosen monospace font
- Box-drawing characters may not align perfectly in all fonts (Consolas shows gaps, Lucida Console does not)
- Some terminals (Alacritty, Windows Terminal) hand-draw box characters to ensure alignment
- Test with common fonts: Cascadia Code, Consolas, JetBrains Mono, Fira Code

### Anti-Patterns

- Using 256-color or 24-bit colors for critical UI elements (may not display on all terminals)
- Hardcoding specific hex colors that assume a particular background color
- Using bold-yellow for warnings (invisible on light backgrounds)
- Ignoring `NO_COLOR` environment variable
- Applying color to every piece of output ("if everything is a highlight, nothing is a highlight")
- Using color in text that will be parsed by other programs (breaks grep/awk)

### Key Sources

- [Terminal Colours Are Tricky (Julia Evans)](https://jvns.ca/blog/2024/10/01/terminal-colours/)
- [NO_COLOR Standard](https://no-color.org/)
- [FORCE_COLOR Standard](https://force-color.org/)
- [Choosing Readable ANSI Colors for CLIs](https://trentm.com/2024/09/choosing-readable-ansi-colors-for-clis.html)
- [Terminal Colors (Chris Yeh)](https://chrisyeh96.github.io/2020/03/28/terminal-colors.html)
- [CLICOLOR Standard](http://bixense.com/clicolors/)

---

## 5. Interactive Terminal Patterns

### The Wizard Pattern (Multi-Step Prompts)

**Core principle:** Lead users through decisions one at a time rather than requiring all flags upfront. Each step should depend on the previous step's output when relevant.

**Design rules for CLI wizards:**
- Restrict to 3-6 steps. More than 6 feels tedious.
- Group related inputs into a single step.
- Provide sensible defaults for every prompt. The happy path should be achievable by pressing Enter repeatedly.
- Show a summary/confirmation before executing, especially for destructive or complex operations.
- Every interactive flow must have a flag-based equivalent for automation/CI.

```csharp
// Step 1: Project name
var name = AnsiConsole.Prompt(
    new TextPrompt<string>("Project [green]name[/]:")
        .DefaultValue("my-project")
        .Validate(n => n.Length > 0
            ? ValidationResult.Success()
            : ValidationResult.Error("Name cannot be empty")));

// Step 2: Template (depends on context, not on step 1)
var template = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Choose a [green]template[/]:")
        .AddChoices("console", "webapi", "classlib"));

// Step 3: Confirmation
var panel = new Panel(
    $"[bold]Name:[/] {Markup.Escape(name)}\n[bold]Template:[/] {template}")
    .Header("[blue]Summary[/]");
AnsiConsole.Write(panel);

if (!AnsiConsole.Confirm("Create this project?"))
{
    AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
    return;
}
```

### Non-Interactive Fallback

**Always check for interactivity before prompting.**

```csharp
if (!AnsiConsole.Profile.Capabilities.Interactive)
{
    // Non-interactive: require flags
    if (string.IsNullOrEmpty(settings.Name))
    {
        AnsiConsole.MarkupLine("[red]Error:[/] --name is required in non-interactive mode.");
        return 1;
    }
}
else
{
    // Interactive: prompt for missing values
    settings.Name ??= SpectreHelpers.PromptNonEmpty("Project [green]name[/]:");
}
```

### Selection Prompt Best Practices

- **Keep lists short (7 plus or minus 2 items).** Cognitive load increases with list length. Group or paginate when there are more.
- **Enable search for long lists.** `SelectionPrompt.EnableSearch()` lets users type to filter.
- **Use meaningful display strings.** Show context, not just IDs. "Production (us-east-1)" is better than "prod-us-east-1".
- **Highlight the recommended choice.** Place it first or mark it with a visual indicator.
- **Use `PageSize` to control visible items.** Default page size of 10 works well. Show more only if all items fit.

### Input Validation

**Validate in real-time, not after submission.** Use Spectre's inline validators on TextPrompt.

```csharp
var port = AnsiConsole.Prompt(
    new TextPrompt<int>("Port [green]number[/]:")
        .DefaultValue(8080)
        .Validate(p => p switch
        {
            < 1 or > 65535 => ValidationResult.Error("[red]Port must be between 1 and 65535[/]"),
            < 1024 => ValidationResult.Error("[yellow]Ports below 1024 require elevated privileges[/]"),
            _ => ValidationResult.Success(),
        }));
```

### Typo Correction ("Did You Mean...?")

When users make obvious mistakes, suggest corrections using Levenshtein distance.

**Implementation approach:**
- Compare user input against all valid command/option names
- Calculate edit distance using the Damerau-Levenshtein algorithm
- Suggest matches within a threshold (typically distance <= 2)
- Present as a question: "Did you mean 'auth'? [y/n]"
- Do not auto-execute corrections -- users learn from seeing corrected syntax

### Confirmation Tiers for Destructive Actions

| Risk Level | Examples | Confirmation Pattern |
|------------|----------|---------------------|
| Mild | Delete a local file | Optional confirmation, `--force` to skip |
| Moderate | Delete a directory, bulk modifications | Prompt yes/no, offer `--dry-run` preview |
| Severe | Delete cloud resources, data loss | Require typing the resource name to confirm |

### Context-Awareness

- **Detect execution environment.** Adapt to current folder, config files, project structure.
- **Set smart defaults from environment.** Git branch name, project name from package.json, etc.
- **Allow per-project overrides.** Local .env or project config overrides global settings.
- **Prefer project binaries over global ones.** Like npm's `npx` behavior.

### Anti-Patterns

- Prompting for inputs that could be inferred from context
- Requiring interactive prompts for operations that will run in CI
- Multi-step flows with no way to go back and change a previous answer
- Prompting for optional values without offering a "skip" option
- Using multi-select for single-choice scenarios (or vice versa)
- Not validating input until form submission (validate each field inline)

### Key Sources

- [UX Patterns for CLI Tools (Lucas F. Costa)](https://www.lucasfcosta.com/blog/ux-patterns-cli-tools)
- [Command Line Interface Guidelines (clig.dev)](https://clig.dev/)
- [Wizard UI Pattern (Eleken)](https://www.eleken.co/blog-posts/wizard-ui-pattern-explained)
- [Wizards: Definition and Design Recommendations (NN/g)](https://www.nngroup.com/articles/wizards/)

---

## 6. Error Handling UX in CLIs

### The Three-Part Error Message

Every error message should answer three questions:

1. **What happened?** A clear, non-technical description of the failure.
2. **Why did it happen?** Context about the cause (missing file, network issue, permission denied).
3. **What can the user do about it?** Concrete, actionable steps to resolve the issue.

```
Error: Could not connect to the API.
  Endpoint: https://api.example.com/v1

Try:
  - Check your network connection
  - Verify the API key with `app login --check`
  - Run with `--verbose` for detailed logs
```

### Error Presentation Patterns

**Visual hierarchy for errors:**
```csharp
// Primary error message (red, bold)
AnsiConsole.MarkupLine("[red bold]Error:[/] Could not connect to the API.");

// Context (dim)
AnsiConsole.MarkupLine("[dim]Endpoint:[/] https://api.example.com/v1");

// Blank line separator
AnsiConsole.WriteLine();

// Actionable suggestions (plain text with highlighted commands)
AnsiConsole.MarkupLine("Try:");
AnsiConsole.MarkupLine("  - Check your network connection");
AnsiConsole.MarkupLine("  - Verify the API key with [blue]app login --check[/]");
AnsiConsole.MarkupLine("  - Run with [blue]--verbose[/] for detailed logs");
```

### Error Classification

**Distinguish between error sources:**

| Source | User Message | Technical Detail |
|--------|-------------|-----------------|
| User input error | "Invalid port number: 99999" | Validate inline, suggest correct range |
| Configuration error | "Missing API key in config" | Show config file path, suggest fix command |
| Network error | "Could not reach api.example.com" | Suggest checking connection, proxy settings |
| Authentication error | "Authentication failed" | Suggest re-login command |
| Internal/unexpected error | "An unexpected error occurred" | Include error code, log file path, bug report URL |
| External service error | "GitHub API returned 503" | Distinguish from tool bugs; suggest retry |

### Error Codes for Searchability

- Assign structured error codes (e.g., "ERR-AUTH-001", "ERR-NET-002")
- Make codes unique and searchable on the web
- Link to documentation pages for each code
- Include the code in the error message: "Error ERR-AUTH-001: Authentication token expired"

### Signal-to-Noise Ratio

- **Group similar errors.** If 50 files fail the same validation, show the count and pattern rather than 50 identical messages.
- **Place important info at the end.** The last line is where eyes naturally rest after an error.
- **Use red text sparingly.** Only the "Error:" prefix and critical information. If everything is red, nothing is red.
- **No stack traces by default.** Reserve stack traces for `--verbose` or `--debug` mode. Write them to a log file instead.

### Assisted Recovery

- **Suggest similar commands for typos.** Use Levenshtein distance to find close matches.
- **Offer to fix the issue.** "Would you like to create the missing directory? [y/n]"
- **Show the exact command to retry.** After fixing the root cause, tell users what to run next.
- **Provide log file paths.** "See detailed logs at ~/.myapp/logs/2024-01-15.log"
- **Include bug report URLs.** Pre-populate issue templates with error details.

### Validation Patterns

**Fail fast:** Check input validity at the earliest possible point, before any work begins.

**Progressive validation:** Validate each prompt input inline before moving to the next step.

**Batch validation summary:** When validating a file or complex input, show all errors at once rather than one at a time:

```
Found 3 issues in config.yaml:
  Line 5:  Invalid port number '99999' (must be 1-65535)
  Line 12: Unknown provider 'gogle' (did you mean 'google'?)
  Line 18: Missing required field 'api_key'
```

### Anti-Patterns

- Generic "An error occurred" with no context
- Stack traces as the primary error output
- Swallowing errors silently (returning 0 when there was a failure)
- Printing the same error message 100 times for 100 failures
- Error messages that blame the user ("You did it wrong")
- Errors that require reading source code to understand
- Missing exit codes (always returning 0)

### Key Sources

- [Command Line Interface Guidelines (clig.dev) -- Errors section](https://clig.dev/)
- [UX Patterns for CLI Tools -- Human-Understandable Errors](https://www.lucasfcosta.com/blog/ux-patterns-cli-tools)
- [Error Handling in CLI Tools (Medium)](https://medium.com/@czhoudev/error-handling-in-cli-tools-a-practical-pattern-thats-worked-for-me-6c658a9141a9)
- [Creating User-Friendly Error Messages (LeadDev)](https://leaddev.com/software-quality/creating-user-friendly-error-messages)

---

## 7. Progressive Disclosure in CLIs

### Core Concept

Progressive disclosure reveals information and complexity only when the user needs it, minimizing cognitive load at each step. In terminal apps, this manifests at multiple levels.

### Three Levels of Information Disclosure

**Level 1: Summary (always visible)**
- Brief status messages, command confirmations
- The "headline" of what happened
- Example: "v 3 files deployed successfully"

**Level 2: Details (on request)**
- Expanded output via `--verbose` flag
- Help text via `--help`
- Individual item details via subcommands (e.g., `app show <id>`)
- Example: Adding `--verbose` shows each file name and deployment URL

**Level 3: Deep Diagnostic (targeted request)**
- Debug output via `--debug`
- Log files written to disk
- API request/response traces
- Stack traces and internal state

### Applying Progressive Disclosure to Help

**Tiered help:**
1. **No arguments:** Brief usage hint and example. "Run `app --help` for more."
2. **--help:** Full command listing with descriptions. Common commands first.
3. **subcommand --help:** Detailed options, examples, and links to documentation.
4. **Web documentation:** Comprehensive guides, tutorials, reference pages.

**Example: Git's tiered help:**
```bash
$ git                    # Brief usage + common commands
$ git --help             # Full command listing
$ git commit --help      # Detailed commit options
$ git help -a            # All available commands
```

### Applying Progressive Disclosure to Output

**Default output is concise:**
```
v Deployed 3 services to production
```

**Verbose output adds detail:**
```
v Deployed 3 services to production
  - api-server    -> https://api.example.com     (v2.1.0)
  - web-frontend  -> https://www.example.com     (v3.0.1)
  - worker        -> internal                     (v1.5.2)
```

**Debug output adds internals:**
```
[DEBUG] POST https://deploy.example.com/v1/deploy
[DEBUG] Request body: {"services": [...]}
[DEBUG] Response: 200 OK (342ms)
v Deployed 3 services to production
  ...
```

### Information Hierarchy Through Visual Weight

The terminal equivalent of font-size hierarchy:

```
[bold]PRIMARY INFORMATION[/]           <- Bold, possibly colored
Normal supporting details              <- Plain text
[dim]Metadata: timestamps, IDs[/]      <- Dim text

────────────────────────────           <- Rule separator

[bold]NEXT SECTION[/]
...
```

### Applying Progressive Disclosure to Errors

- **First:** Short error message with the fix ("Error: Missing API key. Run `app login` to authenticate.")
- **Then:** Error code for searching ("Error ERR-AUTH-001")
- **Then:** Log file path for debugging ("See ~/.app/logs/error.log for details")
- **Finally:** Bug report link if the error is unexpected

### Anti-Patterns

- Dumping all information at once regardless of context
- Requiring `--verbose` to see any useful output at all
- Hiding critical error information behind flags
- Showing debug-level output by default
- No path from summary to detail (no `--verbose`, no help subcommand)

### Key Sources

- [Command Line Interface Guidelines (clig.dev)](https://clig.dev/)
- [Progressive Disclosure Guidelines (DeepWiki)](https://deepwiki.com/spences10/claude-skills-cli/5.3-progressive-disclosure-guidelines)
- [Progressive Disclosure Controls (Microsoft Learn)](https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-progressive-disclosure-controls)

---

## 8. Terminal Animation & Feedback

### The Feedback Imperative

Every user action should produce visible feedback within 100ms. A program that responds immediately feels fast even if the actual operation takes seconds. Never leave the user staring at a blank cursor.

### Three Categories of Progress Indicators

#### 1. Spinners (Indeterminate Progress)

Use when you cannot measure completion percentage.

**Characteristics:**
- Animated character sequences (braille dots, bouncing bar, dots)
- Accompanied by a status message describing current activity
- Should update the status message as phases change

**When to use:**
- Network requests with unknown response time
- Authentication flows
- Operations under ~10 seconds

**When NOT to use:**
- Sub-second operations (adds flicker, not value)
- Operations where progress is measurable (use a progress bar instead)

**Common spinner patterns:**
- Braille characters: most popular choice, smooth animation
- Dots: `...` cycling, simple and universal
- Line: `-\|/` rotation, classic UNIX
- Bouncing: `[=   ]` bar moving back and forth

#### 2. X-of-Y Pattern (Counting Progress)

Use when you know the total count but not necessarily the time per item.

```
Processing files... (3/10)
Processing files... (4/10)
```

**When to use:**
- Batch processing with known total count
- Multi-step operations with countable steps

#### 3. Progress Bars (Determinate Progress)

Use when you can calculate completion percentage.

```
Downloading  [=========>          ]  45%  12.3 MB/s  ETA: 00:02:15
```

**Characteristics:**
- Visual bar showing proportion complete
- Percentage display
- Speed/throughput when applicable
- Estimated time remaining (ETA)

**When to use:**
- File downloads/uploads
- Large data processing
- Build pipelines

### Feedback Timing Guidelines

| Duration | Feedback Type | Example |
|----------|--------------|---------|
| < 200ms | None needed | Reading a small config file |
| 200ms - 1s | Brief spinner (optional) | Network round-trip |
| 1s - 10s | Spinner with status message | API authentication |
| 10s - 60s | Progress bar or step counter | File download |
| > 60s | Progress bar with ETA | Large data migration |

### Post-Completion Feedback

**Clear the spinner/progress, show the result:**

Good pattern:
```
v Downloaded 3 packages (14.2 MB) in 8.3s
v Built project successfully
v Tests passed (42 tests, 0 failures)
```

Bad pattern:
```
(spinner just disappears, no confirmation of what happened)
```

**Use checkmarks for completed steps:**
```
v Connecting to server
v Authenticating
v Downloading configuration
  Applying changes...
```

### Parallel Progress

When multiple operations run concurrently, use multi-task progress displays:

```csharp
await AnsiConsole.Progress()
    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(),
             new PercentageColumn(), new RemainingTimeColumn())
    .StartAsync(async ctx =>
    {
        var task1 = ctx.AddTask("Downloading frontend");
        var task2 = ctx.AddTask("Downloading backend");
        var task3 = ctx.AddTask("Downloading assets");
        // Update each independently
    });
```

**Rules for parallel progress:**
- Show one line per concurrent task
- Make sure output does not interleave confusingly
- When errors occur, surface error details that may have scrolled past

### Anti-Patterns

- Progress bars for sub-second operations (adds flicker, not value)
- Spinners that run indefinitely with no timeout
- Progress bars that jump backward (indicates incorrect tracking)
- Clearing the progress bar without showing the final result
- "100% complete" followed by more work (dishonest progress)
- Animated output when stdout is redirected/piped
- Multiple simultaneous live-rendering widgets (causes corruption in Spectre.Console)

### Key Sources

- [CLI UX Best Practices: 3 Patterns for Improving Progress Displays (Evil Martians)](https://evilmartians.com/chronicles/cli-ux-best-practices-3-patterns-for-improving-progress-displays)
- [Spectre.Console Progress](https://spectreconsole.net/live/progress)
- [Spectre.Console Status](https://spectreconsole.net/live/status)
- [Spectre.Console Spinners Reference](https://spectreconsole.net/appendix/spinners)
- [How to Write Better Bash Spinners](https://willcarh.art/blog/how-to-write-better-bash-spinners)
- [cli-spinners (GitHub)](https://github.com/sindresorhus/cli-spinners)

---

## 9. ASCII Art & Box Drawing

### Box-Drawing Characters

Unicode includes 128 box-drawing characters in the Box Drawing block (U+2500 to U+257F). These are designed to connect horizontally and vertically with adjacent characters, requiring proper alignment in monospaced fonts.

**Common box-drawing characters:**

| Purpose | Light | Heavy | Double |
|---------|-------|-------|--------|
| Horizontal | `─` (U+2500) | `━` (U+2501) | `═` (U+2550) |
| Vertical | `│` (U+2502) | `┃` (U+2503) | `║` (U+2551) |
| Top-left corner | `┌` (U+250C) | `┏` (U+250F) | `╔` (U+2554) |
| Top-right corner | `┐` (U+2510) | `┓` (U+2513) | `╗` (U+2557) |
| Bottom-left corner | `└` (U+2514) | `┗` (U+2517) | `╚` (U+255A) |
| Bottom-right corner | `┘` (U+2518) | `┛` (U+251B) | `╝` (U+255D) |
| T-junction (down) | `┬` (U+252C) | `┳` (U+2533) | `╦` (U+2566) |
| T-junction (up) | `┴` (U+2534) | `┻` (U+253B) | `╩` (U+2569) |
| T-junction (right) | `├` (U+251C) | `┣` (U+2523) | `╠` (U+2560) |
| T-junction (left) | `┤` (U+2524) | `┫` (U+252B) | `╣` (U+2563) |
| Cross | `┼` (U+253C) | `╋` (U+254B) | `╬` (U+256C) |

### Cross-Platform Compatibility

**Safe characters that work on most terminals** (tested across 15+ terminals on Ubuntu, macOS, and Windows):
- All standard Latin, Greek, Cyrillic alphabets
- Standard punctuation and brackets
- Light box-drawing characters (`─`, `│`, `┌`, `└`, `┐`, `┘`, `├`, `┤`, `┬`, `┴`, `┼`)
- Common symbols: bullets (`-`), arrows (`<-`, `->`), checkmarks
- Currency symbols: $, EUR, GBP, JPY

**Potentially problematic:**
- Heavy and double box-drawing characters (may not render in all fonts)
- Block elements (partial blocks, shading characters)
- Emoji (support varies dramatically across terminals and OS versions)
- Powerline symbols (require specific patched fonts)

**Windows-specific concerns:**
- Legacy cmd.exe and conhost may use CP437/CP850 encodings
- Windows Terminal (modern) fully supports Unicode
- Consolas font shows gaps between box-drawing lines; Cascadia Code handles them correctly

### Spectre.Console Border Styles

Spectre.Console provides multiple border styles that automatically handle box-drawing:

```csharp
// Table borders
table.Border(TableBorder.Rounded);   // Rounded corners (modern)
table.Border(TableBorder.Simple);    // Simple horizontal lines
table.Border(TableBorder.Ascii);     // ASCII only (maximum compat)
table.Border(TableBorder.Heavy);     // Heavy lines
table.Border(TableBorder.Double);    // Double lines
table.Border(TableBorder.None);      // No borders

// Panel borders
panel.Border(BoxBorder.Rounded);
panel.Border(BoxBorder.Ascii);
panel.Border(BoxBorder.Heavy);
panel.Border(BoxBorder.None);
```

When Unicode is not supported (detected via `AnsiConsole.Profile.Capabilities.Unicode`), Spectre.Console automatically falls back from Unicode box-drawing to ASCII equivalents (`-`, `|`, `+`).

### FigletText (ASCII Art Banners)

**When to use:** Startup banners, celebration/completion moments, version display.
**When NOT to use:** Help output, frequently repeated output, within live displays.

```csharp
AnsiConsole.Write(
    new FigletText("Welcome")
        .Color(Color.Green)
        .Centered());
```

### Layout Guidance

**Use whitespace over borders:**
- A blank line between sections often communicates separation better than a Panel border
- Indentation (2-4 spaces) creates visual grouping without box-drawing overhead
- Not every piece of information needs to be in a box

**Keep nesting shallow:**
- Panels inside Panels inside Tables creates visual noise
- Prefer flat composition: multiple adjacent widgets rather than nested containers
- Use indentation and color for hierarchy instead of nested borders

### Anti-Patterns

- Borders around everything (visual noise, reduces information density)
- Deep nesting of bordered containers
- ASCII art logos in help output or frequently displayed content
- Assuming all terminals support the same Unicode characters
- Using box-drawing for simple key-value output (use a Grid instead)
- Emoji as word replacements (breaks greppability)

### Key Sources

- [Box-Drawing Characters (Wikipedia)](https://en.wikipedia.org/wiki/Box-drawing_characters)
- [Cross-Platform Terminal Characters (GitHub)](https://github.com/ehmicky/cross-platform-terminal-characters)
- [Cross-Platform Terminal Characters (DEV Community)](https://dev.to/ehmicky/cross-platform-terminal-characters-2gfm)
- [Box Drawing Characters: Building Text-Based UI with Unicode](https://symbolfyi.com/guides/box-drawing-characters/)
- [A New Way of Drawing Boxes in the Terminal](https://www.willmcgugan.com/blog/tech/post/ceo-just-wants-to-draw-boxes/)

---

## 10. Accessibility in Terminal Apps

### The Accessibility Gap

Unlike web applications with WCAG, there is no equivalent comprehensive standard for terminal and CLI accessibility. Teams must innovate based on W3C's high-level WCAG2ICT guidance rather than prescriptive techniques. This makes accessibility in terminals an area requiring intentional design effort.

### Screen Reader Challenges

**Core issues:**
- Screen readers vocalize terminal output character-by-character
- Unstructured text output results in irrelevant or repetitive vocalization
- Non-alphanumeric visual cues and constant screen redraws confuse screen readers
- Navigation tasks like scrolling are difficult with screen readers
- Tabular data lacks structural markup, forcing manual column/row tracking
- Animated spinners (braille characters, progress bars) generate noise

**Screen readers used with terminals:**
- NVDA (Windows, most common)
- JAWS (Windows)
- VoiceOver (macOS)
- Orca (Linux, graphical environments)
- Fenrir (Linux, command line specific)
- Emacspeak (Linux/macOS, command line specific)

### Design Recommendations

#### 1. Provide a "Bare Mode" or "Accessible Mode"

Screen reader users strongly prefer output free of decorative characters, color, or animation.

```csharp
// Check for accessibility preference
var accessible = Environment.GetEnvironmentVariable("APP_ACCESSIBLE") is not null
    || !AnsiConsole.Profile.Capabilities.Interactive;

if (accessible)
{
    // Plain text output, no spinners, no color
    Console.WriteLine("Status: Processing 3 of 10 files");
}
else
{
    // Rich output with spinners and color
    AnsiConsole.Status().Start("Processing...", ctx => { /* ... */ });
}
```

#### 2. Replace Animated Spinners with Static Messages

Instead of braille character animations that redraw constantly, use contextual text messages that remain stable for assistive technology to read.

```csharp
// Accessible: static status message
Console.Error.WriteLine("Working...");

// Inaccessible: animated spinner that redraws every 80ms
AnsiConsole.Status().Spinner(Spinner.Known.Dots).Start("Working...", _ => { });
```

GitHub CLI (v2.72.0+) implements this via `gh a11y` to enable status trackers instead of Unicode spinners and percentage-based progress bars.

#### 3. Make Tables Exportable

Tables in CLI output lack structural markup. Screen reader users must manually track columns and rows.

**Solutions:**
- Provide `--json` output that can be consumed by other tools
- Offer CSV export functionality for tabular data
- Use consistently formatted key-value pairs instead of tables for small data sets

#### 4. Use ANSI 4-bit Colors Aligned to Terminal Theme

Let the user's terminal theme control color appearance. Using standard ANSI 4-bit colors means all color preferences can be set in the terminal emulator's theme, which is where screen reader users customize their experience.

#### 5. Ensure Text Alternatives for All Visual Information

- Do not rely solely on color to convey meaning (pair with text: "Error:", "Warning:", "v")
- Do not use decorative characters that add noise (box borders, ASCII art)
- Provide text descriptions for any visual patterns (charts, progress bars)

#### 6. Support NO_COLOR and Plain Output

Respect the `NO_COLOR` environment variable. Additionally provide `--plain` or `--no-color` flags. When output is piped (stdout is not a TTY), automatically strip all ANSI escape sequences.

### Colorblind-Safe Design

**Avoid problematic color combinations:**
- Red/green (most common colorblind pattern -- affects ~8% of males)
- Blue on black (low contrast in many terminal themes)
- Yellow on white (insufficient contrast)

**Pair color with other indicators:**

| Status | Color Only (BAD) | Color + Symbol (GOOD) |
|--------|-----------------|----------------------|
| Success | Green text | `[green]v[/] Success` |
| Error | Red text | `[red]Error:[/] message` |
| Warning | Yellow text | `[yellow]Warning:[/] message` |
| Info | Blue text | `[blue]>[/] message` |

**Test with common colorblind simulators** to ensure text remains distinguishable.

### Keyboard Navigation

- **Document all keyboard shortcuts.** Display them in a help bar or via a help command.
- **Use standard conventions.** Ctrl+C to cancel, Enter to confirm, Escape to go back, arrow keys for navigation.
- **Do not rely on mouse input.** Some assistive technology users cannot use a mouse.
- **Support tab navigation** where multiple interactive elements exist.

### Documentation Accessibility

Screen reader users rely on HTML/web versions of documentation rather than in-terminal help or man pages. Ensure:
- All documentation has web-accessible HTML versions
- Web docs follow WCAG guidelines
- In-terminal help is well-structured with clear headings and sections

### Anti-Patterns

- Assuming all users can see colors
- Using emoji as the sole indicator of status
- Animated UI elements with no static alternative
- ASCII art as critical information (invisible/noise to screen readers)
- Tables with no machine-readable alternative (--json, --csv)
- Help text only available via man pages (not accessible to all screen readers)
- Constant screen redraws during spinner/progress animations
- Using decorative Unicode characters (box borders, fancy bullets) without a plain mode

### Key Sources

- [Accessibility of Command Line Interfaces (ACM)](https://dl.acm.org/doi/fullHtml/10.1145/3411764.3445544)
- [Building a More Accessible GitHub CLI](https://github.blog/engineering/user-experience/building-a-more-accessible-github-cli/)
- [Two Ways to Make Your CLI More Accessible](https://dev.to/baspin94/two-ways-to-make-your-command-line-interfaces-more-accessible-541k)
- [The State of Linux Command Line Accessibility](https://blindcomputing.org/linux/state-of-cli-accessibility/)
- [Google Cloud CLI Accessibility Features](https://cloud.google.com/sdk/docs/enabling-accessibility-features)
- [NO_COLOR Standard](https://no-color.org/)

---

## 11. Layout + Live Display: Dashboard TUI Pattern

### Overview

Spectre.Console's `Layout` widget combined with `AnsiConsole.Live()` enables a dashboard-style TUI where named regions of the terminal update independently and in-place. This is the correct architecture for interactive applications like chat assistants, monitoring dashboards, and any tool that needs persistent fixed regions alongside scrolling content.

### Layout Widget

The `Layout` widget divides the terminal into named, nested regions:

```csharp
var layout = new Layout("Root")
    .SplitRows(
        new Layout("Content"),    // Takes remaining space
        new Layout("StatusBar")); // Fixed at bottom

layout["Content"].Ratio(1);       // Fills available space
layout["StatusBar"].Size(1);      // Exactly 1 row
```

Key properties:
- **`Ratio(int)`** -- Proportional sizing (like CSS flex-grow)
- **`Size(int)`** -- Fixed number of rows
- **`MinimumSize(int)`** -- Floor for ratio-based regions
- **`IsVisible`** -- Toggle region visibility without removing it
- **`Update(IRenderable)`** -- Replace region content with any renderable
- **`SplitRows()`** / **`SplitColumns()`** -- Nest horizontal or vertical divisions

Layouts can nest arbitrarily: a row can contain columns, which contain rows, etc.

### Live Display Integration

Wrap a Layout in `AnsiConsole.Live()` to enable in-place updates:

```csharp
var layout = BuildLayout();
await AnsiConsole.Live(layout)
    .AutoClear(false)
    .Overflow(VerticalOverflow.Ellipsis)
    .StartAsync(async ctx =>
    {
        while (running)
        {
            layout["Content"].Update(BuildConversationView());
            layout["StatusBar"].Update(BuildStatusBar());
            ctx.Refresh();
            await Task.Delay(16); // ~60fps cap
        }
    });
```

### Critical Constraint: Thread Safety

**Live display is NOT thread safe.** The following cannot be used inside a Live context:
- `AnsiConsole.Prompt()` (TextPrompt, SelectionPrompt, etc.)
- `AnsiConsole.Status()`
- `AnsiConsole.Progress()`
- Any other interactive or live-rendering widget

This means interactive prompts (slash commands that need SelectionPrompt, confirmation dialogs) must **exit the Live context**, render their UI with full terminal control, then **re-enter the Live context**. The Suspend/Resume pattern is architecturally correct for this.

### Modal Overlay Pattern

For TUI applications that need modal dialogs (configuration screens, detail views, help panels) without blocking the main loop:

1. The Layout has a hidden "Modal" region that covers the content area
2. When a modal is requested, `layout["Modal"].IsVisible = true` and the modal content is rendered
3. The main content region is either hidden or rendered underneath (dimmed)
4. When dismissed, `layout["Modal"].IsVisible = false` restores the main view
5. The underlying state (conversation, streaming) continues unaffected

This is similar to how lazygit and k9s handle popup dialogs over their main views.

```csharp
// Conceptual structure
var layout = new Layout("Root")
    .SplitRows(
        new Layout("Main")
            .SplitRows(
                new Layout("Content"),
                new Layout("StatusBar").Size(1)),
        new Layout("Modal"));  // Hidden by default

layout["Modal"].IsVisible = false;

// To show modal:
layout["Modal"].IsVisible = true;
layout["Modal"].Update(BuildModalPanel(slashCommandOutput));
ctx.Refresh();

// To dismiss:
layout["Modal"].IsVisible = false;
ctx.Refresh();
```

**Note**: Because Live is not thread-safe, modal content that requires interactive prompts (SelectionPrompt, TextPrompt) still needs the Suspend/Resume pattern. Read-only modals (help, show, list) can render entirely within the Live context.

### Suspend/Resume for Interactive Prompts

When a slash command needs interactive prompts:

1. **Suspend**: Stop the Live display (let it complete its `StartAsync`)
2. **Render prompts**: Spectre.Console has full terminal control
3. **Resume**: Re-enter `AnsiConsole.Live()` with updated state

The key insight is that "suspend" does not mean raw ANSI escape sequences. It means structuring the code so the Live context can be exited and re-entered cleanly.

### Streaming Content in Layout Regions

For LLM token streaming within a Layout region:

1. Accumulate tokens in a `StringBuilder`
2. On each token, rebuild the renderable (e.g., `new Markup(Markup.Escape(buffer.ToString()))`)
3. Call `layout["Content"].Update(renderable)` then `ctx.Refresh()`
4. Cap refresh rate (~60fps) to avoid excessive redraws

For long conversations, the content region should show only the tail of the conversation (most recent messages) since Layout regions do not scroll. The full history lives in the data model and can be viewed via `/context show` or similar commands.

### Sources

- [Spectre.Console Layout Documentation](https://spectreconsole.net/widgets/layout)
- [Spectre.Console Live Display Documentation](https://spectreconsole.net/live/live-display)
- [lazygit Modal Dialog Pattern](https://github.com/jesseduffield/lazygit)
- [k9s Dashboard Layout](https://k9scli.io/)

---

## 12. Streaming Markdown Rendering in Terminals

### The Block Boundary Insight

Markdown documents can be divided into top-level blocks: paragraphs, headers, code fences, lists, blockquotes. During streaming, only the **last** block can change when new content arrives. All previously completed blocks are finalized and can be cached.

This enables an efficient rendering strategy:
1. Parse incoming tokens into blocks
2. Render finalized blocks once and cache the output
3. Only re-render the in-progress block on each new token
4. When a block boundary is detected (e.g., double newline, code fence close), finalize and cache

### Rendering Strategy for Chat TUIs

For a Spectre.Console-based chat TUI:

- **Paragraphs**: Render as `Markup` or `Text` with word wrapping
- **Code blocks**: Render as `Panel` with syntax-highlighted content (language-specific if detectable)
- **Headers**: Render as `Rule` with left-justified bold text
- **Lists**: Render as indented `Markup` lines with bullet characters
- **Inline code**: Render with `[bold]` or backtick-styled markup

During streaming, the in-progress block renders as plain text. When finalized, it gets its full styled treatment.

### Performance Considerations

- Cache finalized block renderables to avoid re-parsing on every refresh
- Rate-limit display updates (~60fps max, 16ms minimum between refreshes)
- For very long responses, show only the tail portion in the live display
- The full rendered conversation is available in scrollback after the Live context exits

### Sources

- [Efficient Streaming of Markdown in the Terminal (Will McGugan)](https://willmcgugan.github.io/streaming-markdown/)
- [Streamdown: Streaming Markdown Renderer for TUI CLIs](https://github.com/day50-dev/Streamdown)
- [md-tui: Markdown Renderer in the Terminal](https://github.com/henriklovhaug/md-tui)
- [OpenCode TUI Markdown Rendering](https://opencode.ai/docs/tui/)

---

## 13. Modern TUI Exemplars and Patterns

### lazygit

**Layout**: Multi-pane with 5 named panels (Status, Files, Branches, Commits, Stash). Panels resize dynamically. Focus switches between panels via Tab or number keys.

**Modal dialogs**: Confirmation prompts, commit message editors, and search dialogs appear as centered overlays on top of the main view. The underlying panels remain visible but dimmed. Dismissing the modal restores focus to the previous panel.

**Key patterns**: Vim-style navigation (j/k/h/l), contextual keybinding hints at the bottom, color-coded status indicators, inline diffs with syntax highlighting.

### k9s

**Layout**: Header bar (cluster info), main content area (resource list), footer (keybindings). The content area switches between views (pods, services, deployments) based on commands typed in a command palette.

**Navigation**: Command palette (`:` to open), fuzzy search, namespace filtering, contextual actions per resource type. Real-time updates every 2 seconds.

**Key patterns**: Breadcrumb navigation showing current context, color-coded resource status, log streaming with follow mode, YAML/describe views as full-screen overlays.

### btop

**Layout**: Multi-panel dashboard with CPU, memory, network, and process panels. Each panel updates independently at different rates. Responsive layout adjusts panel sizes and visibility based on terminal dimensions.

**Key patterns**: Sparklines and mini-graphs for metrics, color gradients for utilization, process tree with sorting, configurable themes.

### Claude Code

**Layout**: Scrollback-based chat (not fixed regions). User input at bottom with readline-style editing. Streaming responses render inline. Tool calls appear as collapsible sections with execution output.

**Key patterns**: Markdown rendering with syntax-highlighted code blocks, file diffs with +/- notation, thinking indicators (spinner), permission prompts for tool execution, context usage bar.

### Common Patterns Across Modern TUIs

1. **Keyboard-first**: Every action reachable via keyboard. Mouse is optional enhancement.
2. **Contextual keybinding hints**: Bottom bar or header shows available keys for current state.
3. **Color as reinforcement**: Color supplements text/symbols, never used alone.
4. **Responsive regions**: Layout adapts to terminal size -- panels hide, stack, or compress.
5. **Non-blocking modals**: Overlays for configuration/details that don't interrupt background work.
6. **Vim navigation**: j/k for up/down is expected in developer-facing TUIs.
7. **Command palette**: Colon or slash prefix for commands (`:pods`, `/help`).

### Sources

- [lazygit GitHub Repository](https://github.com/jesseduffield/lazygit)
- [K9s Documentation](https://k9scli.io/)
- [btop++ GitHub Repository](https://github.com/aristocratos/btop)
- [awesome-tuis: Curated List of TUI Projects](https://github.com/rothgar/awesome-tuis)
- [Beyond the GUI: Modern TUI Guide](https://www.blog.brightcoding.dev/2025/09/07/beyond-the-gui-the-ultimate-guide-to-modern-terminal-user-interface-applications-and-development-libraries/)

---

## Appendix A: Quick Reference -- Decision Matrix

### What Widget to Use

| Scenario | Widget | Notes |
|----------|--------|-------|
| List of items with multiple fields | Table | Use `TableBorder.Simple` for clean look |
| Single item with properties | Grid or key-value Markup lines | Panel only if grouping needed |
| Hierarchical data | Tree | File structures, dependency graphs |
| Section separation | Rule | Left-justified with dim style |
| Side-by-side comparison | Columns or Layout | Columns auto-flows; Layout for fixed regions |
| Dashboard with regions | Layout | Split into named sections |
| Important announcement | Panel with header | Rounded border, brief content |
| Startup banner | FigletText | One-time use; never in help |
| Success/error/warning | Markup lines (or SpectreHelpers) | Inline output, not panels |

### What Feedback to Show

| Duration | Feedback | Implementation |
|----------|----------|---------------|
| < 200ms | None | Just do it |
| 200ms - 1s | Optional spinner | `AnsiConsole.Status()` |
| 1s - 10s | Spinner with message | `AnsiConsole.Status()` with context updates |
| 10s+ (known total) | Progress bar | `AnsiConsole.Progress()` with tasks |
| 10s+ (unknown total) | Spinner or indeterminate progress | `AnsiConsole.Status()` or indeterminate task |

### Color Semantic Map

| Meaning | Color | Spectre Markup | Symbol |
|---------|-------|---------------|--------|
| Success | Green | `[green]` | `v` |
| Error | Red | `[red]` | `x` or `Error:` |
| Warning | Yellow | `[yellow]` | `!` or `Warning:` |
| Info/Command | Blue/Cyan | `[blue]` or `[cyan]` | `>` |
| Secondary/Metadata | Dim | `[dim]` | None |
| Primary emphasis | Bold | `[bold]` | None |
| User data (needs escaping) | Cyan | `[cyan]` | `Markup.Escape()` |

---

## Appendix B: Quick Reference -- Spectre.Console Escaping Rules

### When to Escape

| Context | Who Escapes | Method |
|---------|-------------|--------|
| SpectreHelpers status methods (Success, Error, etc.) | Helper escapes internally | Pass plain text, never pre-escape |
| SpectreHelpers prompt labels | Caller escapes interpolated user data | `$"Name for [green]{Markup.Escape(input)}[/]:"` |
| Raw `AnsiConsole.MarkupLine` with user data | Caller escapes | `Markup.Escape(data)` in format string |
| `AnsiConsole.MarkupInterpolated` | Auto-escaped | Just use interpolation: `$"[blue]{data}[/]"` |
| `Markup.FromInterpolated` for IRenderable | Auto-escaped | For Table cells, Tree nodes, etc. |
| Developer-authored literal strings | No escaping needed | `"[bold]My Label[/]"` is fine |

### Common Mistakes

| Mistake | Result | Fix |
|---------|--------|-----|
| Double-escaping (escaping then passing to SpectreHelpers) | Literal `[green]` text in output | Pass plain text to helpers |
| Not escaping user input in MarkupLine | Runtime crash on `[` or `]` in input | Use `Markup.Escape()` or `MarkupInterpolated` |
| Escaping prompt labels entirely | Markup tags rendered as literal text | Only escape interpolated user data portions |

---

## Appendix C: Checklist for Terminal UX Review

### Before Release

- [ ] Does every action produce visible feedback within 100ms?
- [ ] Do long operations (> 1s) show a spinner or progress bar?
- [ ] Do error messages explain what, why, and what to do?
- [ ] Does the tool work when stdout is piped (non-TTY)?
- [ ] Is `NO_COLOR` respected?
- [ ] Does `--help` work at every command level?
- [ ] Are all interactive prompts bypassable with flags?
- [ ] Is user input escaped before rendering as markup?
- [ ] Does the layout work at 80 columns?
- [ ] Are exit codes non-zero on failure?
- [ ] Is Ctrl+C handled gracefully (cleanup + cancellation message)?
- [ ] Does success produce an explicit confirmation message?
- [ ] Are destructive operations confirmed before execution?
- [ ] Is output sent to the correct stream (stdout vs stderr)?
- [ ] Are tables empty-state handled ("No items found" vs empty table)?
- [ ] Is color used semantically, never as the sole differentiator?
- [ ] Are there no multiple simultaneous live-rendering widgets?
