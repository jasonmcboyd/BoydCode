# Screen: /jea show

## Overview

Displays a detailed view of a single JEA profile in a modeless Detail Modal
window (component pattern #11, Variant C), showing name, scope, description,
allowed commands, denied commands, modules, and the profile file path. Content
is drawn using Terminal.Gui native drawing (`SetAttribute`, `Move`, `AddStr`)
with structured key-value layout and color-coded command lists.

**Screen IDs**: JEA-04, JEA-05, JEA-06, JEA-07

## Trigger

`/jea show [name]`

- If `name` is provided inline, it is used directly.
- If `name` is omitted, a selection prompt lists all profiles.

## Layout (80 columns)

### Full Profile Detail

```
+-- _global ------------------------------------------------+
|                                                            |
|  Name       _global                                       |
|  Scope      User                                          |
|  Language   ConstrainedLanguage                            |
|                                                            |
|  -- Allowed commands ---                                   |
|  ✓ Get-ChildItem                                           |
|  ✓ Get-Content                                             |
|  ✓ Get-Item                                                |
|  ✓ Get-Location                                            |
|  ✓ Set-Content                                             |
|  ✓ Set-Location                                            |
|  ✓ Test-Path                                               |
|  ... (28 total)                                            |
|                                                            |
|  -- Denied commands ---                                    |
|  ✗ Remove-Item                                             |
|  ✗ Invoke-Expression                                       |
|                                                            |
|  -- Modules ---                                            |
|  Microsoft.PowerShell.Management                           |
|  Microsoft.PowerShell.Utility                              |
|  Microsoft.PowerShell.Security                             |
|                                                            |
|  File  ~/.boydcode/jea/_global.profile                     |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

### Minimal Profile (no commands, no modules)

```
+-- empty-profile ------------------------------------------+
|                                                            |
|  Name       empty-profile                                  |
|  Scope      User                                           |
|  Language   RestrictedLanguage                              |
|                                                            |
|  File  ~/.boydcode/jea/empty-profile.profile               |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

### Not Found

```
Error: Profile nonexistent not found.
```

### No Profiles Available

```
No JEA profiles found.
Create one with /jea create <name>
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Full detail | Profile has commands and modules | Window with header, command lists, modules, file path |
| No allowed commands | No commands with `IsDenied: false` | "Allowed commands" section omitted |
| No denied commands | No commands with `IsDenied: true` | "Denied commands" section omitted |
| No modules | Empty modules list | "Modules" section omitted |
| No commands or modules | Empty profile | Only header info pairs and file path |
| Profile selection | No name argument, profiles exist | SelectionPrompt listing all names |
| No profiles | No name argument, no profiles exist | "No JEA profiles found" + dim hint |
| Not found | Name does not match any profile | Red error with bold entity name |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

- `Theme.Modal.BorderScheme` -- blue border on the modeless window
- `Theme.Semantic.Muted` -- dim labels in info pairs ("Name", "Scope",
  "Language", "File"), section divider rules, "Esc to dismiss", empty
  state hint
- `Theme.Semantic.Info` -- cyan values in info pairs (profile name, scope,
  language mode)
- `Theme.Semantic.Success` -- green `✓` (`Theme.Symbols.Check`) for
  allowed commands
- `Theme.Semantic.Error` -- red `✗` (`Theme.Symbols.Cross`) for denied
  commands, "Error:" prefix
- `Theme.Semantic.Default` -- white command names in both lists

## Interactive Elements

| Element | Type | Label | Context |
|---|---|---|---|
| Profile selection | `PromptProfileSelectionAsync` | `Select profile:` | No name argument, profiles exist |

## Behavior

1. **Name resolution**: If the name is provided as trailing arguments, they are
   joined with spaces. Otherwise, `PromptProfileSelectionAsync` is called.

2. **Profile loading**: The profile is loaded via `_store.LoadAsync`. If null,
   a red error is shown.

3. **Window construction**: `ShowDetailModal` opens a Terminal.Gui modeless
   `Window` with a blue border (`Theme.Modal.BorderScheme`). The window title
   is the profile name.

4. **Native drawing layout**: The window's inner `View` overrides
   `OnDrawingContent` to draw the structured layout:

   - **Header info pairs**: Three rows using the Info Grid pattern (pattern
     #9). Labels ("Name", "Scope", "Language") at `x = 2` in
     `Theme.Semantic.Muted`, values at label pad offset in
     `Theme.Semantic.Info`. Label column width is `Theme.Layout.InfoLabelPad`
     (10 chars).

   - **Section dividers**: "Allowed commands", "Denied commands", and
     "Modules" sections are separated by Section Dividers (pattern #8) drawn
     with `Theme.Semantic.Muted` and `Theme.Symbols.Rule`.

   - **Allowed commands**: Each command on its own line at 2-char indent.
     `Theme.Symbols.Check` (`✓`) drawn with `Theme.Semantic.Success` (green),
     followed by the command name in `Theme.Semantic.Default` (white).

   - **Denied commands**: Each command on its own line at 2-char indent.
     `Theme.Symbols.Cross` (`✗`) drawn with `Theme.Semantic.Error` (red),
     followed by the command name in `Theme.Semantic.Default` (white).

   - **Modules**: Each module on its own line at 2-char indent in
     `Theme.Semantic.Default` (white), no symbol prefix.

   - **File path**: An info pair at the bottom: "File" label in
     `Theme.Semantic.Muted`, path value in `Theme.Semantic.Info`.

5. **Dismiss hint**: "Esc to dismiss" is drawn at the bottom of the content
   in `Theme.Semantic.Muted`. The `ActivityBarView` transitions to
   `ActivityState.Modal` while the window is open.

6. **File path**: `GetProfileFilePath` constructs the path from
   `~/.boydcode/jea/{name}.profile`.

## Edge Cases

- **Very long command lists**: All commands are rendered inline within the
  window. Profiles with many commands (e.g., the `_global` profile with 28
  allowed commands) produce a tall window. The window scrolls if content
  exceeds the terminal height (capped at 90% of terminal height per pattern
  #11 sizing rules).

- **Narrow terminal**: The modal window is sized to fit content up to the
  terminal width (capped at 90%). At very narrow widths, long command names
  wrap inside the window.

- **Long file paths**: The file path wraps naturally within the available
  width of the window.

- **Profile selection with no profiles**: `PromptProfileSelectionAsync` checks
  for empty profile list and shows "No JEA profiles found" + dim hint. Returns
  null, and the flow exits without error.

## Non-TUI Fallback

When running in non-interactive/piped mode (no Terminal.Gui), the profile
detail is rendered as plain text to stdout using the same structural layout
but without color. Section dividers use `--` prefix. Allowed/denied markers
use text characters `v` and `x`.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Modal Overlay (Detail Modal) | #11, Variant C | Modeless window with native drawing layout |
| Info Grid | #9 | Header key-value pairs (name, scope, language) |
| Section Divider | #8 | "Allowed commands", "Denied commands", "Modules" headings |
| Selection Prompt | #12 | Profile selection when name omitted |
| Status Message | #7 | Error, empty state messages |
| Empty State | #21 | "No JEA profiles found" |
