# Screen: /project create

## Overview

Creates a new named project using a Multi-Step Wizard Dialog (component
pattern #32). The wizard guides the user through project creation in 4 steps:
(1) Name + directories, (2) System prompt, (3) Container settings, (4) Review
and confirm. Steps 2-3 are optional and can be skipped. In non-interactive
mode, creates a bare project with no configuration.

**Screen IDs**: PROJ-02, PROJ-03, PROJ-04, PROJ-05, PROJ-06, PROJ-07, PROJ-08,
PROJ-09, PROJ-10, PROJ-11, PROJ-12

## Trigger

`/project create [name]`

- If `name` is provided inline, the wizard opens at Step 1 with the name
  pre-filled.
- If `name` is omitted and the terminal is interactive, the wizard opens at
  Step 1 with an empty name field.
- If `name` is omitted and the terminal is non-interactive, a usage hint is
  shown and the command exits.

## Layout (80 columns)

### Step 1: Name and Directories

```
+-- Create Project -----------------------------------------+
|                                                            |
|  Step 1 of 4: Name and Directories                         |
|  --------------------------------------------------------  |
|                                                            |
|  Name:  [my-api                                         ]  |
|                                                            |
|  Directories:                                              |
|    C:\Users\jason\source\repos\my-api          ReadWrite   |
|    C:\Users\jason\data\fixtures                ReadOnly    |
|                                                            |
|                     [ Add Directory ]                      |
|                                                            |
|  [ Cancel ]                                    [ Next > ]  |
|                                                            |
+------------------------------------------------------------+
```

The Name field is required -- the Next button validates non-empty. Directories
are optional at this step.

#### Add Directory Sub-Dialog

Clicking "Add Directory" opens a nested Form Dialog (pattern #31):

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

After adding, the directory appears in the list on Step 1. Directories can be
removed by selecting them and pressing Delete, or via a context action.

### Step 1 -- With Validation Error

```
+-- Create Project -----------------------------------------+
|                                                            |
|  Step 1 of 4: Name and Directories                         |
|  --------------------------------------------------------  |
|                                                            |
|  Name:  [                                               ]  |
|         Name cannot be empty.                              |
|                                                            |
|  Directories: (none)                                       |
|                                                            |
|                     [ Add Directory ]                      |
|                                                            |
|  [ Cancel ]                                    [ Next > ]  |
|                                                            |
+------------------------------------------------------------+
```

### Step 1 -- Name Already Exists

```
|  Name:  [my-api                                         ]  |
|         Project 'my-api' already exists.                   |
```

The duplicate check runs when Next is clicked. The error message uses
`Theme.Semantic.Error` (bright red).

### Step 2: System Prompt

```
+-- Create Project -----------------------------------------+
|                                                            |
|  Step 2 of 4: System Prompt                                |
|  --------------------------------------------------------  |
|                                                            |
|  Custom system prompt (leave empty for default):           |
|  [You are an expert API developer working on            ]  |
|  [the my-api application.                               ]  |
|  [                                                      ]  |
|  [                                                      ]  |
|                                                            |
|                                                            |
|  [ Cancel ]                          [ < Back ] [ Next > ] |
|                                                            |
+------------------------------------------------------------+
```

The `TextView` is pre-filled with `Project.DefaultSystemPrompt` in dim text
as a placeholder. If the user leaves it unchanged or clears it,
`project.SystemPrompt` is set to null (meaning "use default").

### Step 3: Container Settings

```
+-- Create Project -----------------------------------------+
|                                                            |
|  Step 3 of 4: Container Settings                           |
|  --------------------------------------------------------  |
|                                                            |
|  Docker image:  [python:3.12-slim                       ]  |
|                 (optional)                                 |
|                                                            |
|  Require container execution?                              |
|                       [ No ]  [ Yes ]                      |
|                                                            |
|                                                            |
|  [ Cancel ]                          [ < Back ] [ Next > ] |
|                                                            |
+------------------------------------------------------------+
```

Both fields are optional. The Docker image field can be left empty. The
"Require container" buttons default to No.

### Step 4: Review and Confirm

```
+-- Create Project -----------------------------------------+
|                                                            |
|  Step 4 of 4: Review                                       |
|  --------------------------------------------------------  |
|                                                            |
|  Name:               my-api                                |
|  Directories:        2 configured                          |
|  System prompt:      custom                                |
|  Docker image:       python:3.12-slim                      |
|  Require container:  Yes                                   |
|                                                            |
|                                                            |
|  [ Cancel ]                        [ < Back ] [ Create ]   |
|                                                            |
+------------------------------------------------------------+
```

The review step shows a read-only summary of all entered values. The "Next"
button is replaced with "Create" on this final step.

### Step 4 -- Minimal Configuration

```
|  Name:               my-api                                |
|  Directories:        none                                  |
|  System prompt:      default                               |
|  Docker image:       none                                  |
|  Require container:  No                                    |
```

Values that were skipped or left at defaults show in `Theme.Semantic.Muted`
(dim): "none", "default", "No".

### After Create

```
  v Project my-api created.
```

The success message is rendered in the conversation view after the wizard
dialog closes. If the user skipped all configuration:

```
  v Project my-api created.
  Tip: Use /project edit my-api to configure later.
```

The tip uses `Theme.Semantic.Muted` (dim).

### Non-Interactive Usage Hint

```
  Usage: /project create <name>
```

In non-interactive mode with a name argument, the project is created as a
bare entity with no configuration wizard.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Step 1 | Wizard open | Name TextField + directory list + Add button |
| Step 1 validation | Empty name or duplicate | Red error below Name field |
| Step 2 | After Step 1 Next | TextView for system prompt |
| Step 3 | After Step 2 Next | Docker image TextField + Require container buttons |
| Step 4 | After Step 3 Next | Read-only summary, "Create" button |
| Created | "Create" clicked | Dialog closes; success in conversation view |
| Non-interactive, no name | Non-interactive terminal, no name arg | Yellow usage hint |
| Non-interactive, with name | Non-interactive terminal, name arg | Bare project created, success message |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Modal.BorderScheme` (blue border for wizard
dialog and sub-dialogs), `Theme.Semantic.Default` with `TextStyle.Bold` (step
indicator text), `Theme.Semantic.Muted` (dim step separator rule, "none" /
"default" / "No" in review, tip text, "(optional)" hints),
`Theme.Semantic.Success` (green success checkmark, "ReadWrite" label),
`Theme.Semantic.Warning` (yellow "ReadOnly" label, "Usage:" prefix),
`Theme.Semantic.Error` (red validation errors and "Error:" prefix),
`Theme.Input.Text` (white text in TextFields/TextViews),
`Theme.Symbols.Rule` (step separator character).

All interaction occurs within Terminal.Gui Dialogs. No Terminal.Gui
suspension or Spectre prompts are needed.

## Interactive Elements

| Element | Type | Context |
|---|---|---|
| Name | TextField in Step 1 | Required, non-empty validation |
| Directory list | Labels in Step 1 | Showing added directories |
| Add Directory | Nested Form Dialog (pattern #31) | Path + access level input |
| System prompt | TextView in Step 2 | Multi-line, optional |
| Docker image | TextField in Step 3 | Optional |
| Require container | Yes/No buttons in Step 3 | Default: No |
| Review summary | Labels in Step 4 | Read-only confirmation |

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

1. **Wizard structure**: The dialog progresses through 4 steps. The step
   indicator at the top shows "Step N of 4: {title}". Back and Next buttons
   navigate between steps. Cancel exits without creating.

2. **Name validation**: Step 1's Next button validates that the name is
   non-empty and does not match an existing project. Validation errors
   appear inline below the field in `Theme.Semantic.Error`.

3. **Directory management**: Directories are added via a nested Form Dialog.
   Each directory entry shows the path and access level. Directories can be
   removed from the list before creation.

4. **System prompt**: Step 2 shows a `TextView` for multi-line input. If left
   empty, `project.SystemPrompt` is set to null (meaning "use default").

5. **Container settings**: Step 3 has an optional Docker image field and a
   Yes/No toggle for requiring container execution.

6. **Review**: Step 4 shows a read-only summary of all values. "Create"
   replaces the "Next" button. Clicking "Create" saves the project and
   closes the wizard.

7. **Back navigation**: Back preserves all entered values. The user can
   navigate freely between steps to revise inputs.

8. **Non-interactive**: If `_ui.IsInteractive` is false and no name argument
   is provided, shows the usage hint. If a name is given, the project is
   created as a bare entity without the wizard.

## Edge Cases

- **Name with special characters**: The name validation regex
  `^[a-zA-Z0-9_-]+$` rejects names with spaces or special characters. The
  validation error appears inline on Step 1.

- **Default system prompt acceptance**: If the user leaves the system prompt
  at the default or clears it, `project.SystemPrompt` is set to null. No
  distinction is made between "cleared" and "never entered".

- **Narrow terminal (< 80 columns)**: The wizard dialog uses `Dim.Percent(70)`
  for width. The step indicator may wrap to two lines. Field labels and
  TextFields adjust via `Dim.Fill`.

- **Cancel confirmation**: If the user has entered data in any step and
  presses Esc, a confirmation MessageBox (pattern #14) asks "Discard changes?"
  with Yes/No buttons. If no data has been entered, the wizard closes
  immediately.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Multi-Step Wizard | #32 | Overall wizard dialog structure |
| Form Dialog | #31 | Add Directory sub-dialog, field layout |
| Confirmation Prompt | #14 (MessageBox approach) | Cancel confirmation when data entered |
| Status Message | #7 | Success, error, usage, dim messages |
| Empty State | #21 | Not used (project is always created on confirm) |
