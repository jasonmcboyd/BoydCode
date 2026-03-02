# Screen: /project delete

## Overview

Deletes a named project after showing a summary of what will be lost and
requesting confirmation. The ambient `_default` project cannot be deleted.

**Screen IDs**: PROJ-28, PROJ-29, PROJ-30, PROJ-31, PROJ-32, PROJ-33

## Trigger

`/project delete [name]`

- If `name` is provided inline, it is used directly.
- If `name` is omitted and the terminal is interactive, an `Ask<string>` prompt
  appears.
- If `name` is omitted and the terminal is non-interactive, a usage hint is
  shown.

## Layout (80 columns)

### Delete Confirmation

    (blank line)
      This will delete project my-api:
        - 3 directory mapping(s)
        - Custom system prompt
        - Docker image (python:3.12-slim)
        - Container execution required
    (blank line)
    Delete project my-api? [y/N] y
      v Project my-api deleted.

### Minimal Project (no configuration)

    (blank line)
      This will delete project empty-project:
        No custom configuration.
    (blank line)
    Delete project empty-project? [y/N] y
      v Project empty-project deleted.

### Cancelled

    Delete project my-api? [y/N] n
    Cancelled.

### Error States

    Error: Cannot delete the ambient project _default.

    Error: Project nonexistent not found.

    Usage: /project delete <name>

## States

| State | Condition | Visual Difference |
|---|---|---|
| Confirmation with details | Project has config (dirs, prompt, Docker, etc.) | Bulleted list of what will be deleted + confirm prompt |
| Confirmation minimal | No custom configuration | "No custom configuration." in dim instead of bullet list |
| Deleted | User confirms | Green success with bold entity name |
| Cancelled | User declines (or non-interactive) | Dim "Cancelled." |
| Ambient error | Name is `_default` | Red error mentioning ambient project |
| Not found | Project does not exist | Red error with bold entity name |
| Non-interactive, no name | No name argument, non-interactive | Yellow usage hint |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Semantic.Success` (green success checkmark),
`Theme.Semantic.Error` (red "Error:" prefix), `Theme.Semantic.Warning` (yellow
"Usage:" prefix), `Theme.Semantic.Muted` (dim bullet in detail list, "No custom
configuration.", "Cancelled.").

The confirmation prompt runs during Terminal.Gui suspension via
`SpectreHelpers.Confirm`.

## Interactive Elements

| Element | Type | Label | Default |
|---|---|---|---|
| Project name | `SpectreHelpers.Ask<string>` | `Project name:` | No default (when no name arg) |
| Delete confirmation | `SpectreHelpers.Confirm` | `Delete project [bold]{name}[/]?` | Default: No |

## Behavior

1. **Name resolution**: If the name is provided as a trailing argument, it is
   used directly. Otherwise, an interactive prompt asks for the name.

2. **Ambient guard**: The `_default` ambient project is checked first, before
   loading. If the name matches `Project.AmbientProjectName`, a red error is
   shown and the command exits.

3. **Existence check**: The project is loaded via `_projectRepository.LoadAsync`.
   If null, a not-found error is shown.

4. **Detail summary**: A list of what the project contains is assembled:
   - Directory mapping count (e.g., "3 directory mapping(s)")
   - "Custom system prompt" (if `SystemPrompt` is not null)
   - "Docker image ({image})" (if `DockerImage` is not null)
   - "Container execution required" (if `RequireContainer` is true)

   Each detail is rendered with a dim `-` bullet and 4-space indent. If the
   project has no custom configuration at all, "No custom configuration." is
   shown in dim instead.

5. **Confirmation gate**: A confirm prompt defaults to No. If the user declines
   or the terminal is non-interactive, "Cancelled." is shown.

6. **Deletion**: `_projectRepository.DeleteAsync` removes the project. A green
   success message confirms the deletion.

## Edge Cases

- **Non-interactive terminal**: If no name argument is provided and the
  terminal is non-interactive, shows the usage hint. If a name is provided but
  the terminal is non-interactive, the confirmation prompt defaults to No (via
  `SpectreHelpers.Confirm`), so the project is not deleted.

- **Deleting the active project**: The command does not check whether the
  project being deleted is the currently active project. The `ActiveProject`
  singleton retains its reference, which becomes stale. The user would need to
  start a new session to see the effect.

- **Empty detail list**: When a project has zero configuration, the detail
  section shows a single dim line instead of an empty bulleted list.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Delete Confirmation | #15 | Summary + confirm prompt pattern |
| Status Message | #7 | Success, error, cancelled, usage messages |
| Confirmation Prompt | #14 | Delete confirmation with default No |

