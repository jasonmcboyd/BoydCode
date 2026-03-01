# Screen: /jea edit

## Overview

An interactive menu loop for editing an existing JEA profile. The user can
change the language mode, add/remove commands, toggle command deny status,
and add/remove modules. Changes are accumulated in memory and saved when the
user selects "Done".

**Screen IDs**: JEA-14, JEA-15, JEA-16, JEA-17, JEA-18, JEA-19, JEA-20,
JEA-21, JEA-22

## Trigger

`/jea edit [name]`

- If `name` is provided inline, it is used directly.
- If `name` is omitted, a selection prompt lists all profiles.

## Layout (80 columns)

### Edit Menu

    Edit _global:
    > Change language mode
      Add command
      Remove command
      Toggle command deny
      Add module
      Remove module
      Done

### Change Language Mode

      Language mode:
    > FullLanguage
      ConstrainedLanguage
      RestrictedLanguage
      NoLanguage
      v Language mode set to FullLanguage.

### Add Command

      Command name: Invoke-WebRequest
      Action:
    > Allow
      Deny
      v Allow Invoke-WebRequest

### Remove Command

      Select command to remove:
    > Get-Process
      Stop-Service
      Invoke-WebRequest
      v Removed Get-Process.

### Toggle Command Deny

      Select command to toggle:
    > Get-Process  Allow
      Stop-Service  Deny
      Invoke-WebRequest  Allow
      v Stop-Service set to Allow.

### Add Module

      Module name: Az
      v Module Az added.

### Remove Module

      Select module to remove:
    > PSScriptAnalyzer
      Az
      v Removed module PSScriptAnalyzer.

### Empty State Warnings

      No commands to remove.

      No commands to toggle.

      No modules to remove.

### Save and Exit

      v Profile _global saved.
    File: C:\Users\jason\.boydcode\jea\_global.profile

### Not Found

    Error: Profile _global not found.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Profile selection | No name argument | SelectionPrompt listing all profiles |
| Edit menu | Main loop | SelectionPrompt with 7 choices |
| Change language mode | Selected from menu | Language mode SelectionPrompt + success |
| Add command | Selected from menu | Name prompt + Allow/Deny + colored confirmation |
| Remove command | Selected from menu, commands exist | Command SelectionPrompt + success |
| Remove command empty | Selected from menu, no commands | Yellow "No commands to remove" |
| Toggle deny | Selected from menu, commands exist | Command SelectionPrompt showing current status + success |
| Toggle deny empty | Selected from menu, no commands | Yellow "No commands to toggle" |
| Add module | Selected from menu | Name prompt + success |
| Remove module | Selected from menu, modules exist | Module SelectionPrompt + success |
| Remove module empty | Selected from menu, no modules | Yellow "No modules to remove" |
| Saved | "Done" selected | Green success + dim file path |
| Not found | Profile does not exist | Red error with bold entity name |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | Profile name in menu title, entity names in confirmations, language mode value |
| `[green]` | success-green | Success checkmarks, "Allow" markers |
| `[red]` | error-red | "Error:" prefix, "Deny" markers |
| `[yellow]` | warning-yellow | "No commands/modules to..." empty state messages |
| `[dim]` | dim (2.2) | File path after save |
| `Color.Green` | Spectre color (1.5) | SelectionPrompt highlight style |

## Interactive Elements

| Element | Type | Label | Context |
|---|---|---|---|
| Profile selection | `PromptProfileSelectionAsync` | `Select profile:` | No name argument |
| Edit menu | `SpectreHelpers.Select` | `Edit [bold]{name}[/]:` | Main loop, remembers last index |
| Language mode | `SpectreHelpers.Select` | `  Language mode:` | Change language mode |
| Command name | `SpectreHelpers.PromptNonEmpty` | `  Command name:` | Add command |
| Allow/Deny | `SpectreHelpers.Select` | `  Action:` | Add command |
| Command to remove | `SpectreHelpers.Select` | `  Select command to remove:` | Remove command |
| Command to toggle | `SpectreHelpers.Select` | `  Select command to toggle:` | Toggle deny |
| Module name | `SpectreHelpers.PromptNonEmpty` | `  Module name:` | Add module |
| Module to remove | `SpectreHelpers.Select` | `  Select module to remove:` | Remove module |

## Behavior

1. **Profile loading**: The profile is loaded once at the start. Its entries
   and modules are copied into mutable lists for editing.

2. **Menu loop**: The main loop uses `SpectreHelpers.Select` with a
   `lastIndex` variable to remember the user's last selection. The menu has
   7 fixed choices (not dynamically generated).

3. **In-memory editing**: All changes are accumulated in the `entries` and
   `modules` lists and the `languageMode` variable. Nothing is saved until
   the user selects "Done".

4. **Toggle deny display**: The toggle command list shows each command name
   followed by double-space separation and a colored status: `[green]Allow[/]`
   or `[red]Deny[/]`. The selected entry is parsed by splitting on double-space
   to extract the command name.

5. **Profile save**: When "Done" is selected, a new `JeaProfile` record is
   created from the edited state and saved via `_store.SaveAsync`. The file
   path is displayed in dim.

6. **Profile selection helper**: `PromptProfileSelectionAsync` is a shared
   helper that lists all profile names and returns the selected one, or null
   if no profiles exist.

## Edge Cases

- **Editing `_global`**: The global profile can be edited freely. There is no
  special protection. Changes take effect on the next session.

- **Empty actions**: "Remove command", "Toggle command deny", and "Remove
  module" show a yellow warning if the respective list is empty and return to
  the menu without modification.

- **Duplicate commands**: The same command name can be added multiple times.
  No deduplication check is performed during editing.

- **Toggle parsing**: The toggle command parses the selected string by
  splitting on double-space (`"  "`). If a command name contains double-space
  characters, the parsing may fail. This is unlikely in practice.

- **No profiles for selection**: If `PromptProfileSelectionAsync` finds no
  profiles, it shows "No JEA profiles found" with a dim hint and returns null.
  The edit flow exits gracefully.

- **Unsaved changes on cancellation**: If the user force-quits during the edit
  loop (Ctrl+C), changes are lost since they are only saved on "Done".

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Edit Menu Loop | Section 11 | Main edit loop with remembered index |
| Selection Prompt | Section 5 | Menu, language mode, command/module selections |
| Text Prompt | Section 7 | Command name, module name |
| Status Message | Section 1 | Success, error, warning, dim messages |
| Empty State | Section 13 | Yellow warnings for empty lists |

