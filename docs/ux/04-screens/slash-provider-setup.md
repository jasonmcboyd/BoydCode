# Screen: /provider setup

## Overview

Configures an LLM provider by prompting for an API key and model, then
activates it as the current provider. This is the primary onboarding flow for
connecting BoydCode to an LLM service.

**Screen IDs**: PROV-03, PROV-04, PROV-05, PROV-06, PROV-07, PROV-08

## Trigger

`/provider setup [name]`

- If `name` is provided and matches a valid `LlmProviderType` (case-insensitive),
  it is used directly.
- If `name` is omitted or invalid, a selection prompt lists all provider types.
- Requires an interactive terminal for the API key and model prompts.

## Layout (80 columns)

### Full Setup Flow

    Select a provider:
    > Anthropic
      Gemini
      OpenAi
      Ollama

    API key: ****************
    Model: (gemini-2.5-pro)
    Provider 'Gemini' configured and activated.

### With Inline Name

    API key: ****************
    Model: (gpt-4o)
    Provider 'OpenAi' configured and activated.

### Ollama (API Key Optional)

    API key:
    Model: (llama3.2) mistral
    Provider 'Ollama' configured and activated.

### Non-Interactive Errors

    Usage: /provider setup <name>

    Error: /provider setup requires an interactive terminal. Use --api-key instead.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Provider selection | No valid name argument | SelectionPrompt with all provider types |
| API key prompt | Provider selected, interactive | Secret text prompt (input masked with `*`) |
| Model prompt | After API key | Text prompt with default model from `ProviderDefaults` shown in parentheses |
| Success | Provider saved and activated | Green success message |
| Non-interactive, no name | No name arg, non-interactive | Yellow usage hint |
| Non-interactive, after selection | Provider selected but non-interactive | Red error suggesting `--api-key` flag |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green]` | success-green | Success message (entire message wrapped in green) |
| `[red]` | error-red | "Error:" prefix for non-interactive error |
| `[yellow]` | warning-yellow | "Usage:" prefix |
| `Color.Green` | Spectre color (1.5) | SelectionPrompt highlight style |

## Interactive Elements

| Element | Type | Label | Validation/Default |
|---|---|---|---|
| Provider selection | `SpectreHelpers.Select` | `Select a provider:` | All `LlmProviderType` values |
| API key | `SpectreHelpers.PromptSecret` | `API key:` | Required (except Ollama: `allowEmpty: true`) |
| Model | `SpectreHelpers.PromptWithDefault` | `Model:` | Default from `ProviderDefaults.DefaultModelFor` |

## Behavior

1. **Provider resolution**: If the trailing argument matches a valid
   `LlmProviderType` (via `Enum.TryParse` with `ignoreCase: true`), it is
   used directly. Otherwise, a selection prompt shows all provider types.

2. **Interactive gate**: After provider selection, if the terminal is
   non-interactive, an error is shown suggesting the `--api-key` CLI flag
   as an alternative. This is a two-stage check -- the first check (no name +
   non-interactive) shows a usage hint, the second check (after selection +
   non-interactive) shows the error.

3. **API key prompt**: Uses `SpectreHelpers.PromptSecret` which renders input
   as `*` characters. For Ollama, `allowEmpty` is true since Ollama typically
   runs locally without authentication. An empty key is normalized to `null`.

4. **Model prompt**: Uses `SpectreHelpers.PromptWithDefault` with the
   provider's default model. Pressing Enter accepts the default.

5. **Activation**: The provider profile is saved via `_providerConfigStore`,
   then a `LlmProviderConfig` is built and activated via
   `_activeProvider.Activate`. The status line is updated to reflect the new
   provider and model.

6. **Last-used tracking**: The provider type is saved as the last-used provider
   via `_providerConfigStore.SetLastUsedProviderAsync`, so it will be
   auto-selected on the next app launch.

## Edge Cases

- **Overwriting existing config**: If the provider already has a stored
  profile, the new API key and model overwrite it. No confirmation is shown.

- **Invalid provider name argument**: If `/provider setup xyz` is given and
  `xyz` doesn't match any `LlmProviderType`, the `Enum.TryParse` fails and
  the selection prompt is shown instead.

- **Empty API key for non-Ollama**: The secret prompt requires input for all
  providers except Ollama. An empty value is rejected by Spectre's default
  validation (the prompt won't accept Enter with no input).

- **Status line format**: The status line is rebuilt by parsing the existing
  line and replacing the first two segments (provider and model). Segments
  after position 2 (project, branch, engine) are preserved.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Selection Prompt | Section 5 | Provider type selection |
| Text Prompt | Section 7 | Model prompt with default, secret API key prompt |
| Status Message | Section 1 | Error, usage messages |

