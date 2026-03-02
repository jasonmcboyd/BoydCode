# Screen: Not Configured

## Overview

The not-configured screen is a state variant of the startup banner that
appears when the application launches without a valid API key or OAuth token
for the active LLM provider. It replaces the green "Ready" status footer with
a yellow "Not configured" message and setup guidance. The user can still
enter the interactive session, but LLM requests will fail until a provider
is configured.

This is not a separate screen implementation -- it is a conditional branch
within `ChatCommand.RenderBanner()` and the session loop behavior.

**Screen IDs**: STARTUP-05

## Trigger

- App launch via `boydcode` or `boydcode chat` when no API key is available
  for the active provider.
- Specifically: when `isConfigured` is `false` in `ChatCommand.ExecuteAsync()`,
  which is determined by whether `ActiveProvider.Activate()` succeeds
  (provider is usable) and `_activeProvider.IsConfigured` is true.

## Layout (80 columns)

The not-configured state affects only the status footer section of the startup
banner. The banner, info grid, and rule separator render identically to the
configured state. The difference is in the last section:

### Full Banner Variant

```
(blank line)
  (... ASCII art banner, rule separator, info grid as normal ...)
(blank line)
  Not configured
  Use /provider setup to configure an API key, or pass --api-key.
(blank line)
```

### Compact Banner Variant

```
(blank line)
  BOYDCODE  v0.1  AI Coding Assistant
(blank line)
  (... rule separator, info grid ...)
(blank line)
  Not configured
  Use /provider setup to configure an API key, or pass --api-key.
(blank line)
```

### Anatomy (Status Footer Only)

(Markup notation indicates visual intent, not implementation API.)

1. **Primary status** -- `[yellow bold]Not configured[/]` (`Theme.Banner.StatusNotConfigured`,
   warning yellow, bold) at 2-space indent.
2. **Guidance line** -- 2-space indent. Mixed styles:
   - `[dim]Use[/]` -- `Theme.Semantic.Muted` prefix.
   - `[bold]/provider setup[/]` -- bold default, command reference.
   - `[dim]to configure an API key, or pass[/]` -- `Theme.Semantic.Muted` connecting text.
   - `[bold]--api-key[/]` -- bold default, flag reference.
   - `[dim].[/]` -- `Theme.Semantic.Muted` period.

### What Is Absent

When not configured, the following elements from the configured state are
**not rendered**:

- No green "Ready" text.
- No engine description ("Commands run in a constrained PowerShell runspace."
  or "Commands execute inside a Docker container.").
- No hint line ("Type a message to start, or /help for available commands.").

The hint line is suppressed because the user cannot meaningfully start a
conversation without a provider.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Not configured (full banner) | Terminal height >= 30, no API key | Full ASCII art + info grid + yellow "Not configured" + guidance |
| Not configured (compact banner) | Terminal height < 30, no API key | Single-line brand + info grid + yellow "Not configured" + guidance |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Banner.StatusNotConfigured` (warning yellow, bold —
"Not configured" primary status), `Theme.Semantic.Muted` (guidance text connecting words),
bold default weight (`/provider setup` and `--api-key` command references)

**Component patterns:** Status Message (#7), Banner (#24)

## Interactive Elements

None. The not-configured screen is purely informational. The user can then
type `/provider setup` at the input prompt to configure a provider, or
`/help` to see all available commands.

## Behavior

- **Session still created**: Even when not configured, the session loop
  starts normally. The user can use slash commands (`/provider setup`,
  `/project create`, `/help`, etc.). Only LLM requests (typing a message)
  will fail with the "No LLM provider configured" error (CHAT-08).

- **Provider defaults**: The info grid still shows a provider and model
  (from the configuration or defaults), even though no API key is
  available. This helps the user understand which provider to configure.

- **Determination logic**: `isConfigured` is set to `true` only when
  `ActiveProvider.Activate()` completes without throwing. The activation
  process checks for API keys in this priority:
  1. `--api-key` CLI argument.
  2. Environment variable for the provider.
  3. Stored OAuth token from `ICredentialStore`.
  If none are found, activation either throws or marks the provider as
  not configured.

- **Transition to configured**: After the user runs `/provider setup` and
  provides an API key, the provider is activated. The status line updates
  to reflect the active provider. No banner re-render occurs -- the
  "Not configured" message remains in the scrollback as historical output.

## Edge Cases

- **Multiple providers configured**: The not-configured state is specific
  to the active provider (the one selected by `--provider` or the default).
  Other providers may have valid credentials stored.

- **API key in environment**: If the user has set the environment variable
  (e.g., `ANTHROPIC_API_KEY`) before launching, the provider activates
  normally and the "Ready" state is shown instead.

- **Non-interactive/piped terminal**: The guidance to use `/provider setup`
  is less useful in non-interactive mode. The `--api-key` alternative is
  included for this reason.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | #7 | The guidance line follows the warning-yellow pattern |
| Banner | #24 | This is a state variant of the startup banner |

