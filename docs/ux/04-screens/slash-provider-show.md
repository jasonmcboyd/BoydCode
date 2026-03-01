# Screen: /provider show

## Overview

Displays the currently active provider's details in a bordered panel,
including provider type, model name, and context window size.

**Screen IDs**: PROV-09, PROV-10

## Trigger

`/provider show`

No arguments are accepted -- this always shows the active provider.

## Layout (80 columns)

### Active Provider

    ╭─ Active Provider ────────────────────────────────────────────────────────╮
    │ Provider:       Gemini                                                   │
    │ Model:          gemini-2.5-pro                                           │
    │ Context window: 1,048,576 tokens                                        │
    ╰──────────────────────────────────────────────────────────────────────────╯

### No Active Provider

    No provider is currently active. Use /provider setup to configure one.

## States

| State | Condition | Visual Difference |
|---|---|---|
| Active provider | `_activeProvider.IsConfigured` is true | Rounded-border Panel with provider details |
| No active provider | `_activeProvider.IsConfigured` is false | Yellow warning text with setup guidance |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | "Provider:", "Model:", "Context window:" labels; "Active Provider" panel header |
| `[yellow]` | warning-yellow | "No provider is currently active..." message |

## Interactive Elements

None. This screen is purely static output.

## Behavior

1. **Active check**: If `_activeProvider.IsConfigured` is false, a yellow
   warning message is shown with guidance to use `/provider setup`.

2. **Panel construction**: Three key-value lines are built using inline
   `[bold]` markup for labels. The lines are joined with newlines and wrapped
   in a `Markup` renderable inside a `Panel`.

3. **Panel styling**: The panel uses `BoxBorder.Rounded` with a `[bold]Active
   Provider[/]` header and `Padding(1, 0, 1, 0)` (1 char left/right, 0
   top/bottom).

4. **Context window**: The `MaxContextWindowTokens` value from the provider's
   `Capabilities` is formatted with thousands separators using `"N0"` format
   and `InvariantCulture`.

## Edge Cases

- **Narrow terminal**: The panel adapts to terminal width. At very narrow
  widths (< 40 columns), the content lines may wrap inside the panel. The
  Rounded border adds 4 characters of overhead (2 per side).

- **Provider without capabilities**: If `_activeProvider.Provider` is null
  despite `IsConfigured` being true, this would throw a `NullReferenceException`.
  In practice, `IsConfigured` implies both `Config` and `Provider` are set.

- **Very long model names**: Long model names wrap naturally inside the panel
  cell. The `[bold]Model:[/]` prefix occupies a fixed width due to the padded
  formatting (`{spaces}` between label and value).

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | Section 1 | Yellow warning for no active provider |

The panel is a one-off renderable, not covered by a standard component
pattern. This is the appropriate approach per the Consolidation Principle --
unique panel layouts stay as raw Spectre calls.

