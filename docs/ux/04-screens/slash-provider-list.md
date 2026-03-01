# Screen: /provider list

## Overview

Lists all available LLM provider types with their current status, configured
model, and masked API key. This is also the default subcommand when running
`/provider` with no arguments.

**Screen IDs**: PROV-02

## Trigger

`/provider list` or `/provider` (no subcommand)

## Layout (80 columns)

### All Providers

    ┌───────────┬────────────┬──────────────────────┬──────────┐
    │ Provider  │ Status     │ Model                │ API Key  │
    ├───────────┼────────────┼──────────────────────┼──────────┤
    │ Anthropic │            │ claude-sonnet-4-20250514 │ not set  │
    │ Gemini    │ active     │ gemini-2.5-pro       │ AIza**** │
    │ OpenAi    │ ready      │ gpt-4o               │ sk-4**** │
    │ Ollama    │            │ llama3.2             │ not set  │
    └───────────┴────────────┴──────────────────────┴──────────┘

Note: The table uses Spectre's default border (not `SimpleTable`), which
renders as `TableBorder.Square` -- this is a known inconsistency documented
in `06-style-tokens.md` section 11.4.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Active provider | Provider is currently activated | "active" in green bold |
| Ready provider | Has API key but not active | "ready" in dim |
| Unconfigured provider | No API key stored | Empty status cell, "not set" in dim for API key |
| No active provider | No provider is activated | No row shows "active" status |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green bold]` | success-green + bold (2.2) | "active" status |
| `[dim]` | dim (2.2) | "ready" status, "not set" API key |

Note: Table headers use plain text (no bold), which differs from the
`SimpleTable` pattern. See `06-style-tokens.md` section 11.5.

## Interactive Elements

None. This screen is purely static output.

## Behavior

1. **Provider enumeration**: Iterates over all `LlmProviderType` enum values.
   For each provider type, checks whether a stored `ProviderProfile` exists
   via `_providerConfigStore.GetAllAsync`.

2. **Status determination**: A provider is "active" if `_activeProvider` is
   configured and its `ProviderType` matches. A provider is "ready" if it has
   a stored API key but is not active. Otherwise, the status is empty.

3. **Model display**: Uses the stored `DefaultModel` from the profile if
   available, otherwise falls back to `ProviderDefaults.DefaultModelFor`.

4. **API key masking**: Shows the first 4 characters followed by `****`.
   Keys shorter than 4 characters are shown in full (escaped). If no key is
   stored, shows "not set" in dim.

5. **Rendering**: The table is built with raw `new Table()` (not
   `SpectreHelpers.SimpleTable`), which uses Spectre's default Square border.

## Edge Cases

- **No providers configured**: All rows show empty status and "not set" for
  API key. The table still renders with all four `LlmProviderType` values.

- **Narrow terminal**: Spectre's table renderer wraps long model names within
  cells. The fixed-width columns (Provider, Status, API Key) are short enough
  to fit at 80 columns.

- **Ollama without API key**: Ollama does not require an API key for local
  usage, so "not set" is the normal state. It can still show as "active" if
  configured via `/provider setup ollama`.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Simple Table | Section 4 | Table structure (though using default border, not SimpleTable) |

