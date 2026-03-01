# Screen: /context show

## Overview

The context show screen is the most visually complex screen in the application.
It renders a full-width stacked bar chart showing token utilization across five
categories, a legend with aligned columns, and three tree-style breakdowns
(system prompt, messages, tools). The entire display is computed from in-memory
token estimates and rendered as static output into the scrollback.

This screen answers the question: "How much of my context window am I using,
and where is it going?"

**Screen IDs**: CTX-02, CTX-03

## Trigger

- User types `/context show` during an active session.
- Handled by `ContextSlashCommand.HandleShow()`.

## Layout (80 columns)

```
(blank line)
  Gemini . gemini-2.5-pro . 12.4k/1.0M tokens (1.2%)
(blank line)
  ████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
(blank line)
  Estimated usage by category
    ■ System prompt        2.1k tokens  (0.2%)
    ■ Tools                  458 tokens  (0.0%)
    ■ Messages             9.8k tokens  (1.0%)
    ■ Free space         887.6k tokens  (88.8%)
    ■ Compact buffer     100.0k tokens  (10.0%)
(blank line)
  System prompt . 2,134 tokens
  ├── Meta prompt           1,892 tokens
  └── Session prompt          242 tokens
(blank line)
  Messages . 37 messages, 9.8k tokens
  ├── User text             8 messages, 1.2k tokens
  ├── Assistant text        7 messages, 3.4k tokens
  ├── Tool calls           11 calls, 2.1k tokens
  └── Tool results         11 results, 3.1k tokens
(blank line)
  Tools . 1 tool, 458 tokens
  └── Shell                   458 tokens
(blank line)
```

## Layout (120 columns)

The layout is identical in structure. The stacked bar is a fixed 72 characters
wide regardless of terminal width. The only difference is that tree lines and
legend entries have more trailing whitespace. All content is left-aligned with
a 2-space indent; nothing expands to fill wider terminals.

```
(blank line)
  Gemini . gemini-2.5-pro . 12.4k/1.0M tokens (1.2%)
(blank line)
  ████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
(blank line)
  Estimated usage by category
    ■ System prompt        2.1k tokens  (0.2%)
    ■ Tools                  458 tokens  (0.0%)
    ■ Messages             9.8k tokens  (1.0%)
    ■ Free space         887.6k tokens  (88.8%)
    ■ Compact buffer     100.0k tokens  (10.0%)
(blank line)
  System prompt . 2,134 tokens
  ├── Meta prompt           1,892 tokens
  └── Session prompt          242 tokens
(blank line)
  Messages . 37 messages, 9.8k tokens
  ├── User text             8 messages, 1.2k tokens
  ├── Assistant text        7 messages, 3.4k tokens
  ├── Tool calls           11 calls, 2.1k tokens
  └── Tool results         11 results, 3.1k tokens
(blank line)
  Tools . 1 tool, 458 tokens
  └── Shell                   458 tokens
(blank line)
```

### Anatomy

1. **Header line** -- 2-space indent, bold provider name, dim middle dot
   separator, bold model name, dim middle dot, color-coded token usage
   fraction and percentage.
2. **Blank line**.
3. **Stacked bar chart** -- 2-space indent, 72-character fixed-width bar
   composed of five colored segments using Unicode block characters.
4. **Blank line**.
5. **Legend heading** -- 2-space indent, bold "Estimated usage by category".
6. **Legend rows** (5 rows) -- 4-space indent, colored square indicator,
   left-aligned label (18 chars), right-aligned token count (8 chars),
   " tokens  ", parenthesized percentage.
7. **Blank line**.
8. **System prompt section** -- 2-space indent, blue bold "System prompt",
   dim middle dot, token count.
9. **System prompt tree** -- 2-space indent, tree connectors, label (20 chars
   pad), value.
10. **Blank line**.
11. **Messages section** -- 2-space indent, green bold "Messages", dim middle
    dot, message count and token count.
12. **Messages tree** -- 4 tree lines: user text, assistant text, tool calls,
    tool results. Each shows count and token estimate.
13. **Blank line**.
14. **Tools section** -- 2-space indent, mediumpurple1 bold "Tools", dim
    middle dot, tool count and token count.
15. **Tools tree** -- 1 tree line: "Shell" with token count.
16. **Blank line**.

### Stacked Bar Chart Detail

The bar is exactly 72 characters wide, rendered at a 2-space indent. It uses
five segments in this order:

| Segment | Color | Character | Meaning |
|---|---|---|---|
| System prompt | blue | U+2588 (full block) | Meta prompt + session prompt tokens |
| Tools | mediumpurple1 | U+2588 (full block) | Tool definition schema tokens |
| Messages | green | U+2588 (full block) | All conversation message tokens |
| Free space | grey | U+2591 (light shade) | Available tokens before compact buffer |
| Compact buffer | darkorange | U+2588 (full block) | Reserved headroom for auto-compaction |

**Proportional sizing algorithm:**

1. Count non-zero segments. Reserve 1 character minimum for each.
2. Distribute the remaining `72 - nonZeroCount` characters proportionally
   by token count: `1 + (tokens * remaining / totalTokens)`.
3. Sum the assigned widths. If the total differs from 72, adjust the free
   space segment (index 3) to absorb the difference. If free space is zero,
   adjust the largest segment instead.
4. Clamp any negative width to 0.

This ensures every non-zero category is visible even when it represents a
tiny fraction of the total (e.g., 458 tokens out of 1M still gets 1 char).

### Legend Alignment

Each legend row is formatted with `string.Format`:

```
"    [{color}]{indicator}[/] {label,-18}{tokenStr,8} tokens  ({percent})"
```

- 4-space indent for nesting under the heading.
- Colored square indicator (U+25A0) in the segment's color.
- Label left-padded to 18 characters.
- Token count right-padded to 8 characters (uses `FormatCompact`: e.g.,
  `2.1k`, `887.6k`, `1.0M`, or raw number for < 1000).
- Fixed string " tokens  (".
- Percentage from `FormatPercent` (e.g., `0.2%`, `88.8%`).
- Closing ")".

### Tree Breakdown Format

Each tree line is formatted with `string.Format`:

```
"  [dim]{connector}[/] {label,-20}{value}"
```

- 2-space indent.
- Dim tree connector: `\u251c\u2500\u2500` for non-last children,
  `\u2514\u2500\u2500` for last child.
- Label left-padded to 20 characters (Markup-escaped).
- Value string (Markup-escaped).

### Token Estimation

All token counts are estimates using a simple `chars / 4` heuristic:

- **String tokens**: `text.Length / 4`.
- **Tool definition tokens**: `(name + description + sum(param.name + type + description)) / 4`.
- **Message breakdown**: Iterates all `ConversationMessage` content blocks,
  categorizing by block type and message role. `TextBlock` text is `/4`,
  `ToolUseBlock` is `(name + args JSON) / 4`, `ToolResultBlock` is
  `content / 4`, `ImageBlock` is a fixed 250 tokens.

### Usage Color Thresholds

The token usage percentage in the header line uses threshold-based coloring:

| Threshold | Color | Token |
|---|---|---|
| < 50% | green | usage-ok |
| 50-79% | yellow | usage-warn |
| >= 80% | red | usage-critical |

## States

| State | Condition | Visual Difference |
|---|---|---|
| Normal (low usage) | < 50% context used | Header percentage in green; large grey free space segment in bar |
| Warning (medium usage) | 50-79% context used | Header percentage in yellow; smaller free space segment |
| Critical (high usage) | >= 80% context used | Header percentage in red; minimal or no free space segment |
| Empty conversation | 0 messages | Messages section shows "0 messages, 0 tokens"; tree lines show 0 for all categories; bar shows only system prompt + tools + free space |
| No session | Session is null | Red error: "No active session." (CTX-03) |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | Provider name, model name, "Estimated usage by category" heading |
| `[dim]` | dim (2.2) | Middle dot separators, tree connectors |
| `[blue bold]` | chart-blue + bold (1.3, 2.2) | "System prompt" section heading |
| `[green bold]` | chart-green + bold (1.3, 2.2) | "Messages" section heading |
| `[mediumpurple1 bold]` | chart-mediumpurple1 + bold (1.3, 2.2) | "Tools" section heading |
| `[blue]` | chart-blue (1.3) | System prompt bar segment, legend square |
| `[mediumpurple1]` | chart-mediumpurple1 (1.3) | Tools bar segment, legend square |
| `[green]` | chart-green (1.3) | Messages bar segment, legend square; also usage-ok header color |
| `[grey]` | chart-grey (1.3) | Free space bar segment (light shade char), legend square |
| `[darkorange]` | chart-darkorange (1.3) | Compact buffer bar segment, legend square |
| `[yellow]` | usage-warn (1.4) | Header percentage when 50-79% |
| `[red]` | usage-critical (1.4) | Header percentage when >= 80%; also error message prefix |

## Interactive Elements

None. This is a purely static render. No prompts, no keyboard interaction.

## Behavior

- **Token source**: The context limit comes from `ILlmProvider.Capabilities
  .MaxContextWindowTokens` if available, falling back to `AppSettings
  .ContextWindowTokenLimit`. The compact buffer is `contextLimit *
  CompactionThresholdPercent / 100` subtracted from the context limit.

- **Rendering**: All output is rendered via `AnsiConsole.MarkupLine` in a
  single synchronous pass. The stacked bar is built in a `StringBuilder`
  and emitted as one markup line. No animation, no progressive rendering.

- **Free space calculation**: `max(0, contextLimit - totalUsed - bufferTokens)`.
  If total usage exceeds `contextLimit - buffer`, free space is 0 and the bar
  shows no grey segment.

- **Compact number formatting**: `FormatCompact` renders numbers as:
  - `>= 1,000,000`: `{value / 1M:F1}M` (e.g., `1.0M`)
  - `>= 1,000`: `{value / 1k:F1}k` (e.g., `12.4k`)
  - `< 1,000`: raw integer (e.g., `458`)

## Edge Cases

- **Narrow terminal (< 72 columns)**: The stacked bar is a fixed 72
  characters plus 2-space indent = 74 characters minimum. At terminals
  narrower than 74 columns, the bar wraps to the next line, breaking the
  visual. The legend rows are approximately 55 characters and fit at 60+
  columns. Tree lines are approximately 45 characters and fit comfortably.
  No adaptive layout exists for narrow terminals.

- **Zero context limit**: If `contextLimit <= 0`, the `RenderStackedBar`
  method returns immediately (no bar rendered). The header line shows
  `0/0 tokens (NaN%)` -- this is a degenerate state that should not occur
  in normal usage.

- **All tokens in one category**: The proportional algorithm still assigns
  1 character minimum to other non-zero categories, and adjusts the dominant
  category to compensate. A category with 0 tokens gets 0 characters.

- **Very large token counts**: `FormatCompact` handles millions (`1.0M`).
  The right-alignment in the legend accommodates up to 8 characters for the
  token string.

- **No messages**: The message breakdown tree shows all zeros. The bar has
  no green segment. This is the expected state at session start.

- **Non-interactive/piped terminal**: Renders normally. Spectre strips markup
  when stdout is redirected, so colors are lost but the layout (spaces,
  alignment, Unicode characters) is preserved.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | Section 1 | Error message for no active session |
| Token Usage Display | Section 16 | Header line token count rendering |
| Context Tree Pattern | Composite pattern 10.6 (06-style-tokens.md) | System prompt, messages, and tools breakdowns |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| HandleShow entry point | `Commands/ContextSlashCommand.cs` | `HandleShow` | 88-210 |
| Token category computation | `Commands/ContextSlashCommand.cs` | `HandleShow` | 100-125 |
| Header line rendering | `Commands/ContextSlashCommand.cs` | `HandleShow` | 127-148 |
| Stacked bar rendering | `Commands/ContextSlashCommand.cs` | `RenderStackedBar` | 216-283 |
| Legend rendering | `Commands/ContextSlashCommand.cs` | `RenderLegend` | 285-298 |
| System prompt breakdown | `Commands/ContextSlashCommand.cs` | `HandleShow` | 165-173 |
| Message breakdown | `Commands/ContextSlashCommand.cs` | `HandleShow` | 175-199 |
| Tool inventory | `Commands/ContextSlashCommand.cs` | `HandleShow` | 201-209 |
| Tree line renderer | `Commands/ContextSlashCommand.cs` | `RenderTreeLine` | 300-309 |
| Message breakdown computation | `Commands/ContextSlashCommand.cs` | `ComputeMessageBreakdown` | 321-375 |
| Token estimation | `Commands/ContextSlashCommand.cs` | `EstimateToolDefinitionTokens`, `EstimateStringTokens` | 381-391 |
| Compact number formatting | `SpectreHelpers.cs` | `FormatCompact` | 347-355 |
| Percent formatting | `SpectreHelpers.cs` | `FormatPercent` | 357-358 |
| No session error | `Commands/ContextSlashCommand.cs` | `HandleShow` | 90-95 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
