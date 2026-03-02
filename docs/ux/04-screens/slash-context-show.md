# Screen: /context show

## Overview

The context show screen is the most visually complex screen in the application.
It renders a full-width stacked bar chart showing token utilization across five
categories, a legend with aligned columns, and three tree-style breakdowns
(system prompt, messages, tools). The entire display is computed from in-memory
token estimates and rendered inside a modeless Terminal.Gui `Window` overlay
titled "Context Usage" using the native drawing API.

This screen answers the question: "How much of my context window am I using,
and where is it going?"

**Screen IDs**: CTX-02, CTX-03

## Trigger

- User types `/context show` during an active session.
- Handled by `ContextSlashCommand.HandleShow()`.

## Layout (80 columns)

```
+-- Context Usage ---------------------------------------------------------+
|                                                                           |
|  Gemini . gemini-2.5-pro . 12.4k/1.0M tokens (1.2%)                      |
|                                                                           |
|  ████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  |
|   ^                                                                       |
|  Estimated usage by category                                              |
|    ■ System prompt        2.1k tokens  (0.2%)                             |
|    ■ Tools                  458 tokens  (0.0%)                             |
|    ■ Messages             9.8k tokens  (1.0%)                             |
|    ■ Free space         887.6k tokens  (88.8%)                            |
|    ■ Compact buffer     100.0k tokens  (10.0%)                            |
|                                                                           |
|  System prompt . 2,134 tokens                                             |
|  ├── Meta prompt           1,892 tokens                                   |
|  └── Session prompt          242 tokens                                   |
|                                                                           |
|  Messages . 37 messages, 9.8k tokens                                      |
|  ├── User text             8 messages, 1.2k tokens                        |
|  ├── Assistant text        7 messages, 3.4k tokens                        |
|  ├── Tool calls           11 calls, 2.1k tokens                           |
|  └── Tool results         11 results, 3.1k tokens                        |
|                                                                           |
|  Tools . 1 tool, 458 tokens                                               |
|  └── Shell                   458 tokens                                   |
|                                                                           |
|  Left/Right: browse segments  Esc: dismiss                            35% |
|                                                                           |
+---------------------------------------------------------------------------+
```

The `^` cursor beneath the bar chart indicates the currently focused segment.
When the user presses Left/Right arrow keys, the cursor moves between segments
and the corresponding legend row is highlighted (bold text).

## Layout (120 columns)

The layout is identical in structure. The stacked bar is a fixed 72 characters
wide regardless of terminal width. The only difference is that tree lines and
legend entries have more trailing whitespace. All content is left-aligned with
a 2-space indent; nothing expands to fill wider terminals.

```
+-- Context Usage -------------------------------------------------------------------------------------+
|                                                                                                       |
|  Gemini . gemini-2.5-pro . 12.4k/1.0M tokens (1.2%)                                                  |
|                                                                                                       |
|  ████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░                              |
|   ^                                                                                                   |
|  Estimated usage by category                                                                          |
|    ■ System prompt        2.1k tokens  (0.2%)                                                         |
|    ■ Tools                  458 tokens  (0.0%)                                                         |
|    ■ Messages             9.8k tokens  (1.0%)                                                         |
|    ■ Free space         887.6k tokens  (88.8%)                                                        |
|    ■ Compact buffer     100.0k tokens  (10.0%)                                                        |
|                                                                                                       |
|  System prompt . 2,134 tokens                                                                         |
|  ├── Meta prompt           1,892 tokens                                                               |
|  └── Session prompt          242 tokens                                                               |
|                                                                                                       |
|  Messages . 37 messages, 9.8k tokens                                                                  |
|  ├── User text             8 messages, 1.2k tokens                                                    |
|  ├── Assistant text        7 messages, 3.4k tokens                                                    |
|  ├── Tool calls           11 calls, 2.1k tokens                                                       |
|  └── Tool results         11 results, 3.1k tokens                                                    |
|                                                                                                       |
|  Tools . 1 tool, 458 tokens                                                                           |
|  └── Shell                   458 tokens                                                               |
|                                                                                                       |
|  Left/Right: browse segments  Esc: dismiss                                                        35% |
|                                                                                                       |
+-------------------------------------------------------------------------------------------------------+
```

### Anatomy

1. **Modal window** -- A modeless Terminal.Gui `Window` titled "Context
   Usage" with `Theme.Modal.BorderScheme` (blue border). Opened via the
   same overlay mechanism as other modals. The agent continues working in
   the background.
2. **Header line** -- 2-space indent, bold provider name, dim middle dot
   separator (`Theme.Semantic.Muted`), bold model name, dim middle dot,
   color-coded token usage fraction and percentage.
3. **Blank line**.
4. **Stacked bar chart** -- 2-space indent, 72-character fixed-width bar
   composed of five colored segments using Unicode block characters. Drawn
   via Terminal.Gui native drawing API (`SetAttribute`, `Move`, `AddStr`)
   with `Theme.Chart.*` tokens for segment colors.
5. **Segment cursor** -- A `^` character drawn one row below the bar at the
   horizontal position of the currently focused segment. Drawn with
   `Theme.Semantic.Accent` (blue). The cursor starts on the first non-zero
   segment.
6. **Blank line**.
7. **Legend heading** -- 2-space indent, bold "Estimated usage by category".
8. **Legend rows** (5 rows) -- 4-space indent, colored square indicator
   (`Theme.Chart.*` colors), left-aligned label (18 chars), right-aligned
   token count (8 chars), " tokens  ", parenthesized percentage. The row
   corresponding to the focused bar segment is drawn with bold text.
9. **Blank line**.
10. **System prompt section** -- 2-space indent, `Theme.Semantic.Accent`
    (blue) bold "System prompt", dim middle dot, token count.
11. **System prompt tree** -- 2-space indent, tree connectors
    (`Theme.Semantic.Muted`), label (20 chars pad), value.
12. **Blank line**.
13. **Messages section** -- 2-space indent, `Theme.Semantic.Success` (green)
    bold "Messages", dim middle dot, message count and token count.
14. **Messages tree** -- 4 tree lines: user text, assistant text, tool calls,
    tool results. Each shows count and token estimate.
15. **Blank line**.
16. **Tools section** -- 2-space indent, `Theme.Chart.Tools` (purple) bold
    "Tools", dim middle dot, tool count and token count.
17. **Tools tree** -- 1 tree line: "Shell" with token count.
18. **Blank line**.
19. **Action hints** -- Bottom of content: "Left/Right: browse segments
    Esc: dismiss" in `Theme.Semantic.Muted`.
20. **Scroll position indicator** -- Percentage format (e.g., `35%`) in the
    bottom-right corner when content exceeds the viewport (pattern #33).

### Stacked Bar Chart Detail

The bar is exactly 72 characters wide, rendered at a 2-space indent. Each
segment is drawn with `SetAttribute` using the segment's `Theme.Chart.*`
attribute, then `AddStr` with the appropriate fill character. It uses five
segments in this order:

| Segment | Color Token | Attribute Token | Character | Meaning |
|---|---|---|---|---|
| System prompt | `Theme.Semantic.Accent` | `Theme.Semantic.Accent` | U+2588 (full block) | Meta prompt + session prompt tokens |
| Tools | `Theme.Chart.Tools` | `Theme.Chart.ToolsAttr` | U+2588 (full block) | Tool definition schema tokens |
| Messages | `Theme.Semantic.Success` | `Theme.Semantic.Success` | U+2588 (full block) | All conversation message tokens |
| Free space | `Theme.Chart.FreeSpace` | `Theme.Chart.FreeSpaceAttr` | U+2591 (light shade) | Available tokens before compact buffer |
| Compact buffer | `Theme.Chart.Buffer` | `Theme.Chart.BufferAttr` | U+2588 (full block) | Reserved headroom for auto-compaction |

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

### Segment Cursor Interaction

The `^` cursor beneath the bar provides keyboard-driven segment browsing:

- **Initial position**: The cursor starts at the horizontal midpoint of the
  first non-zero segment.
- **Left/Right arrows**: Move the cursor to the adjacent segment (skipping
  zero-width segments). The cursor snaps to the midpoint of the new segment.
- **Legend highlight**: When the cursor moves, the corresponding legend row
  is redrawn with bold text. All other legend rows are drawn in normal
  weight.
- **Wrapping**: The cursor does not wrap -- Left at the first segment and
  Right at the last segment are no-ops.

This interaction gives the user a way to identify individual bar segments,
especially when segments are very narrow and their colors are hard to
distinguish.

### Legend Alignment

Each legend row is drawn natively with `SetAttribute` and `AddStr`:

```
    {indicator} {label,-18}{tokenStr,8} tokens  ({percent})
```

- 4-space indent for nesting under the heading.
- Colored square indicator (U+25A0) drawn with the segment's color attribute.
- Label left-padded to 18 characters.
- Token count right-padded to 8 characters (uses `FormatCompact`: e.g.,
  `2.1k`, `887.6k`, `1.0M`, or raw number for < 1000).
- Fixed string " tokens  (".
- Percentage from `FormatPercent` (e.g., `0.2%`, `88.8%`).
- Closing ")".

The legend row for the currently focused bar segment is drawn with bold text
(`TextStyle.Bold`) to visually link the cursor position to the legend.

### Tree Breakdown Format

Each tree line is drawn natively:

```
  {connector} {label,-20}{value}
```

- 2-space indent.
- Dim tree connector (`Theme.Semantic.Muted`): U+251C U+2500 U+2500 for
  non-last children, U+2514 U+2500 U+2500 for last child.
- Label left-padded to 20 characters.
- Value string.

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
| < 50% | green | `Theme.Semantic.Success` (usage-ok) |
| 50-79% | yellow | `Theme.Semantic.Warning` (usage-warn) |
| >= 80% | red | `Theme.Semantic.Error` (usage-critical) |

## States

| State | Condition | Visual Difference |
|---|---|---|
| Normal (low usage) | < 50% context used | Header percentage in green; large grey free space segment in bar |
| Warning (medium usage) | 50-79% context used | Header percentage in yellow; smaller free space segment |
| Critical (high usage) | >= 80% context used | Header percentage in red; minimal or no free space segment |
| Empty conversation | 0 messages | Messages section shows "0 messages, 0 tokens"; tree lines show 0 for all categories; bar shows only system prompt + tools + free space |
| No session | Session is null | Red error: "No active session." (CTX-03) -- rendered as a status message in the conversation view, not in a modal |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:**

- `Theme.Modal.BorderScheme` -- blue border on the modal window
- `Theme.Semantic.Muted` -- dim middle dot separators, tree connectors,
  action hints, scroll indicator
- `Theme.Semantic.Accent` -- blue "System prompt" section heading, system
  prompt bar segment, segment cursor
- `Theme.Semantic.Success` -- green "Messages" section heading, messages bar
  segment, usage-ok header color
- `Theme.Semantic.Warning` -- yellow header percentage at 50-79% usage
- `Theme.Semantic.Error` -- red header percentage at >= 80% usage, error
  message prefix
- `Theme.Chart.Tools` / `Theme.Chart.ToolsAttr` -- purple tools bar segment
  and legend indicator
- `Theme.Chart.FreeSpace` / `Theme.Chart.FreeSpaceAttr` -- grey free space
  bar segment and legend indicator
- `Theme.Chart.Buffer` / `Theme.Chart.BufferAttr` -- orange compact buffer
  bar segment and legend indicator

See `06-style-tokens.md` section 1.6 (Approved Custom Colors) and section
10.11 (Chart Colors) for the complete chart color definitions.

## Interactive Elements

### Segment Cursor

The bar chart supports keyboard-driven segment browsing via Left/Right
arrow keys. The focused segment is indicated by a `^` cursor below the bar
and bold text in the corresponding legend row.

### Keyboard

| Key | Action |
|---|---|
| Left | Move cursor to previous bar segment |
| Right | Move cursor to next bar segment |
| Up/Down | Scroll content when it exceeds viewport |
| Esc | Dismiss the window |

## Behavior

- **Token source**: The context limit comes from `ILlmProvider.Capabilities
  .MaxContextWindowTokens` if available, falling back to `AppSettings
  .ContextWindowTokenLimit`. The compact buffer is `contextLimit *
  CompactionThresholdPercent / 100` subtracted from the context limit.

- **Rendering**: All content is drawn using Terminal.Gui's native drawing
  API (`SetAttribute`, `Move`, `AddStr`) inside a custom `View`'s
  `OnDrawingContent` override. The stacked bar segments are drawn by
  switching `SetAttribute` for each segment color and calling `AddStr` with
  the fill character. The legend, trees, and header use the same approach.
  No Spectre `IRenderable` objects are used.

- **Window type**: The window is modeless -- the agent continues working in
  the background. The window is opened via the same overlay mechanism as
  other modals (pattern #11).

- **Free space calculation**: `max(0, contextLimit - totalUsed - bufferTokens)`.
  If total usage exceeds `contextLimit - buffer`, free space is 0 and the bar
  shows no grey segment.

- **Compact number formatting**: `FormatCompact` renders numbers as:
  - `>= 1,000,000`: `{value / 1M:F1}M` (e.g., `1.0M`)
  - `>= 1,000`: `{value / 1k:F1}k` (e.g., `12.4k`)
  - `< 1,000`: raw integer (e.g., `458`)

## Edge Cases

- **Narrow terminal (< 74 columns)**: The stacked bar is a fixed 72
  characters plus 2-space indent = 74 characters minimum. At terminals
  narrower than 74 columns, the bar wraps to the next line, breaking the
  visual. The legend rows are approximately 55 characters and fit at 60+
  columns. Tree lines are approximately 45 characters and fit comfortably.
  No adaptive layout exists for narrow terminals.

- **Content exceeds viewport**: When the window is shorter than the full
  content (header + bar + legend + 3 tree sections), the view is scrollable
  via Up/Down arrow keys. A scroll position indicator (percentage format)
  appears in the bottom-right corner (pattern #33).

- **Zero context limit**: If `contextLimit <= 0`, the bar is not rendered.
  The header line shows `0/0 tokens (NaN%)` -- this is a degenerate state
  that should not occur in normal usage.

- **All tokens in one category**: The proportional algorithm still assigns
  1 character minimum to other non-zero categories, and adjusts the dominant
  category to compensate. A category with 0 tokens gets 0 characters. The
  segment cursor skips zero-width segments.

- **Very large token counts**: `FormatCompact` handles millions (`1.0M`).
  The right-alignment in the legend accommodates up to 8 characters for the
  token string.

- **No messages**: The message breakdown tree shows all zeros. The bar has
  no green segment. This is the expected state at session start.

- **Non-interactive/piped terminal**: Not applicable. The modal window is
  only shown during an active Terminal.Gui session. In non-TUI mode, the
  context show output could be rendered via direct console writes as a
  fallback, but this is not currently implemented.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Modal Overlay (Detail variant) | #11 | Terminal.Gui Window overlay for the context display |
| Context Usage Bar | #27 | Stacked bar chart with native drawing |
| Status Message | #7 | Error message for no active session |
| Token Usage Display | #17 | Header line token count rendering |
| Scroll Position Indicator | #33 | Percentage indicator when content exceeds viewport |
