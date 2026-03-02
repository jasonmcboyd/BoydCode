# Screen: /context summarize

## Overview

The context summarize screen is an interactive command that generates an
LLM-powered conversation summary, displays a preview in a Terminal.Gui
Window with action buttons, and allows the user to Apply, Fork, Revise, or
Cancel before committing. Optional free-form instructions after the command
narrow the summary's focus. If the terminal is non-interactive (piped), the
first generated summary is applied automatically.

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

### Preview Window

```
+-- Summary Preview ----------------------------------------+
|                                                            |
|  Key decisions:                                            |
|  - Adopted Clean Architecture with strict layer            |
|    separation                                              |
|  - Chose Terminal.Gui v2 for TUI framework                 |
|                                                            |
|  File paths:                                               |
|  - src/BoydCode.Presentation.Console/SpectreHelpers.cs     |
|                                                            |
|  Pending tasks:                                            |
|  - Implement streaming token display in conversation       |
|    view                                                    |
|                                                            |
|  --------------------------------------------------------  |
|  22 messages -> 1 summary (estimated 8,400 -> 950 tokens)  |
|                                                            |
|  [ Cancel ]  [ Revise ]  [ Fork ]  [ Apply ]               |
|                                                            |
+------------------------------------------------------------+
```

The summary text is displayed in a scrollable `TextView` (read-only) within
the Window. The token savings line uses `Theme.Semantic.Muted` (dim). A dim
rule separates the summary from the action area.

### Preview Window -- After Revise

When "Revise" is clicked, a revision input area appears at the bottom of the
window, replacing the button bar:

```
+-- Summary Preview ----------------------------------------+
|                                                            |
|  Key decisions:                                            |
|  - Adopted Clean Architecture with strict layer            |
|    separation                                              |
|  ...                                                       |
|                                                            |
|  --------------------------------------------------------  |
|  22 messages -> 1 summary (estimated 8,400 -> 950 tokens)  |
|                                                            |
|  Revision instructions:                                    |
|  [Focus more on the PowerShell execution engine          ] |
|                                                            |
|  [ Cancel ]                                   [ Submit ]   |
|                                                            |
+------------------------------------------------------------+
```

After submitting revision instructions, the activity bar shows "Thinking..."
while the LLM re-generates the summary. The preview window updates with the
new summary and the action buttons reappear.

### After Apply

The window closes and a success message is rendered in the conversation view:

```
  v Summarized 24 messages into 3. Estimated tokens: 1,200
```

### After Fork

The window closes and a success message is rendered in the conversation view:

```
  v Forked conversation. New session: abc12345
  v Summarized 24 messages into 3. Estimated tokens: 1,200
```

### After Cancel

The window closes and a dim message is rendered in the conversation view:

```
  Cancelled.
```

### Too Few Messages

No window is opened. Rendered directly in the conversation view:

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

## Anatomy

1. **Guards** -- No session, no provider, and too-few-messages checks run
   before any LLM call. Errors are rendered and the command returns.
2. **Activity bar** -- `ActivityBarView` is set to `Thinking` state before
   the LLM call. `Idle` is restored in a `finally` block after each LLM call
   completes or fails.
3. **Preview window** -- A Terminal.Gui `Window` (modal) with the title
   "Summary Preview" and a blue border (`Theme.Modal.BorderScheme`). The
   window contains a scrollable read-only `TextView` for the summary text,
   a token savings line, and action buttons at the bottom.
4. **Token savings line** -- `Theme.Semantic.Muted` (dim):
   `{count} messages -> 1 summary (estimated {before:N0} -> {after:N0} tokens)`
   Rendered as a Label below the summary text, above the buttons.
5. **Action buttons** -- Four buttons at the bottom: Cancel, Revise, Fork,
   Apply. Apply is `IsDefault = true`. Cancel is positioned first (leftmost).
6. **Revision input** -- When "Revise" is clicked, the button bar is replaced
   with a Label ("Revision instructions:"), a TextField for instructions,
   and Cancel + Submit buttons. After submitting, the LLM re-generates and
   the preview updates.

## States

| State | Condition | Visual |
|---|---|---|
| Preview | Summary generated | Window with scrollable summary + action buttons |
| Revise input | User clicks Revise | TextField for revision instructions replaces buttons |
| Regenerating | Revision submitted | Activity bar shows Thinking; window awaits update |
| Apply | User clicks Apply | Window closes; messages replaced; success in conversation |
| Fork | User clicks Fork | Window closes; new session created; success in conversation |
| Cancel | User clicks Cancel | Window closes; dim "Cancelled." in conversation |
| Non-interactive | `_ui.IsInteractive` is false | First summary auto-applied without window |
| Too few messages | < 4 messages in conversation | Plain text in conversation, no window |
| No session | Session is null | Red "Error:" + "No active session." |
| No provider | `ActiveProvider.IsConfigured` is false | Red "Error:" + "No LLM provider configured." |
| Empty summary | LLM returns empty/whitespace | Red "Error:" + "Summarization produced no output." |
| LLM error | API call throws (non-cancellation) | Red "Error:" + "Summarization failed: {message}"; conversation restored |

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Modal.BorderScheme` (blue border for the
preview window), `Theme.Semantic.Muted` (dim token savings line, "Cancelled."
message, step separator rule), `Theme.Semantic.Success` with
`Theme.Symbols.Check` (green checkmark success prefix),
`Theme.Semantic.Error` (red "Error:" prefix), `Theme.Semantic.Default`
(white summary text in TextView), `Theme.Input.Text` (white text in
revision TextField).

All interaction occurs within a Terminal.Gui Window. No Terminal.Gui
suspension or Spectre prompts are needed.

## Interactive Elements

| Element | Type | Condition |
|---|---|---|
| Summary text | Read-only TextView in Window | After LLM generates summary |
| Apply button | Button (IsDefault) | Replaces messages with summary |
| Fork button | Button | Creates new session with summary |
| Revise button | Button | Shows revision input area |
| Cancel button | Button | Dismisses window without changes |
| Revision instructions | TextField | After Revise is clicked |
| Submit button | Button | Submits revision and re-generates |

## Keyboard

| Key | Action |
|---|---|
| Enter | Activate focused button (Apply by default) |
| Tab | Move between buttons |
| Shift+Tab | Move backward between buttons |
| Esc | Cancel (close window without changes) |
| Up / Down | Scroll summary text in TextView |
| Page Up / Page Down | Scroll summary text by page |

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

- **Preview loop**: The command manages a loop via the window's button handlers.
  On each iteration: the system prompt is built, the Thinking indicator is set,
  the LLM is called, the indicator is set to Idle, the preview window content
  is updated. The loop exits on Apply, Fork, or Cancel.

- **Revision feedback**: When the user clicks Revise, the button bar is
  replaced with a TextField for instructions. After submitting, the feedback
  is appended to the summarization system prompt as
  `\n\nRevision feedback: {feedback}`. Each revision replaces the previous
  feedback string -- feedback does not accumulate across multiple Revise cycles.
  The LLM is called again and the preview updates.

- **Token estimation**: `EstimateContentBlockTokens` computes a character-based
  approximation: `TextBlock` chars / 4, `ToolUseBlock` (name + args) / 4,
  `ToolResultBlock` content / 4, `ImageBlock` fixed at 250 tokens.

- **Non-interactive fallback**: When `_ui.IsInteractive` is false, the first
  generated summary is applied immediately without showing the preview window.

- **Activity bar**: `ActivityBarView` is set to `Thinking` state before each
  LLM call. `Idle` state is always restored in the `finally` block, including
  on error or cancellation.

- **Message replacement on Apply**: The conversation's messages are replaced
  with a single user message containing the summary text (prefixed with
  "[The following is a summary of the earlier conversation.]"), followed by
  the preserved recent exchange. Handled by `ApplySummary`.

- **Fork on Fork**: Saves the current session, creates a new session seeded
  with the summary, auto-names via LLM (50 char limit, fallback to
  "Fork of {id}"), logs `context_fork` to both sessions, re-initializes
  logger.

- **Rollback on failure**: If the LLM call throws (any exception other than
  `OperationCanceledException`), the conversation's messages are restored to
  the original list captured before the attempt. The preview loop exits.

- **Logging**: After successful Apply, the event is logged via
  `IConversationLogger.LogContextSummarizeAsync()` with original and new
  message counts.

## Edge Cases

- **Focus instructions with markup characters**: The instructions are
  interpolated into the system prompt as plain text and never rendered to the
  console, so markup injection is not a concern.

- **Exactly 4 messages**: The minimum 4-message check allows summarization.
  If the last 2 qualify as a recent exchange, only 2 messages are sent for
  summarization, which may produce a trivial summary. The user can Cancel.

- **Multiple Revise cycles**: Each Revise cycle replaces the previous feedback.
  The preview window content updates in place, showing the updated summary.

- **Cancellation**: `OperationCanceledException` propagates without being caught
  by the error handler, allowing the outer cancellation flow to handle it. The
  conversation is not modified.

- **Non-interactive/piped terminal**: Auto-applies the first generated summary.
  No window is shown.

- **Long summary text**: The `TextView` scrolls vertically. The Window uses
  `Dim.Percent(80)` for width and `Dim.Percent(70)` for height, providing
  adequate space for most summaries.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Modal Overlay | #11 | Preview window with scrollable content |
| Form Dialog | #31 | Revision instructions TextField |
| Status Message | #7 | Success and error messages in conversation view |
