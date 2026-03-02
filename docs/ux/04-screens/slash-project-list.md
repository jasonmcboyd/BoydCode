# Screen: /project list

## Overview

The project list screen opens an interactive list in a modeless Window titled
"Projects", showing all named projects with their configuration summary. Users
can navigate the list, view project details, edit, delete, or create new
projects -- all from within the window.

The ambient `_default` project is included with a "(ambient)" suffix in muted
style.

**Screen IDs**: PROJ-13, PROJ-14

## Trigger

- User types `/project list` or `/project` (default subcommand) during an
  active session.
- Handled by `ProjectSlashCommand.HandleListAsync()`.

## Route

Opens a modeless `Window` via the Interactive List pattern (component pattern
#28). The window floats over the conversation view. The agent continues working
in the background. The user dismisses with Esc.

## Layout (80 columns)

### With Projects

```
+-- Projects -----------------------------------------------+
|                                                            |
|  Name                Dirs  Docker                  Used    |
|  ▶ my-api            3     python:3.12 (required)  Feb 26  |
|    frontend          1     --                      Feb 20  |
|    infra             2     ubuntu:24.04            Jan 15  |
|    _default (ambient)  0   --                      Feb 27  |
|                                                            |
|  Enter: Show  e: Edit  d: Delete  n: New  Esc: Close      |
|                                                            |
+------------------------------------------------------------+
```

The highlighted row (first row by default) uses `Theme.List.SelectedBackground`
(blue) with `Theme.List.SelectedText` (white). The `▶` arrow indicator marks
the focused row.

### Empty State

```
+-- Projects -----------------------------------------------+
|                                                            |
|                                                            |
|        No projects configured.                             |
|        Press n to create one.                              |
|                                                            |
|                                                            |
|  n: New  Esc: Close                                        |
|                                                            |
+------------------------------------------------------------+
```

When the list is empty, the empty message is centered and drawn with
`Theme.Semantic.Muted` (dark gray). The Action Bar retains `n: New` since
creating a project is still available.

### Anatomy

1. **Window** -- Modeless `Window` with `Theme.Modal.BorderScheme` (blue border),
   title "Projects", rounded border style, centered at 80% width / 70% height.

2. **Column Header** -- Static `Label` showing column names. Drawn with
   `Theme.Semantic.Muted` (dark gray). Columns:
   - **Name** -- left-aligned, primary column
   - **Dirs** -- right-aligned, directory count
   - **Docker** -- left-aligned, Docker image name and flags
   - **Used** -- left-aligned, last-used date

3. **List View** -- `ListView` with one row per project. Scrollable when items
   exceed viewport height. The focused row uses `Theme.List.SelectedBackground`
   and `Theme.List.SelectedText`. The `▶` arrow indicator (`\u25b6`) marks
   the focused row in column 2.

4. **Row Content** --
   - **Name cell**: Project name. The `_default` project shows
     `_default (ambient)` with the suffix in `Theme.Semantic.Muted`.
   - **Dirs cell**: Integer count, right-aligned. If any resolved directory is
     a git repo, shows the total plus git count: e.g., `3 (2 git)`.
   - **Docker cell**: Docker image name if configured. Appends `(required)` in
     `Theme.Semantic.Muted` if `RequireContainer` is true. Shows `--` in
     `Theme.Semantic.Muted` if no Docker image is set.
   - **Used cell**: `MMM dd` format (e.g., `Feb 26`) using local time. If from
     a prior year, shows `yyyy-MM-dd`.

5. **Action Bar** (component pattern #29) -- Positioned at `Y = Pos.AnchorEnd(2)`.
   Shows available keyboard shortcuts. Priority order (rightmost dropped first
   at narrow widths):
   1. `Esc: Close` (always shown)
   2. `Enter: Show` (always shown)
   3. `e: Edit`
   4. `d: Delete`
   5. `n: New`

## States

| State | Condition | Visual Difference |
|---|---|---|
| With projects | At least one project exists | List with rows per project |
| Empty | No projects exist | Centered empty state message, action bar shows `n: New` and `Esc: Close` |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

| Element | Token | Notes |
|---|---|---|
| Window border | `Theme.Modal.BorderScheme` | Blue border, rounded style |
| Selected row background | `Theme.List.SelectedBackground` | Accent blue |
| Selected row text | `Theme.List.SelectedText` | White on blue |
| Action bar text | `Theme.List.ActionBar` | Delegates to `Theme.Semantic.Muted` |
| Column headers | `Theme.Semantic.Muted` | Dark gray |
| `(ambient)` suffix | `Theme.Semantic.Muted` | Dark gray |
| `(required)` suffix | `Theme.Semantic.Muted` | Dark gray |
| Empty cells (`--`) | `Theme.Semantic.Muted` | Dark gray em-dash |
| Empty state message | `Theme.Semantic.Muted` | Dark gray centered text |
| Data cell text | `Theme.Semantic.Default` | White |
| Row indicator | `\u25b6` (arrow) | Marks focused row |

## Interactive Elements

### Keyboard

| Key | Action |
|---|---|
| Up / k | Move selection up |
| Down / j | Move selection down |
| Enter | Show project detail (opens detail modal -- see `/project show`) |
| e | Edit selected project (opens edit flow -- see `/project edit`) |
| d | Delete selected project (opens Delete Confirmation dialog, pattern #15) |
| n | Create new project (opens create flow -- see `/project create`) |
| Esc | Close the window |

Single-letter hotkeys are handled in the window's `OnKeyDown` override and fire
only when the `ListView` has focus (not when a sub-dialog is open).

### Actions

- **Enter (Show)**: Opens a detail modal window showing the full project
  configuration (name, directories with access levels, Docker image, execution
  mode, JEA profiles, custom prompt). See `/project show` screen spec.

- **e (Edit)**: Opens the project edit flow for the selected project. The edit
  flow suspends Terminal.Gui for interactive prompts. On completion, the list
  row updates to reflect changes.

- **d (Delete)**: Opens a Delete Confirmation dialog (pattern #15) showing the
  project name, directory count, and Docker configuration. "Cancel" is
  pre-focused. On confirm, the row is removed from the list. Cannot delete the
  `_default` (ambient) project -- the `d` key is ignored or shows a brief
  warning.

- **n (New)**: Opens the project creation flow. On completion, the new project
  appears in the list.

## Behavior

1. **Project loading**: Calls `_projectRepository.ListNamesAsync` to get all
   project names, then loads each project individually to gather metadata.

2. **Directory count**: For each project, directories are resolved via
   `_directoryResolver.Resolve`. If any resolved directory is a git repo, the
   count shows the total plus git count: e.g., `3 (2 git)`.

3. **Docker column**: Shows the Docker image name if configured. Appends
   `(required)` in muted text if `RequireContainer` is true. Shows `--` in
   muted if no Docker image is set.

4. **Ambient project**: The `_default` project is shown with its name plus
   `(ambient)` in muted text. It is always present in a non-empty list.

5. **Last used**: Formatted as `MMM dd` for dates within the current year,
   `yyyy-MM-dd` for prior years.

6. **Sorting**: Projects are listed alphabetically by name.

7. **Delete guard**: The `_default` (ambient) project cannot be deleted.
   Attempting to press `d` on it ignores the keypress or shows a brief status
   message.

8. **Window type**: Modeless window. The agent continues processing in the
   background while the window is open.

## Edge Cases

- **No projects at all**: Shows empty state with `n: New` available in the
  action bar so the user can create a project directly.

- **Project load failure**: If `_projectRepository.LoadAsync` returns null for
  a listed name (corrupted file), that project is silently skipped. The list
  may have fewer rows than expected.

- **Long project names**: Truncated with `...` at the column width boundary.
  The full name is visible in the detail view (Enter).

- **Many projects (> viewport height)**: `ListView` scrolls natively.
  Practical project counts are expected to be small (under 20).

- **Narrow terminal (< 60 columns)**: Columns are dropped right-to-left to
  fit: Used is dropped first, then Docker, then Dirs. Name is always shown.
  Action bar drops less-important hints per pattern #29.

- **Delete ambient project**: The `d` key is ignored for the `_default`
  project.

- **Non-interactive/piped terminal**: Falls back to column-aligned plain text
  output to stdout. No window, no interactivity. Colors are omitted. Format:

  ```
  Name                Dirs  Docker                  Used
  my-api               3    python:3.12 (required)  Feb 26
  frontend             1    --                      Feb 20
  infra                2    ubuntu:24.04            Jan 15
  _default (ambient)   0    --                      Feb 27
  ```

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Interactive List | #28 | ListView with keyboard navigation |
| Action Bar | #29 | Shortcut hints at bottom of window |
| Modal Overlay (List variant) | #11 | Modeless window over conversation |
| Delete Confirmation | #15 | Confirm before deleting a project |
| Empty State | #21 | "No projects configured." message |
