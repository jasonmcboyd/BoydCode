# Screen: /jea edit

## Overview

An interactive Dialog for editing an existing JEA profile. The user sees a
sidebar listing editable fields (Name, Description, Add Command, Remove
Command, Add Module, Remove Module) and a content area showing the appropriate
editing widget for the selected field. Changes are accumulated in memory and
saved when the user clicks "Done".

**Screen IDs**: JEA-14, JEA-15, JEA-16, JEA-17, JEA-18, JEA-19, JEA-20,
JEA-21, JEA-22

## Trigger

`/jea edit [name]`

- If `name` is provided inline, it is used directly.
- If `name` is omitted, a selection Dialog lists all profiles (component
  pattern #12 Selection Prompt, Dialog approach).

## Layout (80 columns)

### Profile Selection (when name omitted)

```
+-- Select Profile ----------------------------------------+
|                                                           |
|    _global                                                |
|  > security                                               |
|    network-ops                                            |
|                                                           |
|                             [ Cancel ]  [ Ok ]            |
|                                                           |
+-----------------------------------------------------------+
```

### Edit Dialog -- Name Selected

```
+-- Edit _global -------------------------------------------+
|                                                            |
|  Actions               | Value                             |
|  ----------------------|-----------------------------------|
|  > Name                | [_global                        ] |
|    Description         |                                   |
|    Add command         |                                   |
|    Remove command      |                                   |
|    Add module          |                                   |
|    Remove module       |                                   |
|                        |                                   |
|                              [ Cancel ]  [ Done ]          |
|                                                            |
+------------------------------------------------------------+
```

### Edit Dialog -- Description Selected

```
+-- Edit _global -------------------------------------------+
|                                                            |
|  Actions               | Value                             |
|  ----------------------|-----------------------------------|
|    Name                |                                   |
|  > Description         | [Default security profile       ] |
|    Add command         |                                   |
|    Remove command      |                                   |
|    Add module          |                                   |
|    Remove module       |                                   |
|                        |                                   |
|                              [ Cancel ]  [ Done ]          |
|                                                            |
+------------------------------------------------------------+
```

### Edit Dialog -- Add Command Selected

When "Add command" is selected, the content area shows a TextField for the
command name and a ListView for Allow/Deny:

```
+-- Edit _global -------------------------------------------+
|                                                            |
|  Actions               | Command name:                     |
|  ----------------------| [Invoke-WebRequest              ] |
|    Name                |                                   |
|    Description         | Action:                           |
|  > Add command         |   Allow                           |
|    Remove command      | > Deny                            |
|    Add module          |                                   |
|    Remove module       |              [ Add ]              |
|                        |                                   |
|                              [ Cancel ]  [ Done ]          |
|                                                            |
+------------------------------------------------------------+
```

After clicking "Add", a success message appears briefly in the content area:

```
|  > Add command         | v Allow Invoke-WebRequest         |
```

### Edit Dialog -- Remove Command Selected

When "Remove command" is selected and commands exist, the content area shows
a ListView of current commands:

```
+-- Edit _global -------------------------------------------+
|                                                            |
|  Actions               | Select command to remove:         |
|  ----------------------|                                   |
|    Name                |   Get-Process        Allow        |
|    Description         | > Stop-Service       Deny         |
|    Add command         |   Invoke-WebRequest  Allow        |
|  > Remove command      |                                   |
|    Add module          |          [ Remove ]               |
|    Remove module       |                                   |
|                        |                                   |
|                              [ Cancel ]  [ Done ]          |
|                                                            |
+------------------------------------------------------------+
```

### Edit Dialog -- Remove Command Empty

When no commands exist:

```
|  > Remove command      | No commands to remove.            |
```

The message uses `Theme.Semantic.Warning` (yellow).

### Edit Dialog -- Add Module Selected

```
+-- Edit _global -------------------------------------------+
|                                                            |
|  Actions               | Module name:                      |
|  ----------------------| [Az                             ] |
|    Name                |                                   |
|    Description         |              [ Add ]              |
|  > Add module          |                                   |
|    Remove command      |                                   |
|    Add module          |                                   |
|    Remove module       |                                   |
|                        |                                   |
|                              [ Cancel ]  [ Done ]          |
|                                                            |
+------------------------------------------------------------+
```

### Edit Dialog -- Remove Module Selected

When modules exist:

```
|  > Remove module       | Select module to remove:          |
|                        |   PSScriptAnalyzer                |
|                        | > Az                              |
|                        |          [ Remove ]               |
```

When no modules exist:

```
|  > Remove module       | No modules to remove.             |
```

### Save and Exit

After clicking "Done", changes are saved:

```
  v Profile _global saved.
  File: C:\Users\jason\.boydcode\jea\_global.profile
```

The success message is rendered in the conversation view (not in the dialog).
The file path uses `Theme.Semantic.Muted` (dim).

### Not Found

```
  Error: Profile _global not found.
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Profile selection | No name argument | Dialog with ListView of all profiles |
| Edit dialog | Profile loaded | Dialog with sidebar + content area |
| Name field | "Name" selected in sidebar | TextField pre-filled with current name |
| Description field | "Description" selected in sidebar | TextField pre-filled with current description |
| Add command | "Add command" selected | TextField + Allow/Deny ListView + Add button |
| Remove command | "Remove command" selected, commands exist | ListView of commands + Remove button |
| Remove command empty | "Remove command" selected, no commands | Yellow "No commands to remove" |
| Add module | "Add module" selected | TextField + Add button |
| Remove module | "Remove module" selected, modules exist | ListView of modules + Remove button |
| Remove module empty | "Remove module" selected, no modules | Yellow "No modules to remove" |
| Saved | "Done" clicked | Dialog closes; success + file path in conversation view |
| Not found | Profile does not exist | Red error, command exits |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Modal.BorderScheme` (blue border for the
dialog), `Theme.List.SelectedBackground` and `Theme.List.SelectedText`
(highlighted sidebar row), `Theme.Semantic.Success` (green success checkmarks
and "Allow" markers), `Theme.Semantic.Error` (red "Error:" prefix and "Deny"
markers), `Theme.Semantic.Warning` (yellow empty state warnings),
`Theme.Semantic.Muted` (dim file path after save), `Theme.Input.Text`
(white text in TextFields).

All interaction occurs within Terminal.Gui Dialogs. No Terminal.Gui
suspension or Spectre prompts are needed.

## Interactive Elements

| Element | Type | Context |
|---|---|---|
| Profile selection | Dialog + ListView (pattern #12) | No name argument |
| Edit sidebar | ListView in Dialog (pattern #16) | Left panel, persistent |
| Name field | TextField in content area | "Name" selected |
| Description field | TextField in content area | "Description" selected |
| Command name | TextField in content area | "Add command" selected |
| Allow/Deny | ListView in content area | "Add command" selected |
| Command to remove | ListView in content area | "Remove command" selected |
| Module name | TextField in content area | "Add module" selected |
| Module to remove | ListView in content area | "Remove module" selected |

## Keyboard

| Key | Action |
|---|---|
| Up / Down | Navigate sidebar items |
| Tab | Move focus from sidebar to content area |
| Shift+Tab | Move focus from content area to sidebar |
| Enter | Confirm field value / activate Add or Remove button |
| Esc | Cancel all changes and close dialog |
| Alt+D | Click Done button (apply changes and close) |

## Behavior

1. **Profile loading**: The profile is loaded once at the start. Its entries
   and modules are copied into mutable lists for editing.

2. **Sidebar navigation**: The left ListView shows 6 fixed action items (Name,
   Description, Add Command, Remove Command, Add Module, Remove Module). When
   the user selects a sidebar item, the content area on the right updates with
   the appropriate editing widget.

3. **In-memory editing**: All changes are accumulated in the `entries` and
   `modules` lists, the `name`, and `description` variables. Nothing is saved
   until the user clicks "Done".

4. **Add command flow**: The content area shows a TextField for the command
   name and a 2-item ListView for Allow/Deny. An "Add" button confirms the
   entry. After adding, the content area shows a brief success message, then
   resets for the next command.

5. **Remove command flow**: The content area shows a ListView of current
   commands with their Allow/Deny status. A "Remove" button removes the
   selected command. If no commands exist, a yellow warning is shown instead.

6. **Profile save**: When "Done" is clicked, a new `JeaProfile` record is
   created from the edited state and saved via `_store.SaveAsync`. The dialog
   closes and a success message with the file path is rendered in the
   conversation view.

7. **Cancel**: Pressing Esc or clicking Cancel closes the dialog without
   saving. No confirmation prompt is shown (changes are discarded silently).

8. **Profile selection**: When no name argument is provided,
   `PromptProfileSelectionAsync` opens a Dialog with a ListView of all profile
   names and returns the selected one, or null if cancelled / no profiles exist.

## Edge Cases

- **Editing `_global`**: The global profile can be edited freely. There is no
  special protection. Changes take effect on the next session.

- **Empty actions**: "Remove command" and "Remove module" show a yellow warning
  in the content area if the respective list is empty. The sidebar remains
  navigable.

- **Duplicate commands**: The same command name can be added multiple times.
  No deduplication check is performed during editing.

- **No profiles for selection**: If `PromptProfileSelectionAsync` finds no
  profiles, it renders "No JEA profiles found" in the conversation view and
  returns null. The edit flow exits gracefully.

- **Unsaved changes on cancel**: If the user presses Esc or clicks Cancel,
  changes are lost since they are only saved on "Done". No confirmation
  dialog is shown because the edit dialog itself is the confirmation context.

- **Narrow terminal**: The Dialog uses `Dim.Percent(80)` for width and
  `Dim.Percent(70)` for height. The sidebar and content area use proportional
  widths. Below 80 columns, the content area may truncate long command names.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Edit Menu Loop | #16 (Dialog approach) | Main dialog with sidebar + content area |
| Selection Prompt | #12 (Dialog approach) | Profile selection, Allow/Deny, command/module lists |
| Form Dialog | #31 | TextField input for name, description, command, module |
| Status Message | #7 | Success, error, warning messages |
| Empty State | #21 | Yellow warnings for empty lists |
