# Screen: /jea effective

## Overview

Displays the effective JEA configuration for the current session -- the result
of composing the `_global` profile with any project-assigned profiles. This
shows what commands and modules the agent can actually use, after the
deny-always-wins composition rule is applied.

**Screen IDs**: JEA-30

## Trigger

`/jea effective`

No arguments are accepted.

## Layout (80 columns)

### With Active Project

    (blank line)
      Language mode:  ConstrainedLanguage
      Commands:       24
    (blank line)
    ── Allowed commands ────────────────────────────────────────────────────────
        v Get-ChildItem
        v Get-Content
        v Get-Item
        v Get-Location
        v Set-Content
        v Set-Location
        v Test-Path
        v New-Item
        ... (24 total)
    (blank line)
    ── Modules ─────────────────────────────────────────────────────────────────
        Microsoft.PowerShell.Management
        Microsoft.PowerShell.Utility
        Microsoft.PowerShell.Security
    (blank line)
      Source profiles: _global, security
    (blank line)

### Ambient / No Project

    (blank line)
      Language mode:  ConstrainedLanguage
      Commands:       28
    (blank line)
    ── Allowed commands ────────────────────────────────────────────────────────
        v Get-ChildItem
        ...
    (blank line)
      Source profiles: _global
    (blank line)

### No Allowed Commands

    (blank line)
      Language mode:  NoLanguage
      Commands:       0
    (blank line)
      Source profiles: _global, lockdown
    (blank line)

## States

| State | Condition | Visual Difference |
|---|---|---|
| Full effective view | Has allowed commands and modules | Summary + allowed commands section + modules section + source footer |
| Commands only | Has commands, no modules | Summary + allowed commands section + source footer |
| Modules only | Has modules, no commands | Summary + modules section + source footer |
| Empty effective | No commands, no modules | Summary only + source footer |
| With project profiles | Active project has JEA profiles assigned | Source footer lists `_global` + project profiles |
| Ambient project | No project or ambient `_default` | Source footer lists `_global` only |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | "Language mode:" and "Commands:" labels, section divider titles |
| `[green]` | success-green | `v` checkmark for allowed commands |
| `[dim]` | dim (2.2) | Section rule style, "Source profiles:" footer line |

## Interactive Elements

None. This screen is purely static output.

## Behavior

1. **Global seeding**: `EnsureGlobalProfileAsync` is called first to ensure the
   `_global` profile exists.

2. **Project resolution**: If an active project exists and is not the ambient
   `_default`, its JEA profile assignments are loaded from
   `project.Execution.JeaProfiles`. Otherwise, no project profiles are used.

3. **Composition**: `_composer.ComposeAsync` merges the global profile with the
   project's assigned profiles. The composition uses deny-always-wins semantics:
   if any profile denies a command, it is excluded from the effective allowed
   list regardless of other profiles allowing it.

4. **Summary**: Two bold key-value lines show the effective language mode and
   total allowed command count.

5. **Allowed commands section**: If there are allowed commands, they are listed
   under a section divider with `[green]v[/]` markers, indented 4 spaces.

6. **Modules section**: If there are modules, they are listed under a separate
   section divider, indented 4 spaces without markers.

7. **Source footer**: A dim line at the bottom lists all contributing profiles
   as a comma-separated list. This always includes `_global` and appends any
   project-assigned profiles.

## Edge Cases

- **No project profiles**: When using the ambient project or no project, only
  `_global` contributes. The source footer shows just "_global".

- **Denied commands not shown**: The effective view only shows allowed commands.
  Commands that were denied by any profile are silently excluded. To see which
  commands are denied, use `/jea show` on individual profiles.

- **Very long command list**: All allowed commands are listed without pagination.
  The `_global` profile has 28 commands by default, producing about 30 lines
  of output. This is acceptable for scrollback.

- **Ambient project with null execution**: If the active project has no
  `Execution` config, `JeaProfiles` defaults to an empty list. Only `_global`
  contributes.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Section Divider | Section 2 | "Allowed commands" and "Modules" headings |
| Status Message | Section 1 | Not directly used, but the `[dim]` footer follows the pattern |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| Effective flow | `Commands/JeaSlashCommand.cs` | `HandleEffectiveAsync` | 475-523 |
| Global seeding | `Commands/JeaSlashCommand.cs` | `EnsureGlobalProfileAsync` | 107-122 |
| Project resolution | `Commands/JeaSlashCommand.cs` | `HandleEffectiveAsync` | 479-493 |
| Composition | `Commands/JeaSlashCommand.cs` | `HandleEffectiveAsync` | 495 |
| Allowed commands | `Commands/JeaSlashCommand.cs` | `HandleEffectiveAsync` | 501-508 |
| Modules | `Commands/JeaSlashCommand.cs` | `HandleEffectiveAsync` | 510-517 |
| Source footer | `Commands/JeaSlashCommand.cs` | `HandleEffectiveAsync` | 519-521 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
