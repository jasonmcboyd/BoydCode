# Screen: Crash Screen

## Overview

The crash screen is the last-resort error display when an unhandled exception
escapes all other error handling. It has two rendering layers: a Spectre.Console
Panel with a red border and structured error information, and a plain-text
fallback that writes directly to stderr if the Spectre rendering itself fails.

The crash screen is designed to survive any failure condition, including
corrupted console state, missing Spectre dependencies, or broken ANSI
terminal support.

**Screen IDs**: SYS-01, SYS-02

## Trigger

Three exception sources can trigger the crash screen:

1. **Top-level try/catch** in `Program.cs` -- catches any exception that
   escapes the entire `CommandApp.RunAsync()` pipeline.
2. **AppDomain.UnhandledException** handler -- catches exceptions from
   background threads, finalizers, and other non-task contexts.
3. **TaskScheduler.UnobservedTaskException** handler -- catches unobserved
   task exceptions (logged but not displayed; the handler calls
   `SetObserved()` to prevent process termination).

In all cases, `CrashLogger.LogException(ex)` writes the full exception
details to disk before the visual crash screen renders.

## Layout (80 columns) -- Spectre Panel

```
(blank line)
┌ boydcode crash ──────────────────────────────────────────────────────────────┐
│                                                                              │
│  An unexpected error occurred.                                               │
│                                                                              │
│  Object reference not set to an instance of an object.                       │
│                                                                              │
│  Details have been written to:                                               │
│  C:\Users\jason\.boydcode\logs\error.log                                     │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
(blank line)
```

### Anatomy

1. **Blank line** -- `AnsiConsole.WriteLine()` before the panel.
2. **Panel** -- Spectre.Console `Panel` with:
   - **Header**: `[red bold] boydcode crash [/]` (with leading/trailing
     spaces for visual padding within the header).
   - **Border color**: `Color.Red`.
   - **Padding**: `Padding(1, 1, 1, 1)` -- 1 character on all sides.
   - **Content** (Markup):
     - Line 1: `[red bold]An unexpected error occurred.[/]`
     - Blank line.
     - Line 2: `[red]{escaped exception message}[/]`
     - Blank line.
     - Line 3: `[dim]Details have been written to:[/]`
     - Line 4: `[cyan]{escaped log file path}[/]`
3. **Blank line** -- `AnsiConsole.WriteLine()` after the panel.

### Content Sections

| Section | Markup | Purpose |
|---|---|---|
| Title | `[red bold]An unexpected error occurred.[/]` | Fixed message establishing this is a crash |
| Error message | `[red]{Markup.Escape(ex.Message)}[/]` | The exception's `.Message` property, red, escaped |
| Log reference label | `[dim]Details have been written to:[/]` | Points user to the detailed log |
| Log file path | `[cyan]{Markup.Escape(CrashLogger.LogFilePath)}[/]` | Absolute path to `~/.boydcode/logs/error.log` |

## Layout (80 columns) -- Plain-Text Fallback

If the Spectre Panel rendering throws (the outer try/catch), the fallback
writes directly to `System.Console.Error`:

```
Fatal error: Object reference not set to an instance of an object.
Error log: C:\Users\jason\.boydcode\logs\error.log
```

### Anatomy

1. `System.Console.Error.WriteLine($"Fatal error: {ex.Message}")` -- plain
   text to stderr, no markup, no escaping.
2. `System.Console.Error.WriteLine($"Error log: {CrashLogger.LogFilePath}")`
   -- plain text to stderr.

This fallback uses `System.Console.Error` (not `System.Console.Out`) because:
- Stderr is not affected by stdout redirection/piping.
- Stderr typically bypasses any Spectre.Console rendering pipeline.
- The error is visible even when stdout is redirected to a file.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Spectre Panel | Spectre.Console is functional | Red-bordered panel with structured error, log path in cyan |
| Plain-text fallback | Spectre.Console rendering throws | Two plain-text lines to stderr, no color, no border |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[red bold]` | error-red + bold (1.1, 2.2) | Panel header " boydcode crash ", "An unexpected error occurred." |
| `[red]` | error-red (1.1) | Exception message text |
| `[dim]` | dim (2.2) | "Details have been written to:" label |
| `[cyan]` | info-cyan (1.1) | Log file path |
| `Color.Red` | Color.Red (1.5) | Panel border color |

## Interactive Elements

None. The crash screen is a terminal state. After rendering, the process
exits with `ExitCode.GeneralError` (from the top-level catch) or the
runtime terminates (from the unhandled exception handler).

## Behavior

- **Log-then-render**: `CrashLogger.LogException(ex)` is always called
  before `RenderCrashMessage(ex)`. This ensures the detailed log (with
  full stack trace) is written even if the visual render fails.

- **CrashLogger details**: The log file at `~/.boydcode/logs/error.log` is
  an append-only text file. Each entry contains:
  - A separator line of `=` characters.
  - ISO 8601 timestamp.
  - Exception type full name.
  - Exception message.
  - Full stack trace (or "(no stack trace)").
  - Another separator line.

- **CrashLogger safety**: `CrashLogger.LogException` wraps all file I/O in
  a try/catch that swallows all exceptions. The crash logger must never
  throw, because it runs in crash-handling contexts where additional
  exceptions could terminate the process.

- **ProcessExit cleanup**: Before the crash screen renders, the `ProcessExit`
  handler attempts to dispose the UI (tear down terminal layout, stop input
  reader), the active execution engine (stop containers), and the
  conversation logger. These are all wrapped in try/catch for best-effort
  cleanup.

- **Exit code**: The top-level catch returns `(int)ExitCode.GeneralError`.
  The `AppDomain.UnhandledException` handler does not set an exit code
  (the runtime decides based on `e.IsTerminating`).

- **TaskScheduler handler**: The `UnobservedTaskException` handler logs the
  exception and calls `e.SetObserved()` to prevent it from tearing down
  the process. It does not render a crash screen because the main thread
  may still be running normally.

## Edge Cases

- **Exception message with markup characters**: `Markup.Escape(ex.Message)`
  prevents `[` and `]` in the exception message from being interpreted as
  Spectre markup tags.

- **Very long exception message**: The Panel wraps text within its content
  area. Long messages may extend the panel vertically but will not break
  the layout.

- **Log directory does not exist**: `CrashLogger` calls
  `Directory.CreateDirectory(LogDirectory)` before writing. If directory
  creation fails, the catch block swallows the exception, the log is not
  written, but the crash screen still renders with the intended log path
  (the file just does not exist).

- **Nested exception (inner exception)**: Only `ex.Message` is shown in the
  crash screen. The full exception chain (including inner exceptions) is
  written to the log file via `ex.StackTrace`.

- **Corrupted terminal state**: If the terminal layout is active when the
  crash occurs, the `ProcessExit` handler attempts to tear it down
  (`IDisposable.Dispose()` on the UI). If this fails, the crash panel
  renders into whatever terminal state exists. The panel is self-contained
  and does not depend on cursor position or scroll region state.

- **Non-interactive/piped terminal**: The Spectre Panel renders to stdout
  with markup stripped. The plain-text fallback writes to stderr regardless
  of stdout redirection.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Crash Panel | Section 15 | Red-bordered error panel with structured content |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| Top-level try/catch | `Program.cs` | Main scope | 62-129 |
| UnhandledException handler | `Program.cs` | Lambda | 19-26 |
| UnobservedTaskException handler | `Program.cs` | Lambda | 28-32 |
| ProcessExit cleanup handler | `Program.cs` | Lambda | 36-60 |
| RenderCrashMessage | `Program.cs` | `RenderCrashMessage` | 131-154 |
| Spectre Panel render | `Program.cs` | `RenderCrashMessage` | 133-146 |
| Plain-text fallback | `Program.cs` | `RenderCrashMessage` | 148-153 |
| CrashLogger class | `CrashLogger.cs` | Full file | 1-34 |
| LogFilePath | `CrashLogger.cs` | `LogFilePath` | 8 |
| LogException | `CrashLogger.cs` | `LogException` | 10-33 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
