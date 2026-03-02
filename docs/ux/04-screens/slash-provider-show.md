# Screen: /provider show

## Overview

Displays the currently active provider's details in a modeless Detail Modal
window (component pattern #11, Variant C), including provider type, model
name, and context window size. Content is drawn using Terminal.Gui native
drawing (`SetAttribute`, `Move`, `AddStr`) with structured key-value layout.

**Screen IDs**: PROV-09, PROV-10

## Trigger

`/provider show`

No arguments are accepted -- this always shows the active provider.

## Layout (80 columns)

### Active Provider

```
+-- Active Provider ----------------------------------------+
|                                                            |
|  Provider        Gemini                                    |
|  Model           gemini-2.5-pro                            |
|  Context window  1,048,576 tokens                          |
|                                                            |
|  Esc to dismiss                                            |
|                                                            |
+------------------------------------------------------------+
```

### No Active Provider

```
No provider is currently active. Use /provider setup to configure one.
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Active provider | `_activeProvider.IsConfigured` is true | Detail Modal window with provider info pairs |
| No active provider | `_activeProvider.IsConfigured` is false | Yellow warning text in conversation view |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

- `Theme.Modal.BorderScheme` -- blue border on the modeless window
- `Theme.Semantic.Muted` -- dim labels in info pairs ("Provider", "Model",
  "Context window"), "Esc to dismiss"
- `Theme.Semantic.Info` -- cyan values in info pairs (provider type, model
  name, token count)
- `Theme.Semantic.Warning` -- yellow "No provider is currently active..."
  message

## Interactive Elements

None. This screen is purely static output.

## Behavior

1. **Active check**: If `_activeProvider.IsConfigured` is false, a yellow
   warning message is shown in the conversation view with guidance to use
   `/provider setup`. No window is opened.

2. **Window construction**: `ShowDetailModal` opens a Terminal.Gui modeless
   `Window` with the title "Active Provider" and a blue border
   (`Theme.Modal.BorderScheme`).

3. **Native drawing layout**: The window's inner `View` overrides
   `OnDrawingContent` to draw three info pairs using the Info Grid pattern
   (pattern #9):

   - **Provider**: Label in `Theme.Semantic.Muted`, value (provider type
     name) in `Theme.Semantic.Info`.
   - **Model**: Label in `Theme.Semantic.Muted`, value (model name) in
     `Theme.Semantic.Info`.
   - **Context window**: Label in `Theme.Semantic.Muted`, value
     (`MaxContextWindowTokens` formatted with thousands separators using
     `"N0"` format and `InvariantCulture`, followed by " tokens") in
     `Theme.Semantic.Info`.

4. **Layout**: Labels start at `x = 2`. Values are aligned at the label pad
   offset (`Theme.Layout.InfoLabelPad` adjusted for the longest label,
   "Context window" at 16 chars).

5. **Dismiss hint**: "Esc to dismiss" is drawn at the bottom of the content
   in `Theme.Semantic.Muted`. The `ActivityBarView` transitions to
   `ActivityState.Modal` while the window is open.

## Edge Cases

- **Narrow terminal**: The modal window is sized to fit content up to the
  terminal width (capped at 90% per pattern #11 sizing rules). At very narrow
  widths (< 40 columns), content lines may wrap inside the window.

- **Provider without capabilities**: If `_activeProvider.Provider` is null
  despite `IsConfigured` being true, this would throw a `NullReferenceException`.
  In practice, `IsConfigured` implies both `Config` and `Provider` are set.

- **Very long model names**: Long model names wrap naturally inside the window.
  The label pad offset keeps the value column aligned.

## Non-TUI Fallback

When running in non-interactive/piped mode (no Terminal.Gui), the provider
detail is rendered as plain text to stdout. Info pairs use string-padded
columns without color.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Modal Overlay (Detail Modal) | #11, Variant C | Modeless window with native drawing layout |
| Info Grid | #9 | Provider metadata key-value display |
| Status Message | #7 | Yellow warning for no active provider |
