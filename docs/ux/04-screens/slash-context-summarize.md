# Screen: /context summarize

## Overview

The context summarize screen triggers LLM-powered conversation summarization.
It sends earlier conversation messages to the active LLM provider with a
specialized summarization system prompt, then replaces those messages with the
summary while preserving the most recent user-assistant exchange. An optional
focus topic narrows the summary's emphasis.

This is the only slash command that makes an LLM API call, so it may take
several seconds to complete and does not provide a progress indicator.

**Screen IDs**: CTX-07, CTX-08, CTX-09, CTX-10, CTX-11

## Trigger

- User types `/context summarize` or `/context summarize <topic>` during an
  active session.
- Handled by `ContextSlashCommand.HandleSummarizeAsync()`.

## Layout (80 columns)

### Success

```
  v Summarized 24 messages into 3. Estimated tokens: 1,200
```

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

## States

| State | Condition | Visual Difference |
|---|---|---|
| Success | Summarization returns non-empty text | Green "v" + summary with original and new message counts, estimated tokens |
| Too few messages | < 4 messages in conversation | Plain text explaining minimum requirement |
| No session | Session is null | Red "Error:" + "No active session." |
| No provider | `ActiveProvider.IsConfigured` is false | Red "Error:" + "No LLM provider configured." |
| Empty summary | LLM returns empty/whitespace | Red "Error:" + "Summarization produced no output." |
| LLM error | API call throws (non-cancellation) | Red "Error:" + "Summarization failed: {message}"; conversation restored to original |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[green]v[/]` | success-green + success indicator (1.1, 3.1) | Success prefix |
| `[red]Error:[/]` | error-red (1.1) | Error prefix for all error states |

## Interactive Elements

None. This is a non-interactive command. The LLM call blocks until complete
or cancelled.

## Behavior

- **Recent exchange preservation**: Before summarization, the method extracts
  the most recent user-assistant exchange (the last 2 messages, if the
  second-to-last is a user text message without tool results). These messages
  are preserved verbatim and appended after the summary.

- **Summarization request**: An `LlmRequest` is constructed with:
  - A dedicated summarization system prompt that instructs the LLM to capture
    key decisions, file paths, pending tasks, and technical context.
  - If a focus topic is provided, it is appended to the system prompt.
  - `Tools` is empty, `ToolChoice` is `None`, `Stream` is `false`.
  - `Messages` contains only the messages to summarize (everything except the
    preserved recent exchange).

- **Message replacement**: On success, the conversation's messages are
  replaced with: a single user message containing the summary text (prefixed
  with "[The following is a summary of the earlier conversation.]"), followed
  by the preserved recent exchange.

- **Rollback on failure**: If the LLM call throws (any exception other than
  `OperationCanceledException`), the conversation's messages are restored to
  the original list captured before the attempt.

- **Logging**: After successful summarization, the event is logged via
  `IConversationLogger.LogContextSummarizeAsync()` with original and new
  message counts.

- **Token display**: The success message shows estimated tokens from
  `conversation.EstimateTokenCount()` formatted with `N0`.

## Edge Cases

- **Focus topic with special characters**: The focus topic is interpolated
  into the system prompt as plain text. Markup-special characters in the
  topic do not affect rendering because the topic is never rendered to the
  console -- only sent to the LLM.

- **Exactly 4 messages**: The minimum 4-message check allows summarization.
  If the last 2 messages qualify as a recent exchange, only 2 messages are
  sent to the LLM for summarization, which may produce a trivial summary.

- **Cancellation**: `OperationCanceledException` propagates without being
  caught by the error handler, allowing the outer cancellation flow to
  handle it. The conversation is not modified (the catch block only runs
  for non-cancellation exceptions).

- **Non-interactive/piped terminal**: Renders normally. No prompts involved.

- **Long-running call**: There is no spinner or progress indicator during
  the LLM call. The user sees no output until the call completes. This is
  a known UX gap -- the command blocks silently.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Status Message | Section 1 | Success and error messages |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| HandleSummarizeAsync | `Commands/ContextSlashCommand.cs` | `HandleSummarizeAsync` | 432-514 |
| Guard: no session | `Commands/ContextSlashCommand.cs` | `HandleSummarizeAsync` | 434-439 |
| Guard: no provider | `Commands/ContextSlashCommand.cs` | `HandleSummarizeAsync` | 441-445 |
| Guard: too few messages | `Commands/ContextSlashCommand.cs` | `HandleSummarizeAsync` | 448-452 |
| Recent exchange extraction | `Commands/ContextSlashCommand.cs` | `ExtractRecentExchange` | 520-537 |
| Summarization system prompt | `Commands/ContextSlashCommand.cs` | `HandleSummarizeAsync` | 459-468 |
| LlmRequest construction | `Commands/ContextSlashCommand.cs` | `HandleSummarizeAsync` | 475-483 |
| Message replacement | `Commands/ContextSlashCommand.cs` | `HandleSummarizeAsync` | 498-504 |
| Rollback on error | `Commands/ContextSlashCommand.cs` | `HandleSummarizeAsync` | 509-513 |
| Logging | `Commands/ContextSlashCommand.cs` | `HandleSummarizeAsync` | 507 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
