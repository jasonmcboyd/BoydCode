# Screen: /jea assign and /jea unassign

## Overview

Assigns or unassigns a JEA profile to/from the currently active project. The
`_global` profile cannot be assigned (it is always implicitly included during
composition). Both commands require an active non-ambient project.

**Screen IDs**: JEA-31 through JEA-43

## Trigger

- `/jea assign [name]` -- assigns a profile to the active project
- `/jea unassign [name]` -- removes a profile from the active project

If `name` is provided, it is used directly. Otherwise, a selection prompt
appears.

## Layout (80 columns)

### Assign -- Full Flow

    Select profile to assign:
    > security
      linting
      api-guard

      v Profile security assigned to project my-api.

### Assign -- Inline Name

      v Profile security assigned to project my-api.

### Assign -- Already Assigned

    Profile security is already assigned to project my-api.

### Unassign -- Full Flow

    Select profile to unassign:
    > security
      linting

      v Profile security unassigned from project my-api.

### Unassign -- Inline Name

      v Profile linting unassigned from project my-api.

### Error States

**No active project (assign and unassign):**

    Error: No project selected. Use /project create or --project to select a
    project first.

**Profile not found (assign):**

    Error: Profile nonexistent not found.

**Project not found (assign):**

    Error: Project my-api not found.

**Not assigned (unassign):**

    Error: Profile security is not assigned to project my-api.

**No profiles assigned (unassign):**

    No JEA profiles assigned to project my-api.

**No assignable profiles (assign):**

    No profiles available to assign.
    Create one with /jea create <name>

## States -- Assign

| State | Condition | Visual Difference |
|---|---|---|
| Profile selection | No name arg, assignable profiles exist | SelectionPrompt (excludes `_global`) |
| Success | Profile assigned | Green success with bold profile and project names |
| Already assigned | Profile is already on the project | Plain text message with bold names (no error prefix) |
| No project | No active project or ambient `_default` | Red error with guidance |
| Profile not found | Profile does not exist | Red error with bold profile name |
| Project not found | Active project does not exist | Red error with bold project name |
| No profiles available | No assignable profiles (only `_global` exists) | Plain text + dim hint |

## States -- Unassign

| State | Condition | Visual Difference |
|---|---|---|
| Profile selection | No name arg, project has assigned profiles | SelectionPrompt of assigned profiles |
| Success | Profile unassigned | Green success with bold profile and project names |
| Not assigned | Profile not in project's list | Red error with bold profile and project names |
| No project | No active project or ambient `_default` | Red error with guidance |
| Project not found | Active project does not exist | Red error with bold project name |
| No profiles assigned | Project has no assigned profiles | Plain text with bold project name |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green]` | success-green | Success checkmark |
| `[red]` | error-red | "Error:" prefix |
| `[bold]` | bold (2.2) | Profile name and project name in all messages, `/project create` and `--project` in guidance |
| `[dim]` | dim (2.2) | Hint for creating profiles |
| `Color.Green` | Spectre color (1.5) | SelectionPrompt highlight style |

## Interactive Elements

| Element | Type | Label | Context |
|---|---|---|---|
| Assign selection | `SpectreHelpers.Select` | `Select profile to assign:` | Assign, no name arg |
| Unassign selection | `SpectreHelpers.Select` | `Select profile to unassign:` | Unassign, no name arg |

## Behavior -- Assign

1. **Project guard**: Checks that `_activeProject.Name` is set and is not
   `_default`. If the guard fails, a red error is shown with guidance to use
   `/project create` or `--project`.

2. **Profile resolution**: If the name is provided inline, it is used directly.
   Otherwise, all profile names are listed and filtered to exclude `_global`.
   If no assignable profiles remain, an empty state with a hint is shown.

3. **Profile existence check**: The profile is loaded via `_store.LoadAsync`.
   If null, a not-found error is shown.

4. **Project loading**: The active project is loaded via
   `_projectRepository.LoadAsync`. If null (should not happen in normal
   operation), a not-found error is shown.

5. **Duplicate check**: If `project.Execution.JeaProfiles` already contains
   the profile name (case-insensitive), an "already assigned" message is shown
   without an error prefix.

6. **Assignment**: The profile name is added to `project.Execution.JeaProfiles`
   and the project is saved. `project.Execution` is initialized to a new
   `ExecutionConfig` if null.

## Behavior -- Unassign

1. **Project guard**: Same as assign -- requires an active non-ambient project.

2. **Project loading**: The active project is loaded immediately (before
   profile resolution) to check the current assignment list.

3. **Empty check**: If `project.Execution.JeaProfiles` is empty, a message is
   shown and the command exits.

4. **Profile resolution**: If the name is provided inline, it is used directly.
   Otherwise, a selection prompt shows the project's currently assigned profiles.

5. **Removal**: `assigned.Remove(name)` removes the profile name. If the
   name is not found (case-sensitive match), a "not assigned" error is shown.

6. **Save**: The project is saved after removal.

## Edge Cases

- **Ambient project**: Both assign and unassign refuse to operate on the
  `_default` ambient project. The error message guides users to create a named
  project first.

- **`_global` profile**: The assign selection prompt excludes `_global` because
  it is always implicitly included during composition. If the user provides
  `_global` as an inline argument, the profile will be "assigned" but this has
  no functional effect (the composer already includes it).

- **Case sensitivity**: The duplicate check in assign uses
  `StringComparer.OrdinalIgnoreCase`. The removal in unassign uses
  `List.Remove` which is case-sensitive. This means a profile assigned as
  "Security" cannot be unassigned with "security".

- **Stale project state**: If the project is modified between loading and
  saving (unlikely in an interactive session), the save overwrites with the
  current state.

- **Profile deleted after assignment**: If a profile is assigned to a project
  and then deleted via `/jea delete`, the assignment remains in the project's
  config. The `JeaProfileComposer` handles missing profiles gracefully during
  composition.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Selection Prompt | Section 5 | Profile selection for assign and unassign |
| Status Message | Section 1 | Success, error messages |
| Empty State | Section 13 | "No profiles available/assigned" messages |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| Assign flow | `Commands/JeaSlashCommand.cs` | `HandleAssignAsync` | 529-585 |
| Assign project guard | `Commands/JeaSlashCommand.cs` | `HandleAssignAsync` | 531-536 |
| Assign profile resolution | `Commands/JeaSlashCommand.cs` | `HandleAssignAsync` | 538-558 |
| Assign duplicate check | `Commands/JeaSlashCommand.cs` | `HandleAssignAsync` | 576-579 |
| Unassign flow | `Commands/JeaSlashCommand.cs` | `HandleUnassignAsync` | 591-632 |
| Unassign project guard | `Commands/JeaSlashCommand.cs` | `HandleUnassignAsync` | 593-598 |
| Unassign empty check | `Commands/JeaSlashCommand.cs` | `HandleUnassignAsync` | 607-612 |
| Unassign removal | `Commands/JeaSlashCommand.cs` | `HandleUnassignAsync` | 624-628 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
