# Screen: /context summarize

## Overview

The context summarize screen is an interactive command that generates an
LLM-powered conversation summary, displays a preview panel, and asks the user
whether to Apply, Revise, or Cancel before committing. Optional free-form
instructions after the command narrow the summary's focus. If the terminal is
non-interactive (piped), the first generated summary is applied automatically.

Earlier conversation messages are sent to the active LLM provider with a
specialized summarization system prompt. On Apply, those messages are replaced
with the summary while preserving the most recent user-assistant exchange. The
Revise path re-runs the LLM with additional feedback appended to the system
prompt; each revision replaces the previous feedback rather than accumulating it.

**Screen IDs**: CTX-07, CTX-08, CTX-09, CTX-10, CTX-11

## Trigger

- User types `/context summarize` or `/context summarize <instructions>` during
  an active session.
- Handled by `ContextSlashCommand.HandleSummarizeAsync()`.

## Layout (80 columns)

### Preview State

```
  +-- Summary Preview -----------------------------------------------+
  |                                                                    |
  |  Key decisions:                                                    |
  |  - Adopted Clean Architecture with strict layer separation         |
  |  - Chose Spectre.Console for terminal UI                           |
  |                                                                    |
  |  File paths:                                                       |
  |  - src/BoydCode.Presentation.Console/SpectreHelpers.cs             |
  |                                                                    |
  +--------------------------------------------------------------------+

  22 messages -> 1 summary message (estimated 8,400 -> 950 tokens)

  What would you like to do?
  > Apply
    Revise
    Cancel
```

### After Apply

```
  v Summarized 24 messages into 3. Estimated tokens: 1,200
```

### After Cancel

```
  Cancelled.
```

### After Revise

```
Revision instructions: _
```

The revision instructions prompt accepts free-form text, then re-runs the LLM
and re-renders the preview panel. The loop repeats until Apply or Cancel.

### Too Few Messages

```
Not enough conversation to summarize (need at least 4 messages).
```

### No Active Session

```
Error: No active session.
```

### No Provider

```
Error: No LLM provider configured.
```

### Summarization Failed

```
Error: Summarization failed: Connection timed out.
```

### Summarization Empty

```
Error: Summarization produced no output.
```

### Anatomy

1. **Guards** -- No session, no provider, and too-few-messages checks run
   before any LLM call. Errors are rendered and the command returns.
2. **Indicator bar** -- `TuiLayout.Current?.SetIndicator(IndicatorState.Thinking)`
   is set before the LLM call. `IndicatorState.Idle` is restored in a `finally`
   block after each LLM call completes or fails.
3. **Preview panel** -- `Panel(new Text(summaryText))` with:
   - `.Header("[bold]Summary Preview[/]")`
   - `.Border(BoxBorder.Rounded)`
   - `.BorderColor(Color.Grey)`
   - `.Padding(2, 1)`
   - `.Expand()`
   Uses `Text` (not `Markup`) because LLM output may contain markup-like chars.
4. **Token savings line** -- `[dim]{count} messages -> 1 summary message
   (estimated {before:N0} -> {after:N0} tokens)[/]` at 2-space indent,
   rendered below the panel.
5. **Selection prompt** -- `SpectreHelpers.Select("What would you like to do?",
   ["Apply", "Revise", "Cancel"])`.
6. **Revision prompt** -- `SpectreHelpers.PromptNonEmpty("Revision
   [green]instructions[/]:")`. Only shown when "Revise" is selected.

## States

| State | Condition | Visual |
|---|---|---|
| Preview | Summary generated | Panel + token savings line + selection prompt |
| Apply | User selects Apply | Messages replaced; success message rendered |
| Revise | User selects Revise | Revision instructions prompt; LLM re-runs; loop to Preview |
| Cancel | User selects Cancel | Dim "Cancelled." message |
| Non-interactive | `_ui.IsInteractive` is false | First summary auto-applied without prompting |
| Too few messages | < 4 messages in conversation | Plain text explaining minimum requirement |
| No session | Session is null | Red "Error:" + "No active session." |
| No provider | `ActiveProvider.IsConfigured` is false | Red "Error:" + "No LLM provider configured." |
| Empty summary | LLM returns empty/whitespace | Red "Error:" + "Summarization produced no output." |
| LLM error | API call throws (non-cancellation) | Red "Error:" + "Summarization failed: {message}"; conversation restored |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | Panel header "Summary Preview" |
| `BoxBorder.Rounded` | (border style) | Panel border shape |
| `Color.Grey` | (color) | Panel border color (signals provisional content) |
| `[dim]` | dim (2.2) | Token savings line |
| `[green]` | success-green (1.1) | Selection prompt highlight; revision instructions label |
| `[green]✓[/]` | success-green + success indicator (1.1, 3.1) | Success message prefix |
| `[red]Error:[/]` | error-red (1.1) | Error prefix for all error states |
| `[dim]Cancelled.[/]` | dim (2.2) | Cancel confirmation message |

## Interactive Elements

| Element | Type | Condition |
|---|---|---|
| Action selection | `SpectreHelpers.Select` | Rendered after preview; interactive terminals only |
| Revision instructions | `SpectreHelpers.PromptNonEmpty` | Rendered after "Revise" is selected |

## Behavior

- **Recent exchange preservation**: Before summarization, the method extracts
  the most recent user-assistant exchange (the last 2 messages, if the
  second-to-last is a user text message without tool results). These messages
  are preserved verbatim and appended after the summary on Apply.

- **Summarization request**: An `LlmRequest` is constructed with:
  - A dedicated summarization system prompt that instructs the LLM to capture
    key decisions, file paths, pending tasks, and technical context.
  - `Tools` is empty, `ToolChoice` is `None`, `Stream` is `false`.
  - `Messages` contains only the messages to summarize (everything except the
    preserved recent exchange).

- **Preview loop**: The command runs a `while (true)` loop. On each iteration:
  the system prompt is built, the Thinking indicator is set, the LLM is called,
  the indicator is set to Idle, the preview panel and token savings line are
  rendered, and the selection prompt is shown. The loop exits on Apply or Cancel.

- **Revision feedback**: When the user selects Revise, a free-form instructions
  prompt is shown. The feedback is appended to the summarization system prompt
  as `\n\nRevision feedback: {feedback}`. Each revision replaces the previous
  feedback string -- feedback does not accumulate across multiple Revise cycles.

- **Token estimation**: `EstimateContentBlockTokens` computes a character-based
  approximation: `TextBlock` chars / 4, `ToolUseBlock` (name + args) / 4,
  `ToolResultBlock` content / 4, `ImageBlock` fixed at 250 tokens.

- **Non-interactive fallback**: When `_ui.IsInteractive` is false, the first
  generated summary is applied immediately without showing the selection prompt.

- **Indicator bar**: `TuiLayout.Current?.SetIndicator(IndicatorState.Thinking)`
  is set before each LLM call. `IndicatorState.Idle` is always restored in
  the `finally` block, including on error or cancellation.

- **Message replacement on Apply**: The conversation's messages are replaced
  with a single user message containing the summary text (prefixed with
  "[The following is a summary of the earlier conversation.]"), followed by
  the preserved recent exchange. Handled by `ApplySummary`.

- **Rollback on failure**: If the LLM call throws (any exception other than
  `OperationCanceledException`), the conversation's messages are restored to
  the original list captured before the attempt. The preview loop exits.

- **Logging**: After successful Apply, the event is logged via
  `IConversationLogger.LogContextSummarizeAsync()` with original and new
  message counts.

## Edge Cases

- **Focus instructions with markup characters**: The instructions are
  interpolated into the system prompt as plain text and never rendered to the
  console, so Spectre markup injection is not a concern.

- **Exactly 4 messages**: The minimum 4-message check allows summarization.
  If the last 2 qualify as a recent exchange, only 2 messages are sent for
  summarization, which may produce a trivial summary. The user can Cancel.

- **Multiple Revise cycles**: Each Revise cycle replaces the previous feedback.
  The panel re-renders on each iteration, showing the updated summary.

- **Cancellation**: `OperationCanceledException` propagates without being caught
  by the error handler, allowing the outer cancellation flow to handle it. The
  conversation is not modified.

- **Non-interactive/piped terminal**: Auto-applies the first generated summary.
  No prompts are shown.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | Section 1 | Success and error messages |
| Modal Panel | Section (Panel) | Preview panel wrapping LLM summary text |

