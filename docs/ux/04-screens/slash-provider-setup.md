# Screen: /provider setup

## Overview

Configures an LLM provider by prompting for a provider type, API key, and
model in a Multi-Step Wizard Dialog (component pattern #32), then activates
it as the current provider. This is the primary onboarding flow for connecting
BoydCode to an LLM service.

**Screen IDs**: PROV-03, PROV-04, PROV-05, PROV-06, PROV-07, PROV-08

## Trigger

`/provider setup [name]`

- If `name` is provided and matches a valid `LlmProviderType` (case-insensitive),
  the wizard opens at Step 2 (skipping provider selection).
- If `name` is omitted or invalid, the wizard opens at Step 1 for provider
  selection.
- Requires an interactive terminal for the API key and model inputs.

## Layout (80 columns)

### Step 1: Choose Provider

```
+-- Provider Setup -----------------------------------------+
|                                                            |
|  Step 1 of 3: Choose Provider                              |
|  --------------------------------------------------------  |
|                                                            |
|    Anthropic                                               |
|  > Gemini                                                  |
|    OpenAI                                                  |
|    Ollama                                                  |
|                                                            |
|                                                            |
|  [ Cancel ]                                    [ Next > ]  |
|                                                            |
+------------------------------------------------------------+
```

The ListView uses `Theme.List.SelectedBackground` and
`Theme.List.SelectedText` for the highlighted row. Up/Down arrows navigate.

### Step 2: Authentication

```
+-- Provider Setup -----------------------------------------+
|                                                            |
|  Step 2 of 3: Authentication                               |
|  --------------------------------------------------------  |
|                                                            |
|  API key:  [************************************        ]  |
|                                                            |
|  Model:    [gemini-2.5-pro                              ]  |
|                                                            |
|                                                            |
|  [ Cancel ]                          [ < Back ] [ Next > ] |
|                                                            |
+------------------------------------------------------------+
```

The API key field uses `TextField` with `Secret = true` so input is masked
with `*` characters. The Model field is pre-filled with the default model
from `ProviderDefaults.DefaultModelFor`.

### Step 2 -- Ollama (API Key Optional)

```
|  API key:  [                                            ]  |
|            (optional for local Ollama)                     |
|                                                            |
|  Model:    [llama3.2                                    ]  |
```

For Ollama, the API key field allows empty input. A dim hint "(optional for
local Ollama)" appears below the field.

### Step 2 -- Validation Error

```
|  API key:  [                                            ]  |
|            API key is required.                            |
```

For non-Ollama providers, the Next button validates that the API key is
non-empty. The error message uses `Theme.Semantic.Error` (bright red).

### Step 3: Confirm

```
+-- Provider Setup -----------------------------------------+
|                                                            |
|  Step 3 of 3: Confirm                                      |
|  --------------------------------------------------------  |
|                                                            |
|  Provider:   Gemini                                        |
|  Model:      gemini-2.5-pro                                |
|  API key:    ****...****                                   |
|                                                            |
|  This will set Gemini as the active provider.              |
|                                                            |
|  [ Cancel ]                          [ < Back ] [ Done ]   |
|                                                            |
+------------------------------------------------------------+
```

The review step shows a read-only summary. The API key is partially masked
(first 4 and last 4 characters visible, middle replaced with `...`). The
"Next" button is replaced with "Done" on this final step.

### After Done

```
  v Provider 'Gemini' configured and activated.
```

The success message is rendered in the conversation view after the wizard
dialog closes.

### With Inline Name (Step 1 Skipped)

When `/provider setup gemini` is typed, the wizard opens at Step 2 with the
provider pre-selected. The Back button on Step 2 is hidden since Step 1 was
skipped.

### Non-Interactive Errors

```
  Usage: /provider setup <name>
```

```
  Error: /provider setup requires an interactive terminal. Use --api-key instead.
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Step 1 | No valid name argument | ListView of provider types |
| Step 2 | Provider selected | API key (masked) + Model TextFields |
| Step 2 validation | Empty API key (non-Ollama) | Red error below API key field |
| Step 3 | After Step 2 Next | Read-only summary, "Done" button |
| Success | "Done" clicked | Dialog closes; success in conversation view |
| Non-interactive, no name | No name arg, non-interactive | Yellow usage hint |
| Non-interactive, after selection | Provider known but non-interactive | Red error suggesting --api-key |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Modal.BorderScheme` (blue border for wizard
dialog), `Theme.Semantic.Default` with `TextStyle.Bold` (step indicator text,
summary labels), `Theme.Semantic.Muted` (dim step separator rule, "(optional
for local Ollama)" hint, masked API key in summary),
`Theme.Semantic.Success` (green success message), `Theme.Semantic.Error`
(red validation errors and "Error:" prefix), `Theme.Semantic.Warning`
(yellow "Usage:" prefix), `Theme.Input.Text` (white text in TextFields),
`Theme.List.SelectedBackground` and `Theme.List.SelectedText` (provider
list highlight), `Theme.Symbols.Rule` (step separator character).

All interaction occurs within Terminal.Gui Dialogs. No Terminal.Gui
suspension or Spectre prompts are needed.

## Interactive Elements

| Element | Type | Context |
|---|---|---|
| Provider selection | ListView in Step 1 | All `LlmProviderType` values |
| API key | TextField with Secret = true in Step 2 | Masked input; required except Ollama |
| Model | TextField in Step 2 | Pre-filled with default from `ProviderDefaults` |
| Review summary | Labels in Step 3 | Read-only confirmation |

## Keyboard

| Key | Action |
|---|---|
| Up / Down | Navigate provider list in Step 1 |
| Tab | Move between fields within the current step |
| Shift+Tab | Move to previous field or button |
| Enter | Confirm (Next/Done when button focused) |
| Esc | Cancel entire wizard |
| Alt+B | Back (same as clicking Back button) |
| Alt+N | Next (same as clicking Next button) |

## Behavior

1. **Provider resolution**: If the trailing argument matches a valid
   `LlmProviderType` (via `Enum.TryParse` with `ignoreCase: true`), the
   wizard opens at Step 2 with that provider pre-selected. Otherwise, the
   wizard opens at Step 1 for selection.

2. **Interactive gate**: If the terminal is non-interactive, no wizard is
   shown. With no name argument, a usage hint is displayed. With a name
   argument, an error suggests using the `--api-key` CLI flag instead.

3. **API key validation**: For all providers except Ollama, the API key field
   requires non-empty input. Step 2's Next button validates this before
   advancing. For Ollama, empty input is accepted and normalized to `null`.

4. **Model default**: The Model field is pre-filled with the provider's
   default model from `ProviderDefaults.DefaultModelFor`. Pressing Tab
   past the field accepts the default.

5. **Activation**: When "Done" is clicked on Step 3, the provider profile is
   saved via `_providerConfigStore`, then a `LlmProviderConfig` is built and
   activated via `_activeProvider.Activate`. The status bar is updated to
   reflect the new provider and model.

6. **Last-used tracking**: The provider type is saved as the last-used provider
   via `_providerConfigStore.SetLastUsedProviderAsync`, so it will be
   auto-selected on the next app launch.

7. **Back navigation**: Back preserves all entered values. If Step 1 was
   skipped (inline name), the Back button is hidden on Step 2.

## Edge Cases

- **Overwriting existing config**: If the provider already has a stored
  profile, the new API key and model overwrite it. No confirmation is shown
  on the review step -- the summary shows what will be set.

- **Invalid provider name argument**: If `/provider setup xyz` is given and
  `xyz` doesn't match any `LlmProviderType`, the wizard opens at Step 1 for
  manual selection.

- **Empty API key for non-Ollama**: Step 2's Next button rejects empty input
  with an inline validation error. The user cannot advance to Step 3.

- **Cancel at any step**: Pressing Esc or clicking Cancel closes the wizard
  without saving. No confirmation is shown since nothing has been persisted
  yet.

- **Status bar update**: The status bar segments for provider and model are
  updated after activation. Other segments (project, branch, engine) are
  preserved.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Multi-Step Wizard | #32 | Overall wizard dialog structure (3 steps) |
| Selection Prompt | #12 (Dialog approach) | Provider type list in Step 1 |
| Form Dialog | #31 | API key + Model fields in Step 2, secret field |
| Status Message | #7 | Success, error, usage messages |
