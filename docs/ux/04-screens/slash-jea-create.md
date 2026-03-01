# Screen: /jea create

## Overview

Creates a new JEA profile by prompting for a language mode, then entering a
loop to add commands (with allow/deny designation) and modules. The profile
is saved to disk upon completion.

**Screen IDs**: JEA-08, JEA-09, JEA-10, JEA-11, JEA-12, JEA-13

## Trigger

`/jea create [name]`

- If `name` is provided inline, it is used directly.
- If `name` is omitted, an interactive text prompt appears.

## Layout (80 columns)

### Full Create Flow

    Profile name: security
    Language mode:
    > FullLanguage
      ConstrainedLanguage
      RestrictedLanguage
      NoLanguage

    Add to profile:
    > Add command
      Add module
      Done

      Command name: Get-Process
      Action:
    > Allow
      Deny
      v Allow Get-Process

    Add to profile:
    > Add command
      Add module
      Done

      Command name: Stop-Service
      Action:
    > Allow
      Deny
      v Deny Stop-Service

    Add to profile:
      Add command
    > Add module
      Done

      Module name: PSScriptAnalyzer
      v Module PSScriptAnalyzer added.

    Add to profile:
      Add command
      Add module
    > Done

      v Profile security created.
    File: C:\Users\jason\.boydcode\jea\security.profile

### Name Already Exists

    Profile name: _global
    Error: Profile _global already exists.

### Validation Errors

    Profile name: my profile!
    Error: Profile name must contain only letters, numbers, hyphens, and
    underscores.

    Profile name: _global
    Error: _global is a reserved profile name.

    Profile name: builtin
    Error: builtin is a reserved profile name.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Name prompt | No name argument | TextPrompt with green highlight on "name" |
| Name validation error | Invalid characters, reserved name, or empty | Red error with specific reason |
| Already exists | Name matches existing profile | Red error with bold entity name |
| Language mode selection | After valid name | SelectionPrompt with 4 language mode options |
| Add loop | Building profile | Repeating SelectionPrompt: Add command / Add module / Done |
| Add command | "Add command" selected | Name prompt + Allow/Deny selection + colored confirmation |
| Add module | "Add module" selected | Name prompt + success confirmation |
| Created | "Done" selected | Green success + dim file path |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green]` | success-green | Name prompt highlight, success checkmarks, "Allow" marker |
| `[red]` | error-red | "Error:" prefix, "Deny" marker |
| `[bold]` | bold (2.2) | Entity name in error/confirmation messages |
| `[dim]` | dim (2.2) | File path after creation |
| `Color.Green` | Spectre color (1.5) | SelectionPrompt highlight style |

## Interactive Elements

| Element | Type | Label | Validation/Default |
|---|---|---|---|
| Profile name | `SpectreHelpers.PromptNonEmpty` | `Profile [green]name[/]:` | Non-empty validation |
| Language mode | `SpectreHelpers.Select` | `Language mode:` | 4 `PSLanguageModeName` values |
| Add action | `SpectreHelpers.Select` | `Add to profile:` | Add command / Add module / Done |
| Command name | `SpectreHelpers.PromptNonEmpty` | `  Command name:` | Non-empty validation |
| Allow/Deny | `SpectreHelpers.Select` | `  Action:` | Allow / Deny |
| Module name | `SpectreHelpers.PromptNonEmpty` | `  Module name:` | Non-empty validation |

## Behavior

1. **Name resolution**: If the name is provided as a trailing argument, it is
   used directly. Multiple words are joined with spaces.

2. **Name validation**: `ValidateProfileName` checks three conditions:
   - Non-empty (should not occur with `PromptNonEmpty`, but guarded)
   - Not a reserved name (`_global` or `builtin`)
   - Matches regex `^[a-zA-Z0-9_-]+$` (letters, numbers, hyphens, underscores)

3. **Duplicate check**: The name is checked against existing profiles via
   `_store.LoadAsync`. If found, a red error is shown.

4. **Language mode**: A selection prompt offers 4 PowerShell language modes.
   These control what language features are available in the constrained
   runspace.

5. **Add loop**: A repeating selection prompt allows adding commands or modules
   one at a time. Each command requires an Allow/Deny designation. The loop
   continues until "Done" is selected.

6. **Command confirmation**: Each added command shows a colored marker:
   `[green]Allow[/]` or `[red]Deny[/]`, plus a green checkmark and the
   bolded command name.

7. **Profile save**: The profile is saved to `~/.boydcode/jea/{name}.profile`.
   The file path is shown in dim after the success message.

## Edge Cases

- **Empty profile**: The user can select "Done" immediately without adding any
  commands or modules. The profile is saved with an empty entries list and
  empty modules list. This is valid.

- **Duplicate command names**: No deduplication is performed. If the user adds
  the same command name twice, both entries are stored. The `JeaProfileComposer`
  handles conflicts during composition.

- **Reserved names**: `_global` and `builtin` (from `BuiltInJeaProfile.Name`)
  are both reserved. Attempting to create either shows a red error.

- **Name with spaces**: The regex `^[a-zA-Z0-9_-]+$` rejects names with
  spaces. If the name is provided as a trailing argument with spaces (e.g.,
  `/jea create my profile`), it is joined as "my profile" which fails
  validation.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Text Prompt | Section 7 | Profile name, command name, module name |
| Selection Prompt | Section 5 | Language mode, add action, allow/deny |
| Status Message | Section 1 | Success, error, dim file path |

