# Screen: /project show

## Overview

Displays a detailed view of a project's configuration in a modeless Detail
Modal window (component pattern #11, Variant C). Content includes metadata
(dates, engine, Docker), directories with access levels and git status, meta
prompt text, system prompt, and assigned JEA profiles. All content is drawn
using Terminal.Gui native drawing (`SetAttribute`, `Move`, `AddStr`) with
structured key-value layout, section dividers, and a directory table.

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

```
+-- my-api -------------------------------------------------+
|                                                            |
|  Project    my-api                                         |
|  Created    2026-02-15 10:30    Last used  2026-02-27      |
|  Docker     python:3.12-slim                               |
|  Container  Required                                       |
|                                                            |
|  -- Directories ---                                        |
|  - C:\Users\jason\source\repos\my-api  ReadWrite   main    |
|  - C:\Users\jason\data\fixtures        ReadOnly    --      |
|                                                            |
|  -- Meta prompt ---                                        |
|  You have access to a Shell tool that executes commands     |
|  in a constrained PowerShell runspace. Use it to explore   |
|  the filesystem, read and write files, and run build       |
|  commands.                                                 |
|                                                            |
|  -- System prompt ---                                      |
|  You are an expert API developer working on the my-api     |
|  project.                                                  |
|                                                            |
|  -- JEA profiles ---                                       |
|  security, linting                                         |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

### Minimal / Unconfigured Project

```
+-- my-api -------------------------------------------------+
|                                                            |
|  Project    my-api                                         |
|  Created    2026-02-27 14:00    Last used  2026-02-27      |
|                                                            |
|  -- Meta prompt ---                                        |
|  You have access to a Shell tool...                        |
|                                                            |
|  -- System prompt ---                                      |
|  (default) You are a helpful AI coding assistant...        |
|                                                            |
|  Tip: Use /project edit my-api to configure settings.      |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

### With Stale Settings Warning

```
+-- my-api -------------------------------------------------+
|                                                            |
|  ...                                                       |
|                                                            |
|  * Project settings changed. Run /context refresh to       |
|    apply.                                                  |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

### Not Found

```
Error: Project my-api not found.
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Full detail | Project has directories, Docker, custom prompt | Header info grid + directory table + meta prompt + system prompt + JEA profiles |
| Minimal | No directories, no Docker, no custom prompt | Header info grid + meta prompt + default system prompt + dim tip |
| With Docker | `DockerImage` set | Extra "Docker" and "Container" rows in header info grid |
| With git | Directory is a git repo | Branch name in `Theme.Semantic.Info` (cyan) in directory table |
| Missing directory | Directory path does not exist on disk | "missing" in `Theme.Semantic.Error` (red) in git column |
| Default prompt | `SystemPrompt` is null | "(default)" prefix in `Theme.Semantic.Muted` before the default prompt text |
| JEA profiles assigned | `Execution.JeaProfiles` has entries | "JEA profiles" section with comma-separated names in `Theme.Semantic.Info` |
| Stale warning | Active project with changed settings | Yellow asterisk warning before dismiss hint |
| Not found | Project name does not exist | Red error with bold entity name |
| Non-interactive, no name | No name, no active project, non-interactive | Yellow usage hint |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

- `Theme.Modal.BorderScheme` -- blue border on the modeless window
- `Theme.Semantic.Muted` -- dim labels in info pairs ("Project", "Created",
  "Last used", "Docker", "Container"), section divider rules, "(default)"
  prefix, "--" for no git, tip text, "Esc to dismiss"
- `Theme.Semantic.Info` -- cyan values in info pairs (project name, dates),
  git branch names, JEA profile names
- `Theme.Semantic.Success` -- green "ReadWrite" access label, "Required"
  container status
- `Theme.Semantic.Warning` -- yellow "ReadOnly" label, "Optional" status,
  stale settings warning, "Usage:" prefix
- `Theme.Semantic.Error` -- red "Error:" prefix, "missing" directory status
- `Theme.Semantic.Default` -- white directory paths, prompt text, module names

## Interactive Elements

| Element | Type | Label | When Used |
|---|---|---|---|
| Project name | `SpectreHelpers.Ask<string>` | `Project name:` | No name arg, no active project, interactive terminal |

## Behavior

1. **Name resolution**: Priority order: inline argument > active project name >
   interactive prompt > usage hint.

2. **Window construction**: `ShowDetailModal` opens a Terminal.Gui modeless
   `Window` with a blue border (`Theme.Modal.BorderScheme`). The window title
   is the project name.

3. **Native drawing layout**: The window's inner `View` overrides
   `OnDrawingContent` to draw the structured layout:

   - **Header info pairs**: Uses the Info Grid pattern (pattern #9). Labels
     at `x = 2` in `Theme.Semantic.Muted`, values at label pad offset in
     `Theme.Semantic.Info`. "Project" is always shown. "Created" and "Last
     used" share a row as a paired info pair. "Docker" and "Container" rows
     appear only when relevant data exists.

   - **Container status**: "Required" in `Theme.Semantic.Success` (green) if
     `RequireContainer` is true, "Optional" in `Theme.Semantic.Warning`
     (yellow) otherwise. This row only appears if Docker image is set or
     `RequireContainer` is true.

   - **Directories section**: A Section Divider (pattern #8) labeled
     "Directories". Each directory is drawn on its own line with `- ` prefix.
     Three columns: path in `Theme.Semantic.Default`, access level
     color-coded (`Theme.Semantic.Success` for ReadWrite,
     `Theme.Semantic.Warning` for ReadOnly), and git branch in
     `Theme.Semantic.Info` or `--` in `Theme.Semantic.Muted` if not a git
     repo, or "missing" in `Theme.Semantic.Error` if the path does not exist.

   - **Meta prompt section**: A Section Divider labeled "Meta prompt". The
     full meta prompt text from `MetaPrompt.Build()` is drawn line by line
     at 2-char indent in `Theme.Semantic.Default`.

   - **System prompt section**: A Section Divider labeled "System prompt".
     Custom prompt text is drawn at 2-char indent in `Theme.Semantic.Default`.
     If null, "(default)" prefix in `Theme.Semantic.Muted` followed by the
     default prompt text.

   - **JEA profiles section**: A Section Divider labeled "JEA profiles".
     Comma-separated profile names in `Theme.Semantic.Info` (cyan). Omitted
     entirely if no profiles are assigned.

4. **Stale warning**: If this is the active project and `_ui.StaleSettingsWarning`
   is set, a `Theme.Semantic.Warning` (yellow) asterisk-prefixed warning is
   drawn before the dismiss hint.

5. **Minimal tip**: If the project has no directories, no custom prompt, no
   Docker image, no container requirement, and no execution config, a
   `Theme.Semantic.Muted` (dim) tip suggests using `/project edit`.

6. **Dismiss hint**: "Esc to dismiss" is drawn at the bottom of the content
   in `Theme.Semantic.Muted`. The `ActivityBarView` transitions to
   `ActivityState.Modal` while the window is open.

## Edge Cases

- **Ambient project**: The `_default` project shows with "(ambient)" appended
  to the display name in the header info pair. It has no directory restrictions
  by design.

- **Git detection failure**: If a directory exists but is not a git repo,
  the git column shows `--` in dim. If the directory does not exist at all,
  it shows "missing" in red.

- **Long meta prompt**: The meta prompt text is word-wrapped at the window's
  inner width minus 4 characters (2-char indent on each side). Long lines
  wrap naturally.

- **Long directory paths**: Path text wraps naturally within the available
  window width. The directory table column alignment may shift for very long
  paths.

- **Long system prompt**: The system prompt section is scrollable within the
  window if it exceeds the visible area. The window itself scrolls via
  Terminal.Gui's built-in Viewport scrolling.

- **Non-interactive, no name, no active project**: Shows usage hint and exits
  without opening a window.

## Non-TUI Fallback

When running in non-interactive/piped mode (no Terminal.Gui), the project
detail is rendered as plain text to stdout. Section dividers use `--` prefix.
Access levels and git info are rendered as plain text without color.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Modal Overlay (Detail Modal) | #11, Variant C | Modeless window with native drawing layout |
| Info Grid | #9 | Header key-value pairs (project, dates, docker, container) |
| Section Divider | #8 | "Directories", "Meta prompt", "System prompt", "JEA profiles" headings |
| Status Message | #7 | Error, usage, tip messages |
| Empty State | #21 | Dim tip for unconfigured projects |
