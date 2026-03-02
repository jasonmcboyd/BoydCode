# Screen: /project edit

## Overview

An interactive Dialog for editing project settings. The Dialog uses a sidebar
ListView of configuration sections (Name, System Prompt, Docker Image, Require
Container, Directories, Done) and a content area that shows the appropriate
editing widget for the selected section. Changes are saved after each field
edit when the user clicks "Done".

This is the most complex project screen -- it uses the Edit Menu Loop pattern
(component pattern #16, Dialog approach) with nested editing widgets for each
configuration section.

**Screen IDs**: PROJ-19, PROJ-20, PROJ-21, PROJ-22, PROJ-23, PROJ-24, PROJ-25,
PROJ-26, PROJ-27

## Trigger

`/project edit [name]`

- If `name` is provided inline, it is used directly.
- If `name` is omitted and there is an active project, the active project is
  used.
- If `name` is omitted and no active project exists, a Form Dialog (pattern
  #31) prompts for the name.
- Requires an interactive terminal. Non-interactive mode shows an error.

## Layout (80 columns)

### Edit Dialog -- Name Selected

```
+-- Edit my-api --------------------------------------------+
|                                                            |
|  Fields                | Value                             |
|  ----------------------|-----------------------------------|
|  > Name                | [my-api                         ] |
|    System prompt       |                                   |
|    Docker image        |                                   |
|    Require container   |                                   |
|    Directories         |                                   |
|                        |                                   |
|                              [ Cancel ]  [ Done ]          |
|                                                            |
+------------------------------------------------------------+
```

The sidebar shows each field with a summary of its current value in
`Theme.Semantic.Muted` (dim). The active field is highlighted with
`Theme.List.SelectedBackground` and a `>` indicator.

### Edit Dialog -- With Summaries

```
+-- Edit my-api --------------------------------------------+
|                                                            |
|  Fields                | Value                             |
|  ----------------------|-----------------------------------|
|    Name                |                                   |
|  > System prompt       | [You are an expert API           ]|
|    Docker image        | [developer working on the        ]|
|    Require container   | [my-api application.             ]|
|    Directories         |                                   |
|                        |                                   |
|                              [ Cancel ]  [ Done ]          |
|                                                            |
+------------------------------------------------------------+
```

When "System prompt" is selected, the content area shows a multi-line
`TextView` pre-filled with the current prompt.

### Edit Dialog -- Docker Image Selected

```
+-- Edit my-api --------------------------------------------+
|                                                            |
|  Fields                | Docker image:                     |
|  ----------------------| [python:3.12-slim               ] |
|    Name                |                                   |
|    System prompt       | (Enter to clear)                  |
|  > Docker image        |                                   |
|    Require container   |                                   |
|    Directories         |                                   |
|                        |                                   |
|                              [ Cancel ]  [ Done ]          |
|                                                            |
+------------------------------------------------------------+
```

The hint "(Enter to clear)" uses `Theme.Semantic.Muted` (dim).

### Edit Dialog -- Require Container Selected

```
+-- Edit my-api --------------------------------------------+
|                                                            |
|  Fields                | Require container execution?      |
|  ----------------------|                                   |
|    Name                | Current: Yes                      |
|    System prompt       |                                   |
|    Docker image        |    [ No ]  [ Yes ]                |
|  > Require container   |                                   |
|    Directories         |                                   |
|                        |                                   |
|                              [ Cancel ]  [ Done ]          |
|                                                            |
+------------------------------------------------------------+
```

When no Docker image is configured, a warning appears:

```
|  > Require container   | Warning: No Docker image          |
|    Directories         | configured.                       |
```

### Edit Dialog -- Directories Selected

When directories exist:

```
+-- Edit my-api --------------------------------------------+
|                                                            |
|  Fields                | Directories:                      |
|  ----------------------|                                   |
|    Name                |  C:\Users\jason\repos\my-api  RW  |
|    System prompt       |  C:\Users\jason\data\fixtures RO  |
|    Docker image        |                                   |
|  > Require container   | [ Add ] [ Remove ] [ Change ]     |
|    Directories         |                                   |
|                        |                                   |
|                              [ Cancel ]  [ Done ]          |
|                                                            |
+------------------------------------------------------------+
```

When no directories exist:

```
|  > Directories         | No directories configured.        |
|                        |                                   |
|                        | [ Add ]                           |
```

#### Add Directory Sub-Dialog

Clicking "Add" opens a nested Form Dialog (pattern #31):

```
+-- Add Directory -----------------------------------------+
|                                                           |
|  Path:          [C:\Users\jason\docs                   ]  |
|                                                           |
|  Access level:                                            |
|    > ReadWrite                                            |
|      ReadOnly                                             |
|                                                           |
|                             [ Cancel ]  [ Add ]           |
|                                                           |
+-----------------------------------------------------------+
```

#### Remove Directory Sub-Dialog

Clicking "Remove" opens a Selection Dialog (pattern #12):

```
+-- Remove Directory --------------------------------------+
|                                                           |
|  > C:\Users\jason\repos\my-api                            |
|    C:\Users\jason\data\fixtures                           |
|                                                           |
|                             [ Cancel ]  [ Remove ]        |
|                                                           |
+-----------------------------------------------------------+
```

#### Change Access Level Sub-Dialog

Clicking "Change" opens a two-step selection:

```
+-- Change Access Level -----------------------------------+
|                                                           |
|  Directory:                                               |
|  > C:\Users\jason\repos\my-api                            |
|    C:\Users\jason\data\fixtures                           |
|                                                           |
|  New access level:                                        |
|  > ReadWrite                                              |
|    ReadOnly                                               |
|                                                           |
|                             [ Cancel ]  [ Apply ]         |
|                                                           |
+-----------------------------------------------------------+
```

### After Done

```
  v Project my-api saved.
```

The success message is rendered in the conversation view after the dialog
closes.

### Error States

```
  Error: Project my-api not found.
```

```
  Error: /project edit requires an interactive terminal.
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Edit dialog | Project loaded | Dialog with sidebar + content area |
| Name field | "Name" selected | TextField pre-filled with current name |
| System prompt field | "System prompt" selected | TextView pre-filled with current prompt (or default) |
| Docker image field | "Docker image" selected | TextField + dim clear hint |
| Require container | "Require container" selected | Current value label + Yes/No buttons; warning if no image |
| Directories | "Directories" selected | Directory list + Add/Remove/Change buttons |
| Directories empty | "Directories" selected, none exist | Dim "No directories configured" + Add button |
| Add directory | Add button clicked | Nested Form Dialog with path + access level |
| Remove directory | Remove button clicked | Nested Selection Dialog |
| Change access | Change button clicked | Nested Dialog with directory + access level selection |
| Saved | "Done" clicked | Dialog closes; success message in conversation view |
| Not found | Project does not exist | Red error, command exits |
| Non-interactive | Terminal is not interactive | Red error, command exits |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Modal.BorderScheme` (blue border for all
dialogs), `Theme.List.SelectedBackground` and `Theme.List.SelectedText`
(highlighted sidebar row), `Theme.Semantic.Success` (green success checkmarks,
"ReadWrite" labels, "Yes" require-container value), `Theme.Semantic.Warning`
(yellow "ReadOnly" labels, "Warning:" prefix, empty directory messages),
`Theme.Semantic.Error` (red "Error:" prefix), `Theme.Semantic.Muted` (dim
hints, "No directories configured", default/empty summaries),
`Theme.Input.Text` (white text in TextFields/TextViews).

All interaction occurs within Terminal.Gui Dialogs. No Terminal.Gui
suspension or Spectre prompts are needed.

## Interactive Elements

| Element | Type | Context |
|---|---|---|
| Edit sidebar | ListView in Dialog (pattern #16) | Left panel, persistent |
| Name field | TextField in content area | "Name" selected |
| System prompt | TextView in content area | "System prompt" selected |
| Docker image | TextField in content area | "Docker image" selected |
| Require container | Yes/No buttons in content area | "Require container" selected |
| Directory list | Labels in content area | "Directories" selected |
| Add directory | Nested Form Dialog (pattern #31) | "Add" button clicked |
| Remove directory | Nested Selection Dialog (pattern #12) | "Remove" button clicked |
| Change access level | Nested Dialog (pattern #12) | "Change" button clicked |

## Keyboard

| Key | Action |
|---|---|
| Up / Down | Navigate sidebar items |
| Tab | Move focus between sidebar, content area, and buttons |
| Shift+Tab | Move focus backward |
| Enter | Confirm field value / activate button |
| Esc | Cancel all changes and close dialog |
| Alt+D | Click Done button (apply changes and close) |

## Behavior

1. **Sidebar navigation**: The left ListView shows 5 field items (Name,
   System Prompt, Docker Image, Require Container, Directories). When the
   user selects a sidebar item, the content area on the right updates with
   the appropriate editing widget. The sidebar items show current value
   summaries in dim text.

2. **Auto-save**: When "Done" is clicked, all accumulated changes are saved
   via `_projectRepository.SaveAsync`. The dialog closes and a success message
   appears in the conversation view.

3. **Session context refresh**: If the edited project is the active project,
   `RefreshSessionContext` is called after save. This re-resolves directories,
   updates the `DirectoryGuard`, and rebuilds the session's system prompt so
   the LLM sees changes on the next turn.

4. **Stale settings warning**: If Docker image or require-container settings
   are changed on the active project, `_ui.StaleSettingsWarning` is set to
   prompt the user to run `/context refresh`.

5. **Nested dialogs**: Directory operations (Add, Remove, Change) open nested
   dialogs on top of the edit dialog. These are modal -- the edit dialog is
   not dismissable while a nested dialog is open. After the nested dialog
   closes, the edit dialog's content area refreshes to show updated directory
   list.

6. **Cancel**: Pressing Esc or clicking Cancel closes the dialog without
   saving any changes.

## Edge Cases

- **Non-interactive terminal**: The command requires `_ui.IsInteractive`. If
  false, shows an error and exits. No fallback is provided because the edit
  dialog requires interactive input.

- **Empty directories on remove/change**: If the user clicks "Remove" or
  "Change" but no directories exist, the buttons are disabled (grayed out).

- **Concurrent name resolution**: The name is resolved once at the start.
  If the active project name is used and the project is deleted by another
  process during the edit session, subsequent saves may recreate the project
  file.

- **Long Docker image names**: The TextField in the content area allows
  horizontal scrolling for values that exceed the visible width.

- **Narrow terminal**: The Dialog uses proportional sizing (`Dim.Percent`).
  Below 80 columns, the sidebar labels may truncate but remain navigable.
  The content area adjusts width via `Dim.Fill`.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Edit Menu Loop | #16 (Dialog approach) | Main dialog with sidebar + content area |
| Form Dialog | #31 | TextField/TextView input, Add Directory sub-dialog |
| Selection Prompt | #12 (Dialog approach) | Remove Directory, Change Access sub-dialogs |
| Confirmation Prompt | #14 (MessageBox approach) | Require container Yes/No |
| Status Message | #7 | Success, error, warning messages |
| Empty State | #21 | "No directories configured" |
