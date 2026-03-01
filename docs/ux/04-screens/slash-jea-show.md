# Screen: /jea show

## Overview

Displays a detailed view of a single JEA profile in a bordered panel, showing
language mode, allowed commands, denied commands, modules, and the profile
file path.

**Screen IDs**: JEA-04, JEA-05, JEA-06, JEA-07

## Trigger

`/jea show [name]`

- If `name` is provided inline, it is used directly.
- If `name` is omitted, a selection prompt lists all profiles.

## Layout (80 columns)

### Full Profile Detail

    (blank line)
    ╭─ _global ────────────────────────────────────────────────────────────────╮
    │ Language mode:  ConstrainedLanguage                                       │
    │                                                                           │
    │ Allowed commands:                                                         │
    │   v Get-ChildItem                                                         │
    │   v Get-Content                                                           │
    │   v Get-Item                                                              │
    │   v Get-Location                                                          │
    │   v Set-Content                                                           │
    │   v Set-Location                                                          │
    │   v Test-Path                                                             │
    │   ... (28 total)                                                          │
    │                                                                           │
    │ Denied commands:                                                          │
    │   x Remove-Item                                                           │
    │   x Invoke-Expression                                                     │
    │                                                                           │
    │ Modules:                                                                  │
    │   Microsoft.PowerShell.Management                                         │
    │   Microsoft.PowerShell.Utility                                            │
    │   Microsoft.PowerShell.Security                                           │
    │                                                                           │
    │ File: C:\Users\jason\.boydcode\jea\_global.profile                        │
    ╰──────────────────────────────────────────────────────────────────────────╯
    (blank line)

### Minimal Profile (no commands, no modules)

    (blank line)
    ╭─ empty-profile ──────────────────────────────────────────────────────────╮
    │ Language mode:  RestrictedLanguage                                        │
    │                                                                           │
    │ File: C:\Users\jason\.boydcode\jea\empty-profile.profile                  │
    ╰──────────────────────────────────────────────────────────────────────────╯
    (blank line)

### Not Found

    Error: Profile nonexistent not found.

### No Profiles Available

    No JEA profiles found.
    Create one with /jea create <name>

## States

| State | Condition | Visual Difference |
|---|---|---|
| Full detail | Profile has commands and modules | Panel with all sections |
| No allowed commands | No commands with `IsDenied: false` | "Allowed commands" section omitted |
| No denied commands | No commands with `IsDenied: true` | "Denied commands" section omitted |
| No modules | Empty modules list | "Modules" section omitted |
| No commands or modules | Empty profile | Only language mode and file path in panel |
| Profile selection | No name argument, profiles exist | SelectionPrompt listing all names |
| No profiles | No name argument, no profiles exist | "No JEA profiles found" + dim hint |
| Not found | Name does not match any profile | Red error with bold entity name |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | Profile name in panel header, "Language mode:", "Allowed commands:", "Denied commands:", "Modules:" labels |
| `[green]` | success-green | `v` checkmark for allowed commands |
| `[red]` | error-red | `x` marker for denied commands, "Error:" prefix |
| `[dim]` | dim (2.2) | "File:" path line, empty state hint |

## Interactive Elements

| Element | Type | Label | Context |
|---|---|---|---|
| Profile selection | `PromptProfileSelectionAsync` | `Select profile:` | No name argument, profiles exist |

## Behavior

1. **Name resolution**: If the name is provided as trailing arguments, they are
   joined with spaces. Otherwise, `PromptProfileSelectionAsync` is called.

2. **Profile loading**: The profile is loaded via `_store.LoadAsync`. If null,
   a red error is shown.

3. **Panel construction**: `BuildProfileDetail` constructs a `Markup` renderable
   with newline-separated lines. Sections are conditionally included:
   - Language mode (always shown)
   - Allowed commands (if `AllowedCommands.Count > 0`)
   - Denied commands (if `DeniedCommands.Count > 0`)
   - Modules (if `Modules.Count > 0`)
   - File path (always shown, in dim)

4. **Panel styling**: The panel uses `BoxBorder.Rounded` with
   `[bold]{profileName}[/]` as the header and `Padding(1, 0)` (1 char
   horizontal, 0 vertical).

5. **Command indicators**: Allowed commands use `[green]v[/]` (the standard
   success indicator). Denied commands use `[red]x[/]`. Each command name is
   escaped via `Markup.Escape`.

6. **File path**: `GetProfileFilePath` constructs the path from
   `~/.boydcode/jea/{name}.profile`.

## Edge Cases

- **Very long command lists**: All commands are rendered inline within the
  panel. Profiles with many commands (e.g., the `_global` profile with 28
  allowed commands) produce a tall panel. No pagination or truncation is
  applied.

- **Narrow terminal**: The panel's Rounded border adds 4 characters of
  overhead. At very narrow widths, long command names wrap inside the panel.

- **Long file paths**: The file path is escaped and rendered inside the panel.
  Long paths wrap naturally within the available width.

- **Profile selection with no profiles**: `PromptProfileSelectionAsync` checks
  for empty profile list and shows "No JEA profiles found" + dim hint. Returns
  null, and the flow exits without error.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Selection Prompt | Section 5 | Profile selection |
| Status Message | Section 1 | Error, empty state messages |
| Empty State | Section 13 | "No JEA profiles found" |

The panel is a one-off layout not covered by a standard component pattern.

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| Show flow | `Commands/JeaSlashCommand.cs` | `HandleShowAsync` | 172-200 |
| Profile detail builder | `Commands/JeaSlashCommand.cs` | `BuildProfileDetail` | 675-716 |
| Panel construction | `Commands/JeaSlashCommand.cs` | `HandleShowAsync` | 192-195 |
| Profile selection helper | `Commands/JeaSlashCommand.cs` | `PromptProfileSelectionAsync` | 638-649 |
| File path helper | `Commands/JeaSlashCommand.cs` | `GetProfileFilePath` | 718-725 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
