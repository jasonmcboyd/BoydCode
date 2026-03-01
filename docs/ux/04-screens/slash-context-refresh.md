# Screen: /context refresh

## Overview

The context refresh screen reloads the active project's configuration from disk,
re-resolves directories, reconfigures the directory guard, recreates the execution
engine, and rebuilds the session system prompt (in that order, so path mappings
from the new engine are available to the prompt builder). It then displays a
before/after summary highlighting what changed. This is the mechanism for applying
project edits (e.g., `/project edit`) to the running session without restarting.

**Screen IDs**: REFRESH-01, REFRESH-02, REFRESH-03, REFRESH-04, REFRESH-05

## Trigger

- User types `/context refresh` during an active session.
- Handled by `ContextSlashCommand.HandleRefreshAsync()`.

## Layout (80 columns)

### Success (with changes)

```
(blank line)
  v Session context refreshed.
(blank line)
    Directories     3 (2 git)
    Git branch      feature/exec  (was: main)
    Engine          Container (refreshed)
    System prompt   updated (1,200 -> 1,450 chars)
(blank line)
```

### Success (no changes)

```
(blank line)
  v Session context refreshed.
(blank line)
    Directories     2 (1 git)
    Git branch      main
    Engine          InProcess (kept previous)
    System prompt   unchanged (1,200 chars)
(blank line)
```

### No Session

```
Error: No active session. Nothing to refresh.
```

### Project Not Found

```
Error: Project 'my-project' not found. It may have been deleted.
```

### Missing Directory Warning

```
Warning: Directory does not exist: C:\Users\jason\missing\path
(blank line)
  v Session context refreshed.
(blank line)
    Directories     2 (1 git)
    ...
```

### Engine Refresh Failure

```
Warning: Engine refresh failed (keeping previous): Docker daemon not running.
(blank line)
  v Session context refreshed.
(blank line)
    Directories     2 (1 git)
    Git branch      main
    Engine          InProcess (kept previous)
    System prompt   unchanged (1,200 chars)
(blank line)
```

### Anatomy

1. **Missing directory warnings** (0 or more) -- Yellow "Warning:" prefix
   per directory that does not exist on disk.
2. **Blank line**.
3. **Success message** -- Green "v" + "Session context refreshed." via
   `SpectreHelpers.Success()`.
4. **Stale warning cleared** -- `_ui.StaleSettingsWarning` is set to null.
5. **Blank line**.
6. **Summary table** -- Four rows of key-value pairs at 4-space indent.
   Each row has a dim label (left-padded to 16 chars) followed by the value.
   Changed values use `[bold]` style; unchanged values use `[dim]` style.

### Summary Rows

| Row | Label | Value Format | Changed Condition |
|---|---|---|---|
| Directories | `Directories` | `{count} ({gitCount} git)` | Directory count differs from before |
| Git branch | `Git branch` | `{branch}` or `{branch}  (was: {oldBranch})` | Branch name changed |
| Engine | `Engine` | `{mode} (refreshed)` or `{mode} (kept previous)` | Engine was successfully recreated, or mode changed |
| System prompt | `System prompt` | `updated ({before} -> {after} chars)` or `unchanged ({length} chars)` | Prompt character length changed |

## States

| State | Condition | Visual Difference |
|---|---|---|
| Success with changes | At least one property changed | Changed rows in bold, unchanged rows in dim |
| Success no changes | Everything is the same | All rows in dim |
| Success with warnings | Some directories missing + engine failure | Warning lines above success message |
| No session | Session or project name is null | Red error only |
| Project not found | Repository returns null for project name | Red error only |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green]v[/]` | success-green + success indicator (1.1, 3.1) | Success prefix |
| `[red]Error:[/]` | error-red (1.1) | Error prefix |
| `[yellow]Warning:[/]` | warning-yellow (1.1) | Missing directory and engine failure warnings |
| `[dim]` | dim (2.2) | Label portion of summary rows; unchanged value styling; "(was: ...)" suffix |
| `[bold]` | bold (2.2) | Changed value styling |

## Interactive Elements

None. This is a non-interactive operational command.

## Behavior

- **Before snapshot**: Before reloading, the method captures: first git
  branch from resolved directories, directory count, execution mode, and
  system prompt character length.

- **Project reload**: The project is loaded fresh from `IProjectRepository
  .LoadAsync()`. If null, the error is returned.

- **Directory re-resolution**: `DirectoryResolver.Resolve()` is called on
  the project's directories. Missing directories generate individual warnings
  via `SpectreHelpers.Warning()`.

- **Directory guard update**: `DirectoryGuard.ConfigureResolved()` is called
  with the newly resolved directories.

- **Engine recreation**: A new `ExecutionConfig` is built from the project.
  `IExecutionEngineFactory.CreateAsync()` creates a new engine, which is set
  on `ActiveExecutionEngine`. If creation fails, a warning is shown and the
  previous engine is kept.

- **System prompt rebuild**: `ChatCommand.BuildSystemPrompt()` reconstructs
  the prompt from the project and resolved directories. This happens AFTER
  engine recreation so that `_activeEngine.Engine?.PathMappings` (container
  volume mounts) are available to the prompt builder.

- **Status line update**: `_ui.StatusLine` is rebuilt with the updated
  provider, model, project, branch, and mode.

- **Conversation logging**: The refreshed LLM context is logged via
  `IConversationLogger.LogLlmContextAsync()`.

- **Stale warning**: `_ui.StaleSettingsWarning` is cleared, removing the
  "Project settings changed" warning from the input prompt area.

## Edge Cases

- **No git directories**: The git branch row shows "none" and is never
  "changed" since both before and after are null.

- **Engine fallback**: If the engine factory throws but `AllowInProcess` is
  true, the factory may internally fall back to in-process mode. The warning
  only appears if the factory itself throws.

- **Concurrent project edits**: If another user/process edits the project
  file between the refresh and the next `/context refresh`, the changes are
  picked up on the next refresh.

- **Non-interactive/piped terminal**: Renders normally. No prompts involved.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | Section 1 | Success, error, and warning messages |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| HandleRefreshAsync | `Commands/ContextSlashCommand.cs` | `HandleRefreshAsync` | 416-530 |
| Before snapshot | `Commands/ContextSlashCommand.cs` | `HandleRefreshAsync` | 429-433 |
| Project reload | `Commands/ContextSlashCommand.cs` | `HandleRefreshAsync` | 436-441 |
| Directory resolution + warnings | `Commands/ContextSlashCommand.cs` | `HandleRefreshAsync` | 444-448 |
| Directory guard update | `Commands/ContextSlashCommand.cs` | `HandleRefreshAsync` | 451 |
| Engine recreation | `Commands/ContextSlashCommand.cs` | `HandleRefreshAsync` | 462-472 |
| System prompt rebuild (after engine) | `Commands/ContextSlashCommand.cs` | `HandleRefreshAsync` | 475 |
| Status line update | `Commands/ContextSlashCommand.cs` | `HandleRefreshAsync` | 478-484 |
| Success message + logging | `Commands/ContextSlashCommand.cs` | `HandleRefreshAsync` | 494-505 |
| Summary rendering | `Commands/ContextSlashCommand.cs` | `HandleRefreshAsync` | 508-530 |
| RenderRefreshSummaryLine helper | `Commands/ContextSlashCommand.cs` | `RenderRefreshSummaryLine` | 532-539 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
