# Screen: /provider remove

## Overview

Removes a stored provider configuration. Uses a Terminal.Gui Dialog with
ListView (component pattern #12, Dialog approach) for provider selection when
no name argument is given, followed by a confirmation MessageBox (component
pattern #15, Dialog approach) before deletion. If the removed provider is the
currently active provider, a warning is shown indicating it will remain active
for the current session but will not persist across restarts.

**Screen IDs**: PROV-11, PROV-12, PROV-13, PROV-14

## Trigger

`/provider remove [name]`

- If `name` is provided and matches a valid `LlmProviderType` (case-insensitive),
  it is used directly (skips to confirmation).
- If `name` is omitted or invalid, a Selection Dialog lists all provider types.
- Non-interactive mode requires the name argument.

## Layout (80 columns)

### Provider Selection Dialog (when name omitted)

```
+-- Select Provider to Remove ------------------------------+
|                                                            |
|    Anthropic                                               |
|  > Gemini                                                  |
|    OpenAI                                                  |
|    Ollama                                                  |
|                                                            |
|                              [ Cancel ]  [ Ok ]            |
|                                                            |
+------------------------------------------------------------+
```

The ListView uses `Theme.List.SelectedBackground` and
`Theme.List.SelectedText` for the highlighted row. Up/Down arrows navigate.
Enter confirms the selection. Esc cancels and returns to the conversation.

### Confirmation MessageBox

After selecting a provider (or providing one inline), a confirmation
MessageBox appears:

```
+-- Remove Provider ----------------------------------------+
|                                                            |
|  Remove provider 'Gemini'?                                 |
|                                                            |
|  This will delete the stored API key and model             |
|  configuration. You can reconfigure later with             |
|  /provider setup.                                          |
|                                                            |
|                            [ Cancel ]  [ Remove ]          |
|                                                            |
+------------------------------------------------------------+
```

The "Cancel" button is pre-focused (safe default). The "Remove" button uses
`Theme.Semantic.Error` (bright red) styling to indicate a destructive action.
The provider name is drawn bold.

### Confirmation -- Active Provider Warning

When the selected provider is the currently active provider, an additional
warning appears in the MessageBox:

```
+-- Remove Provider ----------------------------------------+
|                                                            |
|  Remove provider 'Gemini'?                                 |
|                                                            |
|  Warning: 'Gemini' is the active provider. It will         |
|  remain active for this session but won't persist.         |
|                                                            |
|  This will delete the stored API key and model             |
|  configuration. You can reconfigure later with             |
|  /provider setup.                                          |
|                                                            |
|                            [ Cancel ]  [ Remove ]          |
|                                                            |
+------------------------------------------------------------+
```

The warning line uses `Theme.Semantic.Warning` (yellow).

### Success (Not Active)

After confirming removal, the MessageBox closes and a success message is
rendered in the conversation view:

```
  v Provider 'Anthropic' removed.
```

### Success (Active Provider)

```
  Warning: 'Gemini' is the active provider. It will remain active for this
  session but won't persist.
  v Provider 'Gemini' removed.
```

### Cancelled

After clicking Cancel or pressing Esc in either dialog:

```
  Cancelled.
```

### Non-Interactive Usage Hint

```
  Usage: /provider remove <name>
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Provider selection | No valid name argument, interactive | Selection Dialog with all provider types |
| Confirmation | Provider selected or inline | MessageBox with provider name + Cancel/Remove |
| Confirmation (active) | Provider is active | MessageBox includes yellow warning line |
| Removed (not active) | Confirmed, not active | Green success message |
| Removed (active) | Confirmed, is active | Yellow warning + green success |
| Cancelled | Cancel clicked or Esc pressed | Dim "Cancelled." |
| Non-interactive, no name | No name arg, non-interactive | Yellow usage hint |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Modal.BorderScheme` (blue border for both
dialogs), `Theme.List.SelectedBackground` and `Theme.List.SelectedText`
(provider list highlight), `Theme.Semantic.Error` (bright red "Remove"
button), `Theme.Semantic.Default` with `TextStyle.Bold` (bold provider
name in confirmation), `Theme.Semantic.Success` (green success message),
`Theme.Semantic.Warning` (yellow active provider warning and "Usage:"
prefix), `Theme.Semantic.Muted` (dim "Cancelled." message).

All interaction occurs within Terminal.Gui Dialogs. No Terminal.Gui
suspension or Spectre prompts are needed.

## Interactive Elements

| Element | Type | Context |
|---|---|---|
| Provider selection | Dialog + ListView (pattern #12) | No valid name arg, interactive |
| Removal confirmation | MessageBox (pattern #15) | After provider selected |

## Keyboard

### Selection Dialog

| Key | Action |
|---|---|
| Up / Down | Navigate provider list |
| Enter | Confirm selection |
| Esc | Cancel (return to conversation) |

### Confirmation MessageBox

| Key | Action |
|---|---|
| Enter | Confirm focused button (Cancel by default) |
| Tab | Switch between Cancel and Remove buttons |
| Esc | Cancel (same as clicking Cancel) |

## Behavior

1. **Provider resolution**: Same logic as `/provider setup` -- inline argument
   is parsed via `Enum.TryParse`, falling back to a Selection Dialog.

2. **Confirmation**: After provider selection, a confirmation MessageBox is
   shown. The "Cancel" button is pre-focused. The MessageBox shows what will
   be deleted and mentions `/provider setup` for reconfiguration.

3. **Active provider check**: If the selected provider matches the active
   provider, a yellow warning line is included in the MessageBox message. The
   warning is also shown in the conversation view after removal.

4. **Store removal**: `_providerConfigStore.RemoveAsync` deletes the stored
   `ProviderProfile` for the given provider type.

5. **Session persistence**: The active provider is not deactivated. The
   in-memory `_activeProvider` singleton retains its reference, so the provider
   remains functional for the remainder of the session. On the next app launch,
   the provider will not be available.

6. **Success message**: Always shown after confirmed removal, even when the
   active-provider warning also appears.

## Edge Cases

- **Removing a provider with no stored config**: The
  `_providerConfigStore.RemoveAsync` call is a no-op if the profile does not
  exist. The success message is still shown. No error or warning.

- **Non-interactive terminal**: If no name argument is provided and the
  terminal is non-interactive, shows the usage hint. If a name is provided,
  the confirmation MessageBox is skipped and removal proceeds directly (same
  as the non-interactive bypass pattern used by `/conversations delete`).

- **Invalid provider name**: If `/provider remove xyz` is given and `xyz`
  does not match a `LlmProviderType`, the Selection Dialog is shown (in
  interactive mode) or the usage hint is shown (in non-interactive mode).

- **All providers removed**: After removing all providers, `/provider list`
  shows all four provider types with empty status and "not set" API keys.
  The app still functions if a provider was activated earlier in the session.

- **Cancel at either step**: Cancelling the Selection Dialog or the
  confirmation MessageBox both result in "Cancelled." and no changes.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Selection Prompt | #12 (Dialog approach) | Provider type selection |
| Delete Confirmation | #15 (Dialog approach) | Removal confirmation with Cancel/Remove |
| Status Message | #7 | Success, warning, cancelled, usage messages |
