# Screen: Streaming Response (Prescriptive)

## Overview

The streaming response covers the visual lifecycle of the LLM's reply to a user
message, from the initial thinking indicator through token-by-token streaming to
the final token usage display. This is the core feedback loop -- the user sends a
message and watches the assistant's response materialize in real time.

Streaming happens inside the **conversation view** of the persistent TUI.
The conversation view shows the streaming text growing token by token. The
activity bar shows the current agent state with an animated braille spinner.
When streaming completes, the completed content blocks remain in the
conversation view's scroll buffer.

All rendering uses Terminal.Gui's native drawing API via `ConversationBlockRenderer`.

This spec is PRESCRIPTIVE -- it describes what the screen SHOULD look like.

---

## Lifecycle

A complete response lifecycle follows these phases:

```
1. User submits  -> User message block added to conversation view
                    (styled block with muted background, Theme.User.*)
                    Activity bar set to Thinking

2. Thinking      -> Activity bar: "{spinner} Thinking..." (Theme.Semantic.Warning, yellow)
                    Conversation view: unchanged

3. First token   -> Activity bar: "{spinner} Streaming..." (Theme.Semantic.Info, cyan)
                    Conversation view: streaming text block begins

4. Streaming     -> Activity bar: "{spinner} Streaming..." (cyan)
                    Conversation view: text grows token by token

5. Complete      -> Activity bar set to Idle (dim rule)
                    Completed assistant text + token usage remain in scroll buffer

6. Tool call     -> (if applicable) transitions to execution-window.md
                    (activity bar transitions to Executing state)
```

---

## Phase 1: User Message in Conversation View

When the user submits a message, it is immediately added to the conversation
view as a `UserMessageBlock`:

```
 > Can you add error handling to the auth module?
```

The user message renders with `Theme.User.Background` (dark grey fill),
`Theme.User.Text` (white on that background), and `Theme.User.Prefix`
(dim `>` prefix). This provides a subtle visual distinction from assistant
text. The block is appended to the scroll buffer and the view refreshes
immediately.

---

## Phase 2: Thinking (120 columns)

The activity bar transitions to the Thinking state.

```
  (Conversation view -- showing conversation history)



⠿ Thinking...
──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Rendering

The activity bar is updated immediately after the user message is appended
to the scroll buffer. The LLM request is then dispatched. The activity bar
shows the animated braille spinner followed by `Thinking...` in
`Theme.Semantic.Warning` (yellow). The spinner uses `Theme.Symbols.SpinnerFrames`
(10-frame, 100ms/frame via `Theme.Layout.SpinnerIntervalMs`). The conversation
view continues showing its current scroll buffer.

### Duration

Thinking typically lasts 500ms to 5 seconds depending on the provider and model.
For very long thinking (> 10 seconds), the spinner continues animating -- no
elapsed time counter is shown (unlike execution, where elapsed time is
meaningful for the user).

### Accessibility

In accessible mode: `[Thinking...]` as static text. No animation.

---

## Phase 3: First Token (120 columns)

When the first `TextChunk` arrives from the stream:

1. The Activity row transitions from Thinking to Streaming.
2. The Content region shows the beginning of the assistant's response.

```
  I

⠿ Streaming...
──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

The transition from Thinking to Streaming is instantaneous (same render cycle
as the first token). The spinner continues animating without interruption.

---

## Phase 4: Streaming (120 columns)

Tokens accumulate in the Content region. The assistant's text grows left-to-right
with word wrapping at the terminal width.

### Early in the response

```
  I've examined the auth module and found several methods that lack proper error handling. Let me

⠿ Streaming...
──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Later in the response (text has wrapped)

```
  I've examined the auth module and found several methods that lack proper error handling. Let me
  walk through each one:

  1. `AuthenticateAsync` - Currently throws raw exceptions. I'll wrap this in a try-catch that
     converts to AuthenticationException.
  2. `ValidateTokenAsync` - No error handling at all. If the token is malformed, it crashes.
  3. `RefreshCredentialsAsync` - Has a catch block but swallows the exception silently.

  Let me fix all three. I'll start by reading the current implementation|

⠿ Streaming...
──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Rendering Strategy

The streaming text is accumulated into an `AssistantTextBlock` in the
conversation view's scroll buffer. On each token:

1. Append the token to the in-progress `AssistantTextBlock`.
2. Signal the conversation view to redraw.
3. The view redraws on the Terminal.Gui main thread via `TguiApp.Invoke()`.

The refresh rate is capped at ~60fps (16ms minimum interval). If tokens arrive
faster than 60fps, they batch into a single redraw.

### Text Formatting

During streaming, text is drawn using Terminal.Gui's native drawing API
(`SetAttribute` / `Move` / `AddStr`) with a 2-space indent. User-provided
content is treated as plain text — it cannot inject terminal control sequences.
Word wrapping is handled by the `ConversationBlockRenderer` at the current
view width.

No markdown formatting is applied during streaming. The text is plain
throughout the streaming phase.

---

## Phase 4: Streaming (80 columns)

Same structure at 80 columns. Text wraps earlier:

```
  I've examined the auth module and found several
  methods that lack proper error handling. Let me
  walk through each one:

  1. `AuthenticateAsync` - Currently throws raw
     exceptions. I'll wrap this in a try-catch
     that converts to AuthenticationException.
  2. `ValidateTokenAsync` - No error handling at
     all. If the token is malformed, it crashes|

⠿ Streaming...
──────────────────────────────────────────────────────────────────────────
Gemini | gemini-2.5-pro | my-project    /help
```

---

## Phase 5: Streaming Complete

When all tokens have been received (CompletionChunk processed):

1. The in-progress `AssistantTextBlock` is finalized in the scroll buffer.
2. The token usage `TokenUsageBlock` is appended to the scroll buffer.
3. The activity bar transitions to Idle (dim horizontal rule).

The conversation view shows:

```
 > Can you add error handling to the auth module?

  I've examined the auth module and found several methods that lack proper error handling. Let me
  walk through each one:

  1. `AuthenticateAsync` - Currently throws raw exceptions. I'll wrap this in a try-catch that
     converts to AuthenticationException.
  2. `ValidateTokenAsync` - No error handling at all. If the token is malformed, it crashes.
  3. `RefreshCredentialsAsync` - Has a catch block but swallows the exception silently.

  Let me fix all three. I'll start by reading the current implementation.

  4,521 in / 892 out / 5,413 total

> _
```

### Token Usage Display

After streaming completes, the token usage block is appended to the
conversation view's scroll buffer:

```
  4,521 in / 892 out / 5,413 total
```

All dim text. Numbers formatted with thousand separators using the current
culture. This is cumulative for the turn.

---

## Non-Streaming Response

When the provider does not support streaming (`SupportsStreaming == false`):

1. Activity bar shows `{spinner} Thinking...` in yellow (`Theme.Semantic.Warning`).
2. When the response returns, the full text is appended to the scroll buffer at once.
3. Token usage block appended.
4. Activity bar transitions to Idle.

The visual result is identical to the post-streaming state. The only difference
is the user experience: they see the animated thinking spinner for longer, then
the full text appears at once in the conversation view.

---

## Multi-Round Agentic Turn (120 columns)

When the response contains tool calls, the activity bar transitions between
states across rounds. The lifecycle extends:

```
Round 1: Think -> Stream -> Tool call detected
         Tool preview block added to scroll buffer
         Activity bar: "{spinner} Executing... (Ns)"
         Tool result block added to scroll buffer
Round 2: Stream again (Activity bar: "{spinner} Streaming...")
         ...
Round N: Stream -> end_turn
         Activity bar returns to Idle (dim rule)
         All content already in scroll buffer
```

The conversation view shows after the turn completes:

```
 > Can you add error handling to the auth module?

  I'll start by reading the current implementation.

  4,521 in / 245 out / 4,766 total

  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs                                                                  |
  +------------------------------------------------------------------------------------------------------------+
  ✓ Shell  42 lines | 0.3s
  /expand to show full output

  I can see the implementation. Here are the three methods that need error handling.
  Let me apply the changes now.

  8,932 in / 1,245 out / 10,177 total

  +- Shell ---------------------------------------------------------------------------------------------------+
  | Set-Content -Path src/Auth/AuthService.cs -Value $updatedContent                                           |
  +------------------------------------------------------------------------------------------------------------+
  ✓ Shell  0 lines | 0.1s

  Done. I've added try-catch blocks to all three methods. Each now catches specific
  exception types and wraps them in AuthenticationException with context about which
  operation failed.

  12,450 in / 1,890 out / 14,340 total

> _
```

### Observations

1. Token usage appears after EACH round (each LLM response), showing cumulative
   totals for the session.
2. Tool call badges and results appear inline within the assistant's response.
3. The assistant's text segments and tool calls flow continuously -- there is no
   turn separator between rounds of the same agentic turn.
4. A turn separator (blank line) only appears between the final content of one
   turn and the next user message.
5. All content is accumulated in the conversation view's scroll buffer as
   `ConversationBlock` records during the turn. No flushing is needed after
   the turn ends -- the blocks are already in the buffer.

---

## Error During Streaming (120 columns)

If an exception occurs during streaming (network failure, API error):

1. The in-progress streaming block is finalized with whatever text arrived.
2. An error block is appended to the conversation view's scroll buffer.
3. The activity bar returns to Idle.

The user sees:

```
 > Can you add error handling to the auth module?

  I've examined the auth module and found several methods that

Error: Request failed: Connection timed out (api.anthropic.com)
  Suggestion: Check your network connection or try again.

> _
```

### Behavior

1. If partial text was streamed, it is preserved in the scroll buffer.
2. The error message renders below the partial text using the Error Display
   pattern (07-component-patterns.md #22).
3. The user's message is removed from the conversation to allow retry.
4. The inline input prompt returns -- the user can try again.

---

## Context Compaction Warning

When the context window is near capacity, a warning renders before the request:

```
  ! Warning: Context compacted: 8 message(s) removed to free 45,000 tokens.

 > Can you also update the tests?

  (Activity bar shows Thinking spinner)
```

The warning is appended to the conversation view's scroll buffer before the
user message block.

---

## States

(Activity bar color references `Theme.Semantic.*`; markup notation indicates visual intent.)

| State | Activity Bar | Conversation View |
|-------|--------------|-------------------|
| User message added | N/A | `UserMessageBlock` appended |
| Thinking | `[yellow]{spinner} Thinking...[/]` (Warning) | Unchanged |
| Streaming (first token) | `[cyan]{spinner} Streaming...[/]` (Info) | First token in `AssistantTextBlock` |
| Streaming (ongoing) | `[cyan]{spinner} Streaming...[/]` (Info) | Text grows token by token |
| Tool call follows | Transitions to Executing state | Text + tool badge block |
| Turn complete | Idle (dim rule) | All turn blocks in scroll buffer |
| Error during streaming | Idle (dim rule) | Partial text + error block |
| Non-streaming response | `[yellow]{spinner} Thinking...[/]` then Idle | Full text appears at once |
| Context compaction | N/A | Warning block appended |

---

## Performance

### Rendering Budget

At 60fps during streaming, each render cycle has ~16ms. The budget:

| Operation | Target Time |
|-----------|-------------|
| Append token to `AssistantTextBlock` | < 0.1ms |
| `ConversationBlockRenderer` draw pass | < 5ms |
| `TguiApp.Invoke` + view refresh | < 10ms |
| Total per frame | < 16ms |

The conversation view renders only the blocks that fit in the current viewport.
Blocks outside the viewport are not drawn. This keeps render cost proportional
to viewport height, not total conversation length.

### Caching Strategy

- The in-progress streaming block is redrawn on every frame (it changes each frame).
- Tool call and result blocks are immutable once appended -- they are not redrawn
  unless they scroll into the viewport.
- Token usage blocks are simple text lines rendered by the drawing API.

---

## Accessibility

### Screen Reader Behavior

- Thinking: Screen reader announces "Thinking" (static text, no animation).
- Streaming: Tokens are buffered and announced in chunks (not character by
  character). The announcement interval matches the render rate cap.
- Complete: When the turn ends, the full response text is in the scroll buffer
  and available for sequential reading.
- Token usage: Read as "4521 in, 892 out, 5413 total" (plain text).

### NO_COLOR

All streaming text is plain (no color markup). Token usage line loses its dim
styling but remains readable. Tool badges lose their border colors but retain
their structure.

### Non-Interactive

When piped or non-interactive:
- No Terminal.Gui application at any point.
- Thinking writes "Thinking..." to stderr.
- Streaming tokens write directly to stdout via `Console.Write`.
- Token usage writes to stdout.
- User messages echo as plain text without background styling.
- The output is clean text suitable for piping to another tool.

---

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.User.Background`, `Theme.User.Text`,
`Theme.User.Prefix`, `Theme.Semantic.Warning` (Thinking), `Theme.Semantic.Info`
(Streaming), `Theme.Semantic.Muted` (token usage, dim rule), `Theme.Symbols.SpinnerFrames`,
`Theme.Layout.SpinnerIntervalMs`

**Component patterns:** User Message Block (#1), Streaming Text (#18),
Activity Region (#26), Token Usage Display (#17), Tool Call Badge (#4),
Tool Result Badge (#5), Error Display (#22), Assistant Message Block (#2)

---

## Component Patterns Used

| Pattern | Reference | Usage |
|---------|-----------|-------|
| User Message Block | 07-component-patterns.md #1 | Muted background block for user messages |
| Streaming Text | 07-component-patterns.md #18 | Token accumulation in conversation view |
| Activity Region | 07-component-patterns.md #26 | Animated spinner + state label |
| Token Usage Display | 07-component-patterns.md #17 | Post-response metrics in conversation view |
| Tool Call Badge | 07-component-patterns.md #4 | Tool invocation preview |
| Tool Result Badge | 07-component-patterns.md #5 | Tool execution result |
| Error Display | 07-component-patterns.md #22 | Provider errors in conversation view |
| Assistant Message Block | 07-component-patterns.md #2 | Non-streaming response in conversation view |
