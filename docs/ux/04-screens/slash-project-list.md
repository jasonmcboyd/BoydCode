# Screen: /project list

## Overview

Lists all named projects in a tabular format showing name, directory count,
Docker configuration, and last-used date. The ambient `_default` project is
included with a "(ambient)" suffix.

**Screen IDs**: PROJ-13, PROJ-14

## Trigger

`/project list`

## Layout (80 columns)

### With Projects

    Name                 Dirs   Docker                          Last used
    ────────────────────────────────────────────────────────────────────────
    _default (ambient)      0   --                              2026-02-27
    my-api                  3   python:3.12-slim (required)     2026-02-26
    frontend                1   --                              2026-02-20
    infra                   2   ubuntu:24.04                    2026-01-15

### Empty State

    No projects found.
    Create one with /project create <name>

## States

| State | Condition | Visual Difference |
|---|---|---|
| Table with projects | At least one project exists | SimpleTable with rows per project |
| Empty | No projects exist | Plain text message + dim hint |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | Table column headers (via `SpectreHelpers.SimpleTable`) |
| `[dim]` | dim (2.2) | "(ambient)" suffix, "--" for no Docker, "(required)" suffix, empty state hint |
| `[green]` | success-green | Not used on this screen |

## Interactive Elements

None. This screen is purely static output.

## Behavior

1. **Project loading**: Calls `_projectRepository.ListNamesAsync` to get all
   project names, then loads each project individually to gather metadata.

2. **Directory count**: For each project, directories are resolved via
   `_directoryResolver.Resolve`. If any resolved directory is a git repo, the
   count shows the total plus git count: e.g., `3 (2 git)`.

3. **Docker column**: Shows the Docker image name if configured. Appends
   `(required)` in dim text if `RequireContainer` is true. Shows `--` in dim
   if no Docker image is set.

4. **Ambient project**: The `_default` project is shown with its name plus
   `(ambient)` in dim text. It is always present in a non-empty list.

5. **Last used**: Formatted as `yyyy-MM-dd` using `InvariantCulture`.

6. **Column alignment**: The "Dirs" column is right-aligned. All other columns
   are left-aligned (default).

## Edge Cases

- **No projects at all**: Shows "No projects found." as plain text followed by
  a dim hint line suggesting `/project create <name>`. No table is rendered.

- **Project load failure**: If `_projectRepository.LoadAsync` returns null for
  a listed name (corrupted file), that project is silently skipped. The table
  may have fewer rows than expected.

- **Long project names**: Spectre's Table handles column width distribution
  automatically. Very long names may compress other columns at 80-column width.

- **Narrow terminal**: Spectre's table renderer wraps cell content when the
  terminal is narrower than the natural table width. Column headers remain
  readable but data cells may wrap.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Simple Table | Section 4 | Project list table |
| Empty State | Section 13 | "No projects found" + hint |

