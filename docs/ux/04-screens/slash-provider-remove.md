# Screen: /provider remove

## Overview

Removes a stored provider configuration. If the removed provider is the
currently active provider, a warning is shown indicating it will remain active
for the current session but will not persist across restarts.

**Screen IDs**: PROV-11, PROV-12, PROV-13, PROV-14

## Trigger

`/provider remove [name]`

- If `name` is provided and matches a valid `LlmProviderType` (case-insensitive),
  it is used directly.
- If `name` is omitted or invalid, a selection prompt lists all provider types.
- Non-interactive mode requires the name argument.

## Layout (80 columns)

### Standard Removal

    Select a provider to remove:
    > Anthropic
      Gemini
      OpenAi
      Ollama

    Provider 'Anthropic' removed.

### Removing the Active Provider

    Provider 'Gemini' removed.

With inline name:

    Warning: 'Gemini' is the active provider. It will remain active for this
    session but won't persist.
    Provider 'Gemini' removed.

### Non-Interactive Usage Hint

    Usage: /provider remove <name>

## States

| State | Condition | Visual Difference |
|---|---|---|
| Provider selection | No valid name argument, interactive | SelectionPrompt with all provider types |
| Removed (not active) | Removed provider is not the active one | Green success message only |
| Removed (active) | Removed provider is the currently active one | Yellow warning about session persistence + green success |
| Non-interactive, no name | No name arg, non-interactive | Yellow usage hint |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green]` | success-green | Success message (entire message wrapped in green) |
| `[yellow]` | warning-yellow | Active provider warning (entire message wrapped in yellow); "Usage:" prefix |
| `Color.Green` | Spectre color (1.5) | SelectionPrompt highlight style |

## Interactive Elements

| Element | Type | Label | Context |
|---|---|---|---|
| Provider selection | `SpectreHelpers.Select` | `Select a provider to remove:` | No valid name arg, interactive |

## Behavior

1. **Provider resolution**: Same logic as `/provider setup` -- inline argument
   is parsed via `Enum.TryParse`, falling back to a selection prompt.

2. **No confirmation**: The removal happens immediately without a confirm
   prompt. This is a destructive operation with no undo, though the user can
   reconfigure via `/provider setup`.

3. **Store removal**: `_providerConfigStore.RemoveAsync` deletes the stored
   `ProviderProfile` for the given provider type.

4. **Active provider check**: After removal, if the removed provider type
   matches the active provider, a yellow warning is shown. The warning wraps
   the entire text in `[yellow]` (not just the prefix), which is a known
   inconsistency with the standard `[yellow]Warning:[/]` prefix pattern. See
   `06-style-tokens.md` section 11.6.

5. **Session persistence**: The active provider is not deactivated. The
   in-memory `_activeProvider` singleton retains its reference, so the provider
   remains functional for the remainder of the session. On the next app launch,
   the provider will not be available.

6. **Success message**: Always shown, even when the active-provider warning
   also appears. The success message wraps the entire text in `[green]`,
   including the provider name.

## Edge Cases

- **Removing a provider with no stored config**: The
  `_providerConfigStore.RemoveAsync` call is a no-op if the profile does not
  exist. The success message is still shown. No error or warning.

- **Non-interactive terminal**: If no name argument is provided and the
  terminal is non-interactive, shows the usage hint. If a name is provided,
  the removal proceeds without any prompts.

- **Invalid provider name**: If `/provider remove xyz` is given and `xyz`
  does not match a `LlmProviderType`, the selection prompt is shown (in
  interactive mode) or the usage hint is shown (in non-interactive mode).

- **All providers removed**: After removing all providers, `/provider list`
  shows all four provider types with empty status and "not set" API keys.
  The app still functions if a provider was activated earlier in the session.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Selection Prompt | Section 5 | Provider type selection |
| Status Message | Section 1 | Usage hint |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| Remove flow | `Commands/ProviderSlashCommand.cs` | `HandleRemoveAsync` | 199-237 |
| Provider resolution | `Commands/ProviderSlashCommand.cs` | `HandleRemoveAsync` | 209-223 |
| Store removal | `Commands/ProviderSlashCommand.cs` | `HandleRemoveAsync` | 225 |
| Active provider warning | `Commands/ProviderSlashCommand.cs` | `HandleRemoveAsync` | 227-233 |
| Success message | `Commands/ProviderSlashCommand.cs` | `HandleRemoveAsync` | 235-236 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
