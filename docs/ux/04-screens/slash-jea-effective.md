# Screen: /jea effective

## Overview

Displays the effective JEA configuration for the current session in a modeless
Detail Modal window (component pattern #11, Variant C). Shows the result of
composing the `_global` profile with any project-assigned profiles -- what
commands and modules the agent can actually use, after the deny-always-wins
composition rule is applied.

All content is drawn using Terminal.Gui native drawing (`SetAttribute`,
`Move`, `AddStr`) with structured key-value layout, section dividers, and
color-coded command lists.

**Screen IDs**: JEA-30

## Trigger

`/jea effective`

No arguments are accepted.

## Layout (80 columns)

### With Active Project

```
+-- Effective JEA ------------------------------------------+
|                                                            |
|  Language   ConstrainedLanguage                            |
|  Commands   24                                             |
|                                                            |
|  -- Allowed commands ---                                   |
|  ✓ Get-ChildItem                                           |
|  ✓ Get-Content                                             |
|  ✓ Get-Item                                                |
|  ✓ Get-Location                                            |
|  ✓ Set-Content                                             |
|  ✓ Set-Location                                            |
|  ✓ Test-Path                                               |
|  ✓ New-Item                                                |
|  ... (24 total)                                            |
|                                                            |
|  -- Modules ---                                            |
|  Microsoft.PowerShell.Management                           |
|  Microsoft.PowerShell.Utility                              |
|  Microsoft.PowerShell.Security                             |
|                                                            |
|  Source profiles: _global, security                         |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

### Ambient / No Project

```
+-- Effective JEA ------------------------------------------+
|                                                            |
|  Language   ConstrainedLanguage                            |
|  Commands   28                                             |
|                                                            |
|  -- Allowed commands ---                                   |
|  ✓ Get-ChildItem                                           |
|  ...                                                       |
|                                                            |
|  Source profiles: _global                                   |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

### No Allowed Commands

```
+-- Effective JEA ------------------------------------------+
|                                                            |
|  Language   NoLanguage                                     |
|  Commands   0                                              |
|                                                            |
|  Source profiles: _global, lockdown                         |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Full effective view | Has allowed commands and modules | Header + allowed commands section + modules section + source footer |
| Commands only | Has commands, no modules | Header + allowed commands section + source footer |
| Modules only | Has modules, no commands | Header + modules section + source footer |
| Empty effective | No commands, no modules | Header only + source footer |
| With project profiles | Active project has JEA profiles assigned | Source footer lists `_global` + project profiles |
| Ambient project | No project or ambient `_default` | Source footer lists `_global` only |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

- `Theme.Modal.BorderScheme` -- blue border on the modeless window
- `Theme.Semantic.Muted` -- dim labels in info pairs ("Language", "Commands"),
  section divider rules, "Source profiles:" footer label, "Esc to dismiss"
- `Theme.Semantic.Info` -- cyan values in info pairs (language mode, command
  count), source profile names
- `Theme.Semantic.Success` -- green `✓` (`Theme.Symbols.Check`) for allowed
  commands
- `Theme.Semantic.Default` -- white command names, module names

## Interactive Elements

None. This screen is purely read-only.

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

4. **Window construction**: `ShowDetailModal` opens a Terminal.Gui modeless
   `Window` with the title "Effective JEA" and a blue border
   (`Theme.Modal.BorderScheme`).

5. **Native drawing layout**: The window's inner `View` overrides
   `OnDrawingContent` to draw the structured layout:

   - **Header info pairs**: Two rows using the Info Grid pattern (pattern
     #9). "Language" and "Commands" labels at `x = 2` in
     `Theme.Semantic.Muted`, values in `Theme.Semantic.Info`.

   - **Allowed commands section**: A Section Divider (pattern #8) labeled
     "Allowed commands". Each command on its own line at 2-char indent.
     `Theme.Symbols.Check` (`✓`) drawn with `Theme.Semantic.Success`
     (green), followed by the command name in `Theme.Semantic.Default`
     (white).

   - **Modules section**: A Section Divider labeled "Modules". Each module
     on its own line at 2-char indent in `Theme.Semantic.Default` (white),
     no symbol prefix. Omitted if no modules exist.

   - **Source footer**: "Source profiles:" label in `Theme.Semantic.Muted`,
     followed by comma-separated profile names in `Theme.Semantic.Info`
     (cyan). Always includes `_global`; appends project-assigned profiles.

6. **Dismiss hint**: "Esc to dismiss" is drawn at the bottom of the content
   in `Theme.Semantic.Muted`. The `ActivityBarView` transitions to
   `ActivityState.Modal` while the window is open.

## Edge Cases

- **No project profiles**: When using the ambient project or no project, only
  `_global` contributes. The source footer shows just "_global".

- **Denied commands not shown**: The effective view only shows allowed commands.
  Commands that were denied by any profile are silently excluded. To see which
  commands are denied, use `/jea show` on individual profiles.

- **Very long command list**: All allowed commands are listed without pagination.
  The `_global` profile has 28 commands by default. The window scrolls via
  Terminal.Gui's built-in Viewport scrolling when content exceeds the terminal
  height (capped at 90% per pattern #11 sizing rules).

- **Ambient project with null execution**: If the active project has no
  `Execution` config, `JeaProfiles` defaults to an empty list. Only `_global`
  contributes.

## Non-TUI Fallback

When running in non-interactive/piped mode (no Terminal.Gui), the effective
JEA config is rendered as plain text to stdout. Section dividers use `--`
prefix. Checkmarks use `v` text character.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Modal Overlay (Detail Modal) | #11, Variant C | Modeless window with native drawing layout |
| Info Grid | #9 | Header key-value pairs (language, commands) |
| Section Divider | #8 | "Allowed commands" and "Modules" headings |
| Status Message | #7 | Source profiles footer |
