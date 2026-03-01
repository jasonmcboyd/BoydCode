# Screen: /project show

## Overview

Displays a detailed view of a project's configuration, including metadata
(dates, engine), directories with access levels and git status, meta prompt
text, system prompt, and assigned JEA profiles. This is the primary
inspection command for understanding a project's setup.

**Screen IDs**: PROJ-15, PROJ-16, PROJ-17, PROJ-18

## Trigger

`/project show [name]`

- If `name` is provided inline, it is used directly.
- If `name` is omitted and there is an active project, the active project is
  shown.
- If `name` is omitted, no active project exists, and the terminal is
  interactive, an `Ask<string>` prompt appears.
- If all three fail (no name, no active project, non-interactive), a usage
  hint is shown.

## Layout (80 columns)

### Full Detail View

    (blank line)
      Project   my-api
      Created   2026-02-15 10:30       Last used   2026-02-27 09:15
      Docker    python:3.12-slim
      Container Required
    (blank line)
      Directories
        - C:\Users\jason\source\repos\my-api    ReadWrite   main
        - C:\Users\jason\data\fixtures           ReadOnly    --
    (blank line)
      Meta prompt
    (blank line)
        You have access to a Shell tool that executes commands in a
        constrained PowerShell runspace. Use it to explore the
        filesystem, read and write files, and run build commands.
    (blank line)
      System prompt
    (blank line)
        You are an expert API developer working on the my-api project.
    (blank line)
      JEA profiles
      security, linting
    (blank line)

### Minimal / Unconfigured Project

    (blank line)
      Project   my-api
      Created   2026-02-27 14:00       Last used   2026-02-27 14:00
    (blank line)
      Meta prompt
    (blank line)
        You have access to a Shell tool...
    (blank line)
      System prompt
    (blank line)
        (default) You are a helpful AI coding assistant...
    (blank line)
      Tip: Use /project edit my-api to configure settings.

### With Stale Settings Warning

    (blank line)
      ...
    (blank line)
      * Project settings changed. Run /context refresh to apply.
    (blank line)

### Not Found

    Error: Project my-api not found.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Full detail | Project has directories, Docker, custom prompt | Info grid + directory table + meta prompt + system prompt + JEA profiles |
| Minimal | No directories, no Docker, no custom prompt | Info grid + meta prompt + default system prompt + dim tip |
| With Docker | `DockerImage` set | Extra "Docker" and "Container" rows in info grid |
| With git | Directory is a git repo | Branch name in cyan in directory table |
| Missing directory | Directory path does not exist on disk | Red "missing" in git column |
| Default prompt | `SystemPrompt` is null | "(default)" prefix in dim before the default prompt text |
| JEA profiles assigned | `Execution.JeaProfiles` has entries | "JEA profiles" label + comma-separated profile names in cyan |
| Stale warning | Active project with changed settings | Yellow asterisk warning before final blank line |
| Not found | Project name does not exist | Red error with bold entity name |
| Non-interactive, no name | No name, no active project, non-interactive | Yellow usage hint |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[dim]` | dim (2.2) | Info grid labels, directory/meta/system/JEA section labels, "(default)" prefix, "--" for no git, tip text |
| `[cyan]` | info-cyan | Info grid values, git branch names, JEA profile names |
| `[green]` | success-green | "ReadWrite" access label, "Required" container status |
| `[yellow]` | warning-yellow | "ReadOnly" access label, "Optional" container status, stale warning, "Usage:" prefix |
| `[red]` | error-red | "Error:" prefix, "missing" directory status |
| `[bold]` | bold (2.2) | Entity name in error message, "Current:" labels |

## Interactive Elements

| Element | Type | Label | When Used |
|---|---|---|---|
| Project name | `SpectreHelpers.Ask<string>` | `Project name:` | No name arg, no active project, interactive terminal |

## Behavior

1. **Name resolution**: Priority order: inline argument > active project name >
   interactive prompt > usage hint.

2. **Info grid**: Uses `SpectreHelpers.InfoGrid()` with paired rows. The
   Project row is always shown. Created/Last used share a row. Engine, Docker,
   and Container rows appear only when the relevant data exists.

3. **Container status**: Shows "Required" in green if `RequireContainer` is
   true, "Optional" in yellow otherwise. This row only appears if Docker image
   is set or `RequireContainer` is true.

4. **Directory table**: Uses `TableBorder.None` with hidden headers and 3
   columns: Path (with `- ` prefix), Access (right-aligned), Git info. Each
   directory is resolved for existence and git metadata. The table is wrapped
   in a `Padder` with `PadLeft(4)`.

5. **Meta prompt**: Always shown. Displays the full meta prompt text from
   `MetaPrompt.Build()` based on the current execution engine mode. Text is
   indented 4 spaces per line.

6. **System prompt**: If a custom system prompt is set, it is shown directly.
   If null, the default prompt is shown with a `(default)` prefix in dim.

7. **JEA profiles**: If `Execution.JeaProfiles` has entries, they are shown as
   a comma-separated list with each name in cyan markup.

8. **Stale warning**: If this is the active project and `_ui.StaleSettingsWarning`
   is set, a yellow asterisk-prefixed warning appears.

9. **Minimal tip**: If the project has no directories, no custom prompt, no
   Docker image, no container requirement, and no execution config, a dim tip
   suggests using `/project edit`.

## Edge Cases

- **Ambient project**: The `_default` project shows with "(ambient)" appended
  to the display name in the info grid. It has no directory restrictions by
  design.

- **Git detection failure**: If a directory exists but is not a git repo,
  the git column shows `--` in dim. If the directory does not exist at all,
  it shows "missing" in red.

- **Long meta prompt**: The meta prompt text is rendered line by line with
  4-space indent. At 80 columns, each line has 76 characters before wrapping.
  Long lines wrap naturally via `AnsiConsole.MarkupLine`.

- **Long directory paths**: Path text is escaped via `Markup.Escape` and
  rendered inside a table cell. Spectre wraps long paths within the cell.

- **Non-interactive, no name, no active project**: Shows usage hint and exits.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Info Grid | Section 3 | Project metadata key-value grid |
| Status Message | Section 1 | Error, usage, tip messages |
| Empty State | Section 13 | Dim tip for unconfigured projects |

