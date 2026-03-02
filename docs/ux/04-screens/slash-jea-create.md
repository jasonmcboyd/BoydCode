# Screen: /jea create

## Overview

Creates a new JEA profile using a Multi-Step Wizard Dialog (component
pattern #32). The wizard guides the user through 3 steps: (1) Name and
language mode, (2) Commands and modules, (3) Review and confirm. The profile
is saved to disk upon completion.

**Screen IDs**: JEA-08, JEA-09, JEA-10, JEA-11, JEA-12, JEA-13

## Trigger

`/jea create [name]`

- If `name` is provided inline, the wizard opens at Step 1 with the name
  pre-filled.
- If `name` is omitted, the wizard opens at Step 1 with an empty name field.

## Layout (80 columns)

### Step 1: Name and Language Mode

```
+-- Create Profile -----------------------------------------+
|                                                            |
|  Step 1 of 3: Name and Language Mode                       |
|  --------------------------------------------------------  |
|                                                            |
|  Name:  [security                                       ]  |
|                                                            |
|  Language mode:                                            |
|    > FullLanguage                                          |
|      ConstrainedLanguage                                   |
|      RestrictedLanguage                                    |
|      NoLanguage                                            |
|                                                            |
|  [ Cancel ]                                    [ Next > ]  |
|                                                            |
+------------------------------------------------------------+
```

The Name field is required -- the Next button validates non-empty, no
reserved names, and alphanumeric/hyphen/underscore characters only.

### Step 1 -- Validation Errors

```
|  Name:  [                                               ]  |
|         Name cannot be empty.                              |
```

```
|  Name:  [_global                                        ]  |
|         _global is a reserved profile name.                |
```

```
|  Name:  [my profile!                                    ]  |
|         Name must contain only letters, numbers,           |
|         hyphens, and underscores.                          |
```

```
|  Name:  [security                                       ]  |
|         Profile 'security' already exists.                 |
```

Validation errors appear below the Name field in `Theme.Semantic.Error`
(bright red).

### Step 2: Commands and Modules

```
+-- Create Profile -----------------------------------------+
|                                                            |
|  Step 2 of 3: Commands and Modules                         |
|  --------------------------------------------------------  |
|                                                            |
|  Commands:                                                 |
|    Get-Process          Allow                              |
|    Stop-Service         Deny                               |
|                                                            |
|  Modules:                                                  |
|    PSScriptAnalyzer                                        |
|                                                            |
|  [ Add Command ] [ Add Module ]                            |
|                                                            |
|  [ Cancel ]                          [ < Back ] [ Next > ] |
|                                                            |
+------------------------------------------------------------+
```

Commands show the name and Allow/Deny status. "Allow" uses
`Theme.Semantic.Success` (green) and "Deny" uses `Theme.Semantic.Error` (red).

#### Add Command Sub-Dialog

Clicking "Add Command" opens a nested Form Dialog (pattern #31):

```
+-- Add Command -------------------------------------------+
|                                                           |
|  Command name:  [Invoke-WebRequest                     ]  |
|                                                           |
|  Action:                                                  |
|    > Allow                                                |
|      Deny                                                 |
|                                                           |
|                             [ Cancel ]  [ Add ]           |
|                                                           |
+-----------------------------------------------------------+
```

#### Add Module Sub-Dialog

Clicking "Add Module" opens a nested Form Dialog (pattern #31):

```
+-- Add Module --------------------------------------------+
|                                                           |
|  Module name:  [Az                                     ]  |
|                                                           |
|                             [ Cancel ]  [ Add ]           |
|                                                           |
+-----------------------------------------------------------+
```

### Step 2 -- Empty State

When no commands or modules have been added:

```
|  Commands: (none)                                          |
|  Modules: (none)                                           |
```

The "(none)" text uses `Theme.Semantic.Muted` (dim). This is valid -- the
user can proceed with an empty profile.

### Step 3: Review and Confirm

```
+-- Create Profile -----------------------------------------+
|                                                            |
|  Step 3 of 3: Review                                       |
|  --------------------------------------------------------  |
|                                                            |
|  Name:            security                                 |
|  Language mode:   FullLanguage                              |
|  Commands:        2 (1 allow, 1 deny)                      |
|  Modules:         1                                        |
|                                                            |
|                                                            |
|  [ Cancel ]                        [ < Back ] [ Create ]   |
|                                                            |
+------------------------------------------------------------+
```

The review step shows a read-only summary. "Create" replaces "Next" on this
final step.

### After Create

```
  v Profile security created.
  File: C:\Users\jason\.boydcode\jea\security.profile
```

The success message is rendered in the conversation view after the wizard
dialog closes. The file path uses `Theme.Semantic.Muted` (dim).

## States

| State | Condition | Visual Difference |
|---|---|---|
| Step 1 | Wizard open | Name TextField + language mode ListView |
| Step 1 validation | Invalid name | Red error below Name field |
| Step 2 | After Step 1 Next | Command list + module list + Add buttons |
| Step 2 empty | No commands/modules added | Dim "(none)" for both lists |
| Add command | Add Command clicked | Nested Form Dialog with name + Allow/Deny |
| Add module | Add Module clicked | Nested Form Dialog with module name |
| Step 3 | After Step 2 Next | Read-only summary, "Create" button |
| Created | "Create" clicked | Dialog closes; success + file path in conversation |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Modal.BorderScheme` (blue border for wizard
dialog and sub-dialogs), `Theme.Semantic.Default` with `TextStyle.Bold`
(step indicator text, summary labels), `Theme.Semantic.Muted` (dim step
separator rule, "(none)" empty states, file path after creation),
`Theme.Semantic.Success` (green success checkmark, "Allow" markers),
`Theme.Semantic.Error` (red validation errors, "Error:" prefix, "Deny"
markers), `Theme.Input.Text` (white text in TextFields),
`Theme.Symbols.Rule` (step separator character).

All interaction occurs within Terminal.Gui Dialogs. No Terminal.Gui
suspension or Spectre prompts are needed.

## Interactive Elements

| Element | Type | Context |
|---|---|---|
| Name | TextField in Step 1 | Required, validated |
| Language mode | ListView in Step 1 | 4 `PSLanguageModeName` values |
| Command list | Labels in Step 2 | Showing added commands with status |
| Module list | Labels in Step 2 | Showing added modules |
| Add Command | Nested Form Dialog (pattern #31) | Name + Allow/Deny |
| Add Module | Nested Form Dialog (pattern #31) | Module name |
| Review summary | Labels in Step 3 | Read-only confirmation |

## Keyboard

| Key | Action |
|---|---|
| Tab | Move between fields within the current step |
| Shift+Tab | Move to previous field or button |
| Enter | Confirm (Next/Create when button focused) |
| Esc | Cancel entire wizard |
| Alt+B | Back (same as clicking Back button) |
| Alt+N | Next (same as clicking Next button) |

## Behavior

1. **Name resolution**: If the name is provided as a trailing argument
   (`/jea create security`), the Name field is pre-filled. Multiple words
   are joined with spaces (which will fail validation).

2. **Name validation**: Step 1's Next button validates three conditions:
   - Non-empty
   - Not a reserved name (`_global` or `builtin`)
   - Matches regex `^[a-zA-Z0-9_-]+$` (letters, numbers, hyphens, underscores)
   - Not already an existing profile name

3. **Language mode**: A 4-item ListView in Step 1 allows selecting a
   PowerShell language mode. Default selection is `FullLanguage`.

4. **Command/module management**: Step 2 shows current commands and modules
   as lists. "Add Command" and "Add Module" buttons open nested Form Dialogs.
   Each command requires a name and Allow/Deny designation.

5. **Empty profile**: The user can proceed to Step 3 without adding any
   commands or modules. The profile is saved with empty lists. This is valid.

6. **Profile save**: When "Create" is clicked on Step 3, the profile is saved
   to `~/.boydcode/jea/{name}.profile`. The file path is shown in dim after
   the success message.

7. **Back navigation**: Back preserves all entered values including added
   commands and modules.

## Edge Cases

- **Empty profile**: The user can select "Next" on Step 2 immediately without
  adding any commands or modules. The profile is saved with an empty entries
  list and empty modules list. This is valid.

- **Duplicate command names**: No deduplication is performed. If the user adds
  the same command name twice via the Add Command sub-dialog, both entries are
  stored. The `JeaProfileComposer` handles conflicts during composition.

- **Reserved names**: `_global` and `builtin` (from `BuiltInJeaProfile.Name`)
  are both reserved. Step 1 validation rejects them with a clear error.

- **Name with spaces**: The regex `^[a-zA-Z0-9_-]+$` rejects names with
  spaces. If the name is provided as a trailing argument with spaces (e.g.,
  `/jea create my profile`), the joined name "my profile" fails Step 1
  validation.

- **Cancel at any step**: Pressing Esc or clicking Cancel closes the wizard
  without saving. No confirmation is shown since nothing has been persisted
  yet.

- **Narrow terminal**: The wizard dialog uses `Dim.Percent(70)` for width.
  Step 2 command/module lists remain readable by truncating long names.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Multi-Step Wizard | #32 | Overall wizard dialog structure (3 steps) |
| Form Dialog | #31 | Add Command and Add Module sub-dialogs |
| Selection Prompt | #12 (Dialog approach) | Language mode, Allow/Deny in sub-dialog |
| Status Message | #7 | Success, error, dim file path |
| Empty State | #21 | "(none)" for empty command/module lists |
