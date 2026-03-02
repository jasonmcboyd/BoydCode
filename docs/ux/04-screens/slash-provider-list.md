# Screen: /provider list

## Overview

The provider list screen opens an interactive list in a modeless Window titled
"Providers", showing all available LLM provider types with their current status,
configured model, and masked API key. Users can navigate the list, view provider
details, set up a provider, or remove a provider configuration.

This is also the default subcommand when running `/provider` with no arguments.

**Screen IDs**: PROV-02

## Trigger

- User types `/provider list` or `/provider` (no subcommand) during an active
  session.
- Handled by `ProviderSlashCommand.HandleListAsync()`.

## Route

Opens a modeless `Window` via the Interactive List pattern (component pattern
#28). The window floats over the conversation view. The agent continues working
in the background. The user dismisses with Esc.

## Layout (80 columns)

### All Providers

```
+-- Providers ----------------------------------------------+
|                                                            |
|  Provider   Status  Model                     API Key      |
|  ▶ Gemini   active  gemini-2.5-pro            AIza****     |
|    Anthropic         claude-sonnet-4-20250514  --           |
|    OpenAi   ready   gpt-4o                    sk-4****     |
|    Ollama           llama3.2                   --           |
|                                                            |
|  Enter: Show  s: Setup  r: Remove  Esc: Close             |
|                                                            |
+------------------------------------------------------------+
```

The highlighted row (first row by default) uses `Theme.List.SelectedBackground`
(blue) with `Theme.List.SelectedText` (white). The `▶` arrow indicator marks
the focused row.

### Anatomy

1. **Window** -- Modeless `Window` with `Theme.Modal.BorderScheme` (blue border),
   title "Providers", rounded border style, centered at 80% width / 70% height.

2. **Column Header** -- Static `Label` showing column names. Drawn with
   `Theme.Semantic.Muted` (dark gray). Columns:
   - **Provider** -- left-aligned, provider type name
   - **Status** -- left-aligned, current activation status
   - **Model** -- left-aligned, configured or default model name
   - **API Key** -- left-aligned, masked key or not-set indicator

3. **List View** -- `ListView` with one row per `LlmProviderType` enum value.
   All four providers are always shown (Anthropic, Gemini, OpenAi, Ollama).
   The focused row uses `Theme.List.SelectedBackground` and
   `Theme.List.SelectedText`. The `▶` arrow indicator (`\u25b6`) marks the
   focused row in column 2.

4. **Row Content** --
   - **Provider cell**: Provider type name (`Gemini`, `Anthropic`, `OpenAi`,
     `Ollama`).
   - **Status cell**: `active` in `Theme.Semantic.Success` (green, bold) if
     this is the current provider. `ready` in `Theme.Semantic.Muted` (dim) if
     it has a stored API key but is not active. Empty if unconfigured.
   - **Model cell**: The stored `DefaultModel` from the profile if available,
     otherwise `ProviderDefaults.DefaultModelFor` in `Theme.Semantic.Muted`.
   - **API Key cell**: First 4 characters followed by `****` for stored keys.
     `--` in `Theme.Semantic.Muted` if no key is stored.

5. **Action Bar** (component pattern #29) -- Positioned at `Y = Pos.AnchorEnd(2)`.
   Shows available keyboard shortcuts. Priority order (rightmost dropped first
   at narrow widths):
   1. `Esc: Close` (always shown)
   2. `Enter: Show` (always shown)
   3. `s: Setup`
   4. `r: Remove`

## States

| State | Condition | Visual Difference |
|---|---|---|
| Active provider present | One provider is currently activated | That row shows "active" in green bold |
| Ready providers | Have API key but not active | Those rows show "ready" in dim |
| Unconfigured providers | No API key stored | Empty status cell, `--` for API key |
| No active provider | No provider is activated | No row shows "active" status |

Note: The list always shows all four `LlmProviderType` enum values regardless
of configuration state. There is no empty state -- the four provider types are
always present.

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

| Element | Token | Notes |
|---|---|---|
| Window border | `Theme.Modal.BorderScheme` | Blue border, rounded style |
| Selected row background | `Theme.List.SelectedBackground` | Accent blue |
| Selected row text | `Theme.List.SelectedText` | White on blue |
| Action bar text | `Theme.List.ActionBar` | Delegates to `Theme.Semantic.Muted` |
| "active" status | `Theme.Semantic.Success` (bold) | Green bold text |
| "ready" status | `Theme.Semantic.Muted` | Dark gray |
| Column headers | `Theme.Semantic.Muted` | Dark gray |
| Empty cells (`--`) | `Theme.Semantic.Muted` | Dark gray em-dash |
| Default model names | `Theme.Semantic.Muted` | Dim when showing fallback default |
| Data cell text | `Theme.Semantic.Default` | White |
| Row indicator | `\u25b6` (arrow) | Marks focused row |

## Interactive Elements

### Keyboard

| Key | Action |
|---|---|
| Up / k | Move selection up |
| Down / j | Move selection down |
| Enter | Show provider detail (opens detail modal -- see `/provider show`) |
| s | Setup/configure selected provider (opens setup flow -- see `/provider setup`) |
| r | Remove provider configuration (opens confirmation, then removes stored credentials) |
| Esc | Close the window |

Single-letter hotkeys are handled in the window's `OnKeyDown` override and fire
only when the `ListView` has focus (not when a sub-dialog is open).

### Actions

- **Enter (Show)**: Opens a detail modal window showing the full provider
  configuration (type, status, model, API key, OAuth status, capabilities).
  See `/provider show` screen spec.

- **s (Setup)**: Opens the provider setup flow for the selected provider. This
  suspends Terminal.Gui for interactive prompts (API key entry, OAuth flow).
  On completion, the list row updates to reflect the new configuration.

- **r (Remove)**: Opens a confirmation dialog asking whether to remove the
  stored credentials for the selected provider. "Cancel" is pre-focused. On
  confirm, the provider's stored API key is removed and the row updates to
  show empty status and `--` for API key. Cannot remove the currently active
  provider -- the `r` key is ignored or shows a brief warning: "Switch
  provider first."

## Behavior

1. **Provider enumeration**: Iterates over all `LlmProviderType` enum values.
   For each provider type, checks whether a stored `ProviderProfile` exists
   via `_providerConfigStore.GetAllAsync`.

2. **Status determination**: A provider is "active" if `_activeProvider` is
   configured and its `ProviderType` matches. A provider is "ready" if it has
   a stored API key but is not active. Otherwise, the status is empty.

3. **Model display**: Uses the stored `DefaultModel` from the profile if
   available, otherwise falls back to `ProviderDefaults.DefaultModelFor`. The
   fallback value is drawn in muted style to distinguish it from an explicitly
   configured model.

4. **API key masking**: Shows the first 4 characters followed by `****`.
   Keys shorter than 4 characters are shown in full (escaped). If no key is
   stored, shows `--` in muted.

5. **Sorting**: Fixed order matching the `LlmProviderType` enum declaration
   order.

6. **Remove guard**: The currently active provider cannot be removed. The user
   must switch to another provider first.

7. **Window type**: Modeless window. The agent continues processing in the
   background while the window is open.

## Edge Cases

- **No providers configured**: All rows show empty status and `--` for API
  key. The list still renders with all four `LlmProviderType` values. This
  is the normal initial state.

- **Narrow terminal (< 60 columns)**: Columns are dropped right-to-left to
  fit: API Key is dropped first, then Model. Provider and Status are always
  shown. Action bar drops less-important hints per pattern #29.

- **Ollama without API key**: Ollama does not require an API key for local
  usage, so `--` is the normal state. It can still show as "active" if
  configured via `/provider setup ollama`.

- **Remove active provider**: The `r` key is ignored for the active provider.
  A brief warning is shown: "Switch provider first."

- **Long model names**: Truncated with `...` at the column width boundary. The
  full model name is visible in the detail view (Enter).

- **Non-interactive/piped terminal**: Falls back to column-aligned plain text
  output to stdout. No window, no interactivity. Colors are omitted. Format:

  ```
  Provider   Status  Model                     API Key
  Gemini     active  gemini-2.5-pro            AIza****
  Anthropic          claude-sonnet-4-20250514  --
  OpenAi     ready   gpt-4o                    sk-4****
  Ollama             llama3.2                  --
  ```

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Interactive List | #28 | ListView with keyboard navigation |
| Action Bar | #29 | Shortcut hints at bottom of window |
| Modal Overlay (List variant) | #11 | Modeless window over conversation |
| Empty State | #21 | N/A -- list always has four rows |
