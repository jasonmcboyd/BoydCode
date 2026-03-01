# Screen: /project create

## Overview

Creates a new named project. In interactive mode, offers an optional
configuration wizard that walks through directories, system prompt, and
container settings via a multi-selection prompt. In non-interactive mode,
creates a bare project with no configuration.

**Screen IDs**: PROJ-02, PROJ-03, PROJ-04, PROJ-05, PROJ-06, PROJ-07, PROJ-08,
PROJ-09, PROJ-10, PROJ-11, PROJ-12

## Trigger

`/project create [name]`

- If `name` is provided inline, the name prompt is skipped.
- If `name` is omitted and the terminal is interactive, a text prompt appears.
- If `name` is omitted and the terminal is non-interactive, a usage hint is
  shown and the command exits.

## Layout (80 columns)

### Minimal Create (no configure)

    Project name: my-api
      v Project my-api created.

      Tip: Use /project edit my-api to configure later.

### Full Create with Configuration Wizard

    Project name: my-api
      v Project my-api created.

    Configure project settings now? [y/N] y

    Which settings would you like to configure?
    > [x] Directories
      [x] System prompt
      [ ] Container settings

    ── Directories ─────────────────────────────────────────────────────────────
      Directory path (Enter to finish): C:\Users\jason\source\repos\my-api
      Access level:
    > ReadWrite
      ReadOnly
      v Added C:\Users\jason\source\repos\my-api (ReadWrite)

      Directory path (Enter to finish):

    ── System prompt ───────────────────────────────────────────────────────────
      Custom system prompt (Enter for default): You are an expert API developer.
      v System prompt set.

      v Project my-api saved.

### With Container Settings

    ── Container settings ──────────────────────────────────────────────────────
      Docker image (Enter to skip): python:3.12-slim
      v Docker image set to python:3.12-slim.
      Require container execution? [Y/n] y
      v Require container: True.

      v Project my-api saved.

### Name Already Exists

    Project name: my-api
    Error: Project my-api already exists.

### Non-Interactive Usage Hint

    Usage: /project create <name>

## States

| State | Condition | Visual Difference |
|---|---|---|
| Name prompt | No name argument, interactive | TextPrompt with green highlight on "name" |
| Already exists | Name matches existing project | Red error with bold entity name, command exits |
| Created, no configure | User declines configure or non-interactive | Success message + dim tip |
| Section picker | User accepts configure prompt | MultiSelectionPrompt with 3 checkboxes |
| Directory loop | "Directories" selected | Repeating path prompt + access level selection |
| System prompt | "System prompt" selected | Text prompt with default value |
| Container settings | "Container settings" selected | Docker image prompt + require confirm |
| Saved | Configuration complete | Final success message |
| Non-interactive, no name | Non-interactive terminal, no name arg | Yellow usage hint |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green]` | success-green | Name prompt highlight, success checkmarks, "ReadWrite" label, "Allow" access |
| `[red]` | error-red | "Error:" prefix |
| `[yellow]` | warning-yellow | "Usage:" prefix, "ReadOnly" label |
| `[bold]` | bold (2.2) | Entity name in error/success messages, "Current:" label |
| `[dim]` | dim (2.2) | Tip text, "Enter to finish" / "Enter for default" / "Enter to skip" hints, section rule style, "Skipped" message |
| `[dim italic]` | dim italic (2.2) | Not used directly on this screen |
| `Color.Green` | Spectre color (1.5) | SelectionPrompt and MultiSelectionPrompt highlight style |

## Interactive Elements

| Element | Type | Label | Validation/Default |
|---|---|---|---|
| Project name | `SpectreHelpers.PromptNonEmpty` | `Project [green]name[/]:` | Non-empty validation |
| Configure now? | `SpectreHelpers.Confirm` | `Configure project settings now?` | Default: No |
| Section picker | `SpectreHelpers.MultiSelect` | `Which settings would you like to configure?` | Not required (can select none) |
| Directory path | `SpectreHelpers.PromptOptional` | `  Directory path [dim](Enter to finish)[/]:` | Empty = stop loop |
| Access level | `SpectreHelpers.Select<DirectoryAccessLevel>` | `  Access level:` | ReadWrite, ReadOnly |
| System prompt | `SpectreHelpers.PromptWithDefault` | `  Custom system prompt [dim](Enter for default)[/]:` | Default: `Project.DefaultSystemPrompt` |
| Docker image | `SpectreHelpers.PromptOptional` | `  Docker image [dim](Enter to skip)[/]:` | Empty = skip |
| Require container | `SpectreHelpers.Confirm` | `  Require container execution?` | Default: Yes |

## Behavior

1. **Name resolution**: If the name is provided as a trailing argument
   (`/project create my-api`), it is used directly. Multiple words are joined
   with spaces. Otherwise, an interactive prompt appears.

2. **Duplicate check**: The name is checked against existing projects via
   `_projectRepository.LoadAsync`. If found, a red error is shown and the
   command exits immediately.

3. **Bare creation**: A `Project` entity is created with the given name and
   saved immediately. The success message renders before the configure prompt.

4. **Configure gate**: In interactive mode, a confirm prompt asks whether to
   configure settings. Default is No. If declined, a dim tip shows the edit
   command for later use.

5. **Section picker**: A multi-selection prompt with three checkboxes appears.
   Sections are processed in order: Directories, System prompt, Container
   settings. Each selected section renders under its own section divider.

6. **Directory loop**: Repeatedly prompts for a path until the user presses
   Enter with an empty value. Each directory prompts for an access level
   (ReadWrite or ReadOnly) and shows a success confirmation.

7. **System prompt**: Shows a text prompt with the default system prompt value.
   If the user keeps the default, `project.SystemPrompt` is set to null
   (meaning "use default"). Otherwise, the custom prompt is saved.

8. **Container settings**: Prompts for a Docker image. If provided, prompts
   for whether container execution is required. If skipped, shows a dim
   "Skipped container configuration" message.

9. **Final save**: The project is saved once more after configuration, with a
   final success message.

## Edge Cases

- **Non-interactive terminal**: If `_ui.IsInteractive` is false and no name
  argument is provided, shows the usage hint and returns. No prompts are
  attempted. If a name is given, the project is created but the configure
  prompt is skipped entirely.

- **Reserved names**: No explicit reservation check exists in create. The
  `_default` ambient project is pre-seeded, so creating `_default` would hit
  the "already exists" error path.

- **Empty directory path**: Entering an empty path in the directory loop
  terminates the loop. The path prompt uses `PromptOptional` which allows
  empty input.

- **Default system prompt acceptance**: If the user accepts the default prompt
  (by pressing Enter), `project.SystemPrompt` is set to null, which means
  "use default" in the system. No success message is shown for default
  acceptance -- only for custom prompts.

- **Narrow terminal (< 80 columns)**: The section rule lines scale to
  terminal width via Spectre's `Rule` renderable. The multi-selection prompt
  and text prompts adapt to terminal width automatically. The directory loop's
  2-space indent leaves 78 columns for path text.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | Section 1 | Success, error, usage, dim messages |
| Section Divider | Section 2 | "Directories", "System prompt", "Container settings" headings |
| Text Prompt | Section 7 | Name, directory path, system prompt, Docker image |
| Selection Prompt | Section 5 | Access level selection |
| Multi-Selection Prompt | Section 6 | Section picker |
| Confirmation Prompt | Section 8 | Configure now, require container |
| Empty State | Section 13 | Not used (project is always created) |

