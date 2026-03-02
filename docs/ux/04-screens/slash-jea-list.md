# Screen: /jea list

## Overview

The JEA profiles list screen opens an interactive list in a modeless Window
titled "JEA Profiles", showing all JEA (Just Enough Administration) profiles
with their language mode, command count, and module count. Users can navigate
the list, view profile details, edit, delete, or create new profiles.

The `_global` profile is always present and is labeled with a "(global)" suffix
in muted style.

**Screen IDs**: JEA-02, JEA-03

## Trigger

- User types `/jea list` or `/jea` (default subcommand) during an active
  session.
- Handled by `JeaSlashCommand.HandleListAsync()`.

## Route

Opens a modeless `Window` via the Interactive List pattern (component pattern
#28). The window floats over the conversation view. The agent continues working
in the background. The user dismisses with Esc.

## Layout (80 columns)

### With Profiles

```
+-- JEA Profiles -------------------------------------------+
|                                                            |
|  Name                Language Mode          Cmds  Modules  |
|  ▶ _global (global)  ConstrainedLanguage      28        3  |
|    security          FullLanguage               5        0  |
|    linting           ConstrainedLanguage        3        1  |
|    deployment        ConstrainedLanguage       12        2  |
|                                                            |
|  Enter: Show  e: Edit  d: Delete  n: New  Esc: Close      |
|                                                            |
+------------------------------------------------------------+
```

The highlighted row (first row by default) uses `Theme.List.SelectedBackground`
(blue) with `Theme.List.SelectedText` (white). The `▶` arrow indicator marks
the focused row.

### Empty State

```
+-- JEA Profiles -------------------------------------------+
|                                                            |
|                                                            |
|        No JEA profiles found.                              |
|        Press n to create one.                              |
|                                                            |
|                                                            |
|  n: New  Esc: Close                                        |
|                                                            |
+------------------------------------------------------------+
```

When the list is empty, the empty message is centered and drawn with
`Theme.Semantic.Muted` (dark gray). The Action Bar retains `n: New` since
creating a profile is still available. Note: this state should be rare because
the `_global` profile is seeded automatically on first access.

### Anatomy

1. **Window** -- Modeless `Window` with `Theme.Modal.BorderScheme` (blue border),
   title "JEA Profiles", rounded border style, centered at 80% width / 70%
   height.

2. **Column Header** -- Static `Label` showing column names. Drawn with
   `Theme.Semantic.Muted` (dark gray). Columns:
   - **Name** -- left-aligned, primary column
   - **Language Mode** -- left-aligned, PowerShell language mode enum value
   - **Cmds** -- right-aligned, command entry count
   - **Modules** -- right-aligned, imported module count

3. **List View** -- `ListView` with one row per JEA profile. Scrollable when
   items exceed viewport height. The focused row uses
   `Theme.List.SelectedBackground` and `Theme.List.SelectedText`. The `▶`
   arrow indicator (`\u25b6`) marks the focused row in column 2.

4. **Row Content** --
   - **Name cell**: Profile name. The `_global` profile shows
     `_global (global)` with the suffix in `Theme.Semantic.Muted`.
   - **Language Mode cell**: PowerShell language mode enum name
     (`ConstrainedLanguage`, `FullLanguage`, `NoLanguage`, `RestrictedLanguage`).
   - **Cmds cell**: Integer count, right-aligned. Shows the total number of
     command entries (both allowed and denied).
   - **Modules cell**: Integer count, right-aligned. Shows imported module count.

5. **Action Bar** (component pattern #29) -- Positioned at `Y = Pos.AnchorEnd(2)`.
   Shows available keyboard shortcuts. Priority order (rightmost dropped first
   at narrow widths):
   1. `Esc: Close` (always shown)
   2. `Enter: Show` (always shown)
   3. `e: Edit`
   4. `d: Delete`
   5. `n: New`

## States

| State | Condition | Visual Difference |
|---|---|---|
| With profiles | At least one profile exists | List with rows per profile |
| Empty | No profiles exist (rare -- global is auto-seeded) | Centered empty state message, action bar shows `n: New` and `Esc: Close` |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

| Element | Token | Notes |
|---|---|---|
| Window border | `Theme.Modal.BorderScheme` | Blue border, rounded style |
| Selected row background | `Theme.List.SelectedBackground` | Accent blue |
| Selected row text | `Theme.List.SelectedText` | White on blue |
| Action bar text | `Theme.List.ActionBar` | Delegates to `Theme.Semantic.Muted` |
| Column headers | `Theme.Semantic.Muted` | Dark gray |
| `(global)` suffix | `Theme.Semantic.Muted` | Dark gray |
| Empty state message | `Theme.Semantic.Muted` | Dark gray centered text |
| Data cell text | `Theme.Semantic.Default` | White |
| Row indicator | `\u25b6` (arrow) | Marks focused row |

## Interactive Elements

### Keyboard

| Key | Action |
|---|---|
| Up / k | Move selection up |
| Down / j | Move selection down |
| Enter | Show profile detail (opens detail modal -- see `/jea show`) |
| e | Edit selected profile (opens edit flow -- see `/jea edit`) |
| d | Delete selected profile (opens Delete Confirmation dialog, pattern #15) |
| n | Create new profile (opens create flow -- see `/jea create`) |
| Esc | Close the window |

Single-letter hotkeys are handled in the window's `OnKeyDown` override and fire
only when the `ListView` has focus (not when a sub-dialog is open).

### Actions

- **Enter (Show)**: Opens a detail modal window showing the full profile
  configuration (name, language mode, allowed commands, denied commands,
  imported modules). See `/jea show` screen spec.

- **e (Edit)**: Opens the profile edit flow for the selected profile. This
  suspends Terminal.Gui for interactive prompts. On completion, the list row
  updates to reflect changes.

- **d (Delete)**: Opens a Delete Confirmation dialog (pattern #15) showing the
  profile name, command count, and module count. "Cancel" is pre-focused. On
  confirm, the row is removed from the list. Cannot delete the `_global`
  profile -- the `d` key is ignored or shows a brief warning.

- **n (New)**: Opens the profile creation flow. On completion, the new profile
  appears in the list.

## Behavior

1. **Global profile seeding**: Before listing, `EnsureGlobalProfileAsync` is
   called. If the `_global` profile does not exist in the store, it is seeded
   from `BuiltInJeaProfile.Instance` (28 commands, ConstrainedLanguage mode).

2. **Profile enumeration**: All profile names are loaded via
   `_store.ListNamesAsync`, then each profile is loaded individually.

3. **Global label**: The `_global` profile name is detected by comparison with
   `BuiltInJeaProfile.GlobalName` and receives a `(global)` suffix in muted
   style.

4. **Sorting**: Profiles are listed alphabetically by name, with `_global`
   always appearing first.

5. **Counts**: `Entries.Count` shows the total number of command entries
   (both allowed and denied). `Modules.Count` shows imported modules.

6. **Delete guard**: The `_global` profile cannot be deleted. Attempting to
   press `d` on it ignores the keypress or shows a brief status message.

7. **Window type**: Modeless window. The agent continues processing in the
   background while the window is open.

## Edge Cases

- **No profiles at all**: After global seeding, there should always be at
  least the `_global` profile. The empty state would only occur if the global
  seeding fails (e.g., filesystem error) or if the store is corrupted.

- **Profile load failure**: If `_store.LoadAsync` returns null for a listed
  name, that profile is silently skipped.

- **Many profiles (> viewport height)**: `ListView` scrolls natively.
  Practical profile counts are expected to be small (under 20).

- **Long profile names**: Truncated with `...` at the column width boundary.
  The full name is visible in the detail view (Enter).

- **Narrow terminal (< 60 columns)**: Columns are dropped right-to-left to
  fit: Modules is dropped first, then Cmds, then Language Mode. Name is
  always shown. Action bar drops less-important hints per pattern #29.

- **Delete global profile**: The `d` key is ignored for the `_global` profile.

- **Non-interactive/piped terminal**: Falls back to column-aligned plain text
  output to stdout. No window, no interactivity. Colors are omitted. Format:

  ```
  Name                Language Mode          Cmds  Modules
  _global (global)    ConstrainedLanguage      28        3
  security            FullLanguage              5        0
  linting             ConstrainedLanguage       3        1
  ```

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Interactive List | #28 | ListView with keyboard navigation |
| Action Bar | #29 | Shortcut hints at bottom of window |
| Modal Overlay (List variant) | #11 | Modeless window over conversation |
| Delete Confirmation | #15 | Confirm before deleting a profile |
| Empty State | #21 | "No JEA profiles found." message |
