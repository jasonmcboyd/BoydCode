# Screen: /jea list

## Overview

Lists all JEA (Just Enough Administration) profiles in a tabular format
showing name, language mode, command count, and module count. The `_global`
profile is always present and is labeled with a "(global)" suffix.

**Screen IDs**: JEA-02, JEA-03

## Trigger

`/jea list`

## Layout (80 columns)

### With Profiles

    Name               Language Mode          Commands   Modules
    ────────────────────────────────────────────────────────────────────────
    _global (global)   ConstrainedLanguage          28         3
    security           FullLanguage                  5         0
    linting            ConstrainedLanguage           3         1

### Empty State

    No JEA profiles found.
    Create one with /jea create <name>

## States

| State | Condition | Visual Difference |
|---|---|---|
| Table with profiles | At least one profile exists | SimpleTable with rows per profile |
| Empty | No profiles exist | Plain text message + dim hint |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | Table column headers (via `SpectreHelpers.SimpleTable`) |
| `[dim]` | dim (2.2) | "(global)" suffix, empty state hint |

## Interactive Elements

None. This screen is purely static output.

## Behavior

1. **Global profile seeding**: Before listing, `EnsureGlobalProfileAsync` is
   called. If the `_global` profile does not exist in the store, it is seeded
   from `BuiltInJeaProfile.Instance` (28 commands, ConstrainedLanguage mode).

2. **Profile enumeration**: All profile names are loaded via
   `_store.ListNamesAsync`, then each profile is loaded individually.

3. **Global label**: The `_global` profile name is detected by comparison with
   `BuiltInJeaProfile.GlobalName` and receives a `(global)` suffix in dim.

4. **Column alignment**: The "Commands" and "Modules" columns are right-aligned.
   Name and Language Mode are left-aligned.

5. **Counts**: `Entries.Count` shows the total number of command entries
   (both allowed and denied). `Modules.Count` shows imported modules.

## Edge Cases

- **No profiles at all**: After global seeding, there should always be at
  least the `_global` profile. The empty state would only occur if the global
  seeding fails (e.g., filesystem error) or if the store is corrupted.

- **Profile load failure**: If `_store.LoadAsync` returns null for a listed
  name, that profile is silently skipped.

- **Narrow terminal**: Spectre's table handles wrapping. The "Language Mode"
  column contains enum names up to 19 characters ("ConstrainedLanguage"),
  which fits comfortably at 80 columns.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Simple Table | Section 4 | JEA profile list table |
| Empty State | Section 13 | "No JEA profiles found" + hint |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| List flow | `Commands/JeaSlashCommand.cs` | `HandleListAsync` | 128-166 |
| Global seeding | `Commands/JeaSlashCommand.cs` | `EnsureGlobalProfileAsync` | 107-122 |
| Empty check | `Commands/JeaSlashCommand.cs` | `HandleListAsync` | 134-139 |
| Table construction | `Commands/JeaSlashCommand.cs` | `HandleListAsync` | 141-143 |
| Row population | `Commands/JeaSlashCommand.cs` | `HandleListAsync` | 145-163 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
