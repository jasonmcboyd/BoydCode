# Screen: /project edit

## Overview

An interactive menu loop for editing project settings. The user selects from
a list of configuration sections (Directories, System prompt, Docker image,
Require container), edits the selected section, and returns to the menu. The
loop continues until the user selects "Done". Changes are saved after each
edit action.

This is the most complex project screen -- it uses the Edit Menu Loop pattern
with nested sub-screens for each configuration section.

**Screen IDs**: PROJ-19, PROJ-20, PROJ-21, PROJ-22, PROJ-23, PROJ-24, PROJ-25,
PROJ-26, PROJ-27

## Trigger

`/project edit [name]`

- If `name` is provided inline, it is used directly.
- If `name` is omitted and there is an active project, the active project is
  used.
- If `name` is omitted and no active project exists, an `Ask<string>` prompt
  appears.
- Requires an interactive terminal. Non-interactive mode shows an error.

## Layout (80 columns)

### Edit Menu

    Edit my-api:
    > Directories          3 configured
      System prompt        custom
      Docker image         python:3.12-slim
      Require container    Yes
      Done

The menu items show padded labels (20 characters) followed by a summary of
the current value. Summaries use dim text for defaults/empty states:

    Edit my-api:
    > Directories          none
      System prompt        default
      Docker image         none
      Require container    No
      Done

### Edit Directories Sub-Screen

When "Directories" is selected and directories exist:

    #   Path                                         Access
    ────────────────────────────────────────────────────────────────────────
    1   C:\Users\jason\source\repos\my-api           ReadWrite
    2   C:\Users\jason\data\fixtures                 ReadOnly

      Directory action:
    > Add directory
      Remove directory
      Change access level
      Back

When adding:

      Directory path: C:\Users\jason\docs
      Access level:
    > ReadWrite
      ReadOnly
      v Added C:\Users\jason\docs (ReadWrite)

When removing:

      Select directory to remove:
    > C:\Users\jason\data\fixtures
      C:\Users\jason\source\repos\my-api
      v Removed C:\Users\jason\data\fixtures

When changing access:

      Select directory:
    > C:\Users\jason\source\repos\my-api
      New access level:
    > ReadWrite
      ReadOnly
      v Changed C:\Users\jason\source\repos\my-api to ReadOnly

When no directories exist:

      No directories configured.
      Directory action:
    > Add directory
      ...

### Edit System Prompt Sub-Screen

    The system prompt is always prefixed with the project name.
      Current: You are an expert API developer...

      System prompt:
    > Set new prompt
      Reset to default
      Back

After setting new prompt:

      New system prompt: You are a security-focused code reviewer.
      v System prompt updated.

With default prompt:

    The system prompt is always prefixed with the project name.
      Current: (default) You are a helpful AI coding assistant...

      System prompt:
    > Set new prompt
      Back

Note: "Reset to default" only appears when a custom prompt is set.

### Edit Docker Image Sub-Screen

    Current: python:3.12-slim

      Docker image (Enter to clear): node:20-alpine
      v Docker image set to node:20-alpine.

Clearing the image:

      Current: python:3.12-slim

      Docker image (Enter to clear):
      v Docker image cleared.

When not set:

      Current: (not set)

      Docker image (Enter to clear): python:3.12-slim
      v Docker image set to python:3.12-slim.

### Edit Require Container Sub-Screen

      Current: Yes

      Require container execution? [Y/n] n
      v Require container set to False.

With warning (no Docker image):

      Current: No
      Warning: No Docker image is configured.

      Require container execution? [y/N] y
      v Require container set to True.

### After Each Edit Action

      v Project saved.

    Edit my-api:
    > Directories          3 configured
      ...

### Error States

    Error: Project my-api not found.

    Error: /project edit requires an interactive terminal.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Edit menu | Main loop | SelectionPrompt with 5 choices, summaries reflect current values |
| Directories - table | Directories exist | SimpleTable showing #, Path, Access with colored access labels |
| Directories - empty | No directories | Dim "No directories configured" before action menu |
| Directories - add | "Add directory" selected | Path prompt + access level selection + success |
| Directories - remove | "Remove directory" selected | Selection of existing paths + success; or yellow "No directories to remove" |
| Directories - change | "Change access level" selected | Path selection + level selection + success; or yellow "No directories to modify" |
| System prompt - custom | Custom prompt set | Shows current with "Set new prompt" / "Reset to default" / "Back" |
| System prompt - default | No custom prompt | Shows current with "(default)" prefix, no "Reset to default" option |
| Docker image - set | Docker image configured | Shows current image, prompt to change or clear |
| Docker image - not set | No Docker image | Shows "(not set)", prompt to set |
| Require container | Always shown | Current value + confirm prompt; yellow warning if no Docker image |
| Saved | After each edit | Success message + menu re-renders with updated summaries |
| Not found | Project does not exist | Red error, command exits |
| Non-interactive | Terminal is not interactive | Red error via `SpectreHelpers.Error`, command exits |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | Project name in menu title, entity names in confirmations, "Current:" labels |
| `[green]` | success-green | Success checkmarks, "ReadWrite" labels, "Yes" require-container value |
| `[yellow]` | warning-yellow | "ReadOnly" labels, "Warning:" prefix, "No directories to..." messages |
| `[red]` | error-red | "Error:" prefix |
| `[dim]` | dim (2.2) | "none"/"default"/"No" summaries in menu choices, "No directories configured", "(default)" prefix, "(not set)" label |

## Interactive Elements

| Element | Type | Label | Context |
|---|---|---|---|
| Edit menu | `SpectreHelpers.Select` | `Edit [bold]{name}[/]:` | Main loop, remembers last selection index |
| Directory action | `SpectreHelpers.Select` | `  Directory action:` | After viewing directory table |
| Directory path | `SpectreHelpers.PromptNonEmpty` | `  Directory path:` | Adding a directory |
| Access level | `SpectreHelpers.Select<DirectoryAccessLevel>` | `  Access level:` / `  New access level:` | Adding or changing directory |
| Select directory | `SpectreHelpers.Select` | `  Select directory to remove:` / `  Select directory:` | Removing or changing |
| System prompt action | `SpectreHelpers.Select` | `  System prompt:` | System prompt sub-screen |
| New prompt | `SpectreHelpers.PromptNonEmpty` | `  New system prompt:` | Setting custom prompt |
| Docker image | `SpectreHelpers.PromptOptional` | `  Docker image [dim](Enter to clear)[/]:` | Docker edit sub-screen |
| Require container | `SpectreHelpers.Confirm` | `  Require container execution?` | Container requirement edit |

## Behavior

1. **Menu loop**: The main loop uses `SpectreHelpers.Select` with a
   `lastIndex` variable to remember the user's last selection position. Each
   iteration rebuilds the choice list with updated summaries.

2. **Choice formatting**: `FormatEditChoice(label, summary)` pads the label to
   20 characters then appends the summary. This creates the aligned two-column
   appearance within the selection prompt.

3. **Auto-save**: After each edit action, `_projectRepository.SaveAsync` is
   called immediately. There is no explicit "save" action -- changes are
   persisted as soon as the user makes them.

4. **Session context refresh**: If the edited project is the active project,
   `RefreshSessionContext` is called after each edit. This re-resolves
   directories, updates the `DirectoryGuard`, and rebuilds the session's system
   prompt so the LLM sees changes on the next turn.

5. **Stale settings warning**: If Docker image or require-container settings
   are changed on the active project, `_ui.StaleSettingsWarning` is set to
   prompt the user to run `/context refresh`.

6. **Sub-screen exit**: Each sub-screen returns to the main menu after its
   action completes. The "Back" option in sub-screens exits without changes.

## Edge Cases

- **Non-interactive terminal**: The command requires `_ui.IsInteractive`. If
  false, shows an error and exits. No fallback is provided because the edit
  menu loop requires interactive prompts.

- **Empty directories on remove/change**: If the user selects "Remove
  directory" or "Change access level" but no directories exist, a yellow
  warning is shown and the action is skipped.

- **Concurrent name resolution**: The name is resolved once at the start.
  If the active project name is used and the project is deleted by another
  process during the edit session, subsequent saves may recreate the project
  file.

- **Long Docker image names**: The summary in the menu choice is the full
  image name (not truncated). At 80 columns, the SelectionPrompt truncates
  visually if the combined label + summary exceeds the available width.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Edit Menu Loop | Section 11 | Main edit loop with remembered index |
| Simple Table | Section 4 | Directory listing in edit sub-screen |
| Selection Prompt | Section 5 | Menu, directory actions, access levels |
| Text Prompt | Section 7 | Directory path, system prompt, Docker image |
| Confirmation Prompt | Section 8 | Require container toggle |
| Status Message | Section 1 | Success, error, warning messages |
| Empty State | Section 13 | "No directories configured" |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| Edit flow | `Commands/ProjectSlashCommand.cs` | `HandleEditAsync` | 368-453 |
| Menu loop | `Commands/ProjectSlashCommand.cs` | `HandleEditAsync` | 388-443 |
| Choice formatting | `Commands/ProjectSlashCommand.cs` | `FormatEditChoice` | 765-769 |
| Edit directories | `Commands/ProjectSlashCommand.cs` | `EditDirectories` | 609-685 |
| Edit system prompt | `Commands/ProjectSlashCommand.cs` | `EditSystemPrompt` | 688-717 |
| Edit Docker image | `Commands/ProjectSlashCommand.cs` | `EditDockerImage` | 719-743 |
| Edit require container | `Commands/ProjectSlashCommand.cs` | `EditRequireContainer` | 746-759 |
| Session context refresh | `Commands/ProjectSlashCommand.cs` | `RefreshSessionContext` | 461-469 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
