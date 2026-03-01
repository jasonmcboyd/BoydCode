# Screen: Streaming Response (Prescriptive)

## Overview

The streaming response covers the visual lifecycle of the LLM's reply to a user
message, from the initial thinking indicator through token-by-token streaming to
the final token usage display. This is the core feedback loop -- the user sends a
message and watches the assistant's response materialize in real time.

All rendering happens within the Content region of the chat loop Layout, updated
via the Live display context. The Indicator bar shows agent state. No raw ANSI
escape sequences are used.

This spec is PRESCRIPTIVE -- it describes what the screen SHOULD look like.

---

## Lifecycle

A complete response lifecycle follows these phases:

```
1. Thinking    -> Indicator: "@ Thinking..." (yellow)
                  Content: unchanged (previous conversation tail)

2. First token -> Indicator: "@ Streaming..." (cyan)
                  Content: streaming text begins

3. Streaming   -> Indicator: "@ Streaming..." (cyan)
                  Content: text grows token by token

4. Complete    -> Indicator: idle rule
                  Content: final text + token usage line

5. Tool call   -> (if applicable) transitions to execution-window.md
```

---

## Layout (120 columns) -- Phase 1: Thinking

The thinking state appears immediately after the user's message is sent.
The Content region still shows the previous conversation. The Indicator bar
changes to show the thinking state.

```
  > Can you add error handling to the auth module?




@ Thinking...
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Rendering

The Indicator bar update is the ONLY visual change. The Content region does not
show a "Thinking..." text in the conversation area. The user message has already
been added to the conversation view.

```csharp
layout["Indicator"].Update(new Markup("[yellow]@ Thinking...[/]"));
ctx.Refresh();
```

### Duration

Thinking typically lasts 500ms to 5 seconds depending on the provider and model.
For very long thinking (> 10 seconds), the indicator remains as `@ Thinking...`
-- no elapsed time counter is shown (unlike execution, where elapsed time is
meaningful for the user).

### Accessibility

In accessible mode: `[Thinking...]` as static text. No animation.

---

## Layout (120 columns) -- Phase 2: First Token

When the first `TextChunk` arrives from the stream:

1. The Indicator bar transitions from Thinking to Streaming.
2. The Content region adds the beginning of the assistant's response.

```
  > Can you add error handling to the auth module?

  I

@ Streaming...
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

The transition from Thinking to Streaming is instantaneous (same render cycle
as the first token).

---

## Layout (120 columns) -- Phase 3: Streaming

Tokens accumulate in the Content region. The assistant's text grows left-to-right
with word wrapping at the terminal width.

### Early in the response

```
  > Can you add error handling to the auth module?

  I've examined the auth module and found several methods that lack proper error handling. Let me

@ Streaming...
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Later in the response (text has wrapped)

```
  > Can you add error handling to the auth module?

  I've examined the auth module and found several methods that lack proper error handling. Let me
  walk through each one:

  1. `AuthenticateAsync` - Currently throws raw exceptions. I'll wrap this in a try-catch that
     converts to AuthenticationException.
  2. `ValidateTokenAsync` - No error handling at all. If the token is malformed, it crashes.
  3. `RefreshCredentialsAsync` - Has a catch block but swallows the exception silently.

  Let me fix all three. I'll start by reading the current implementation|

@ Streaming...
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Rendering Strategy

The streaming text is part of the conversation view rendered in the Content
region. On each token:

1. Append the token to a `StringBuilder` in the streaming message.
2. Build the conversation view with the streaming message as the last entry.
3. `layout["Content"].Update(conversationView)`.
4. `ctx.Refresh()`.

The refresh rate is capped at ~60fps (16ms minimum interval). If tokens arrive
faster than 60fps, they batch into a single refresh.

```csharp
_streamBuffer.Append(token);
if (_timeSinceLastRefresh.ElapsedMilliseconds >= 16)
{
    layout["Content"].Update(BuildConversationWithStream());
    ctx.Refresh();
    _timeSinceLastRefresh.Restart();
}
```

### Text Formatting

During streaming, text is rendered as plain escaped text with 2-space indent.
No markdown formatting is applied during streaming. The text wraps naturally
at the terminal width via Spectre.Console's Markup word wrapping.

**Future enhancement**: Markdown rendering could be applied to finalized blocks
(paragraphs, code fences that have been closed). This is not in the initial v2
scope but the architecture supports it -- see `docs/terminal-ux-knowledge-base.md`
Section 12 on streaming markdown.

---

## Layout (80 columns) -- Phase 3: Streaming

Same structure at 80 columns. Text wraps earlier:

```
  > Can you add error handling?

  I've examined the auth module and found several
  methods that lack proper error handling. Let me
  walk through each one:

  1. `AuthenticateAsync` - Currently throws raw
     exceptions. I'll wrap this in a try-catch
     that converts to AuthenticationException.
  2. `ValidateTokenAsync` - No error handling at
     all. If the token is malformed, it crashes|

@ Streaming...
> _
Gemini | gemini-2.5-pro | my-project    /help
```

---

## Layout (120 columns) -- Phase 4: Streaming Complete

When all tokens have been received (CompletionChunk processed):

1. The Indicator bar returns to idle (dim rule).
2. The Content region shows the complete response with token usage below.
3. A blank line separates the response text from the token usage.

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

----------------------------------------------------------------------------------------------------------------------------
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Token Usage Display

After streaming completes, the token usage line appears:

```
  4,521 in / 892 out / 5,413 total
```

All dim text. Numbers formatted with thousand separators using the current
culture. This is cumulative for the turn (all rounds in a multi-round agentic
turn show separate usage lines).

---

## Layout (120 columns) -- Non-Streaming Response

When the provider does not support streaming (`SupportsStreaming == false`):

1. Indicator bar shows `@ Thinking...` for the entire request duration.
2. When the response returns, Indicator bar goes to idle.
3. The full response renders in the Content region as an Assistant Message Block.
4. Token usage follows.

The visual result is identical to the post-streaming state. The only difference
is the user experience: they see "Thinking..." for longer, then the full text
appears at once instead of gradually.

---

## Layout (120 columns) -- Multi-Round Agentic Turn

When the response contains tool calls, the lifecycle extends:

```
  > Can you add error handling to the auth module?

  I'll start by reading the current implementation.

  4,521 in / 245 out / 4,766 total

  +- Shell ---------------------------------------------------------------------------------------------------+
  | Get-Content -Path src/Auth/AuthService.cs                                                                  |
  +------------------------------------------------------------------------------------------------------------+
  \u2713 Shell  42 lines | 0.3s
  /expand to show full output

  I can see the implementation. Here are the three methods that need error handling.
  Let me apply the changes now.

  8,932 in / 1,245 out / 10,177 total

  +- Shell ---------------------------------------------------------------------------------------------------+
  | Set-Content -Path src/Auth/AuthService.cs -Value $updatedContent                                           |
  +------------------------------------------------------------------------------------------------------------+
  \u2713 Shell  0 lines | 0.1s

  Done. I've added try-catch blocks to all three methods. Each now catches specific
  exception types and wraps them in AuthenticationException with context about which
  operation failed.

  12,450 in / 1,890 out / 14,340 total

----------------------------------------------------------------------------------------------------------------------------
> _
```

### Observations

1. Token usage appears after EACH round (each LLM response), showing cumulative
   totals for the session.
2. Tool call badges and results appear inline within the assistant's response.
3. The assistant's text segments and tool calls flow continuously -- there is no
   turn separator between rounds of the same agentic turn.
4. A turn separator (blank line) only appears between the final assistant
   response of one turn and the next user message.

---

## Layout (120 columns) -- Error During Streaming

If an exception occurs during streaming (network failure, API error):

```
  > Can you add error handling to the auth module?

  I've examined the auth module and found several methods that

Error: Request failed: Connection timed out (api.anthropic.com)
  Suggestion: Check your network connection or try again.

----------------------------------------------------------------------------------------------------------------------------
> _
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Behavior

1. Thinking/streaming indicator stops.
2. If partial text was streamed, it remains visible (the text up to the error
   point is preserved in the conversation view).
3. The error message renders below the partial text using the Error Display
   pattern (07-component-patterns.md #22).
4. The Indicator bar returns to idle.
5. The user's message is removed from the conversation to allow retry.
6. The session loop continues -- the user can try again.

---

## Layout (120 columns) -- Context Compaction Warning

When the context window is near capacity, a warning renders before the request:

```
  ! Warning: Context compacted: 8 message(s) removed to free 45,000 tokens.

  > Can you also update the tests?

@ Thinking...
> _
```

---

## States

| State | Indicator Bar | Content Region |
|-------|---------------|----------------|
| Thinking | `@ Thinking...` (yellow) | Previous conversation (no change) |
| Streaming (first token) | `@ Streaming...` (cyan) | First token appears |
| Streaming (ongoing) | `@ Streaming...` (cyan) | Text grows token by token |
| Streaming complete | Idle (dim rule) | Complete text + token usage |
| Tool call follows | Transitions to execution states | Text + tool badge |
| Error during streaming | Idle (dim rule) | Partial text + error message |
| Non-streaming response | `@ Thinking...` then idle | Full text appears at once |
| Context compaction | Idle (before request) | Warning message in content |

---

## Performance

### Rendering Budget

At 60fps, each render cycle has ~16ms. The budget:

| Operation | Target Time |
|-----------|-------------|
| Append token to StringBuilder | < 0.1ms |
| Build conversation view | < 5ms |
| Layout.Update + ctx.Refresh | < 10ms |
| Total per frame | < 16ms |

For conversations with 50+ turns, only the visible tail is rendered (the most
recent messages that fit in the Content region height). Older turns are not
included in the renderable -- they exist only in the data model.

### Caching Strategy

- Finalized message renderables (completed turns) are cached. They are only
  rebuilt when the terminal width changes.
- The streaming message is rebuilt on every frame (it changes each frame).
- Token usage lines are simple Markup strings -- no caching needed.
- Tool call badges and result badges are cached after construction.

---

## Accessibility

### Screen Reader Behavior

- Thinking: Screen reader announces "Thinking" (static text, no animation).
- Streaming: Tokens are buffered and announced in chunks (not character by
  character). The announcement interval matches the render rate cap.
- Complete: The full response text is available for sequential reading.
- Token usage: Read as "4521 in, 892 out, 5413 total" (plain text).

### NO_COLOR

All streaming text is plain (no color markup). Token usage line loses its dim
styling but remains readable. Tool badges lose their border colors but retain
their structure.

### Non-Interactive

When piped or non-interactive:
- No Layout, no Live display.
- Thinking writes "Thinking..." to stderr.
- Streaming tokens write directly to stdout via `Console.Write`.
- Token usage writes to stdout.
- The output is clean text suitable for piping to another tool.

---

## Component Patterns Used

| Pattern | Reference | Usage |
|---------|-----------|-------|
| Streaming Text | 07-component-patterns.md #18 | Token accumulation |
| Thinking Indicator | 07-component-patterns.md #19 | Pre-response state |
| Token Usage Display | 07-component-patterns.md #17 | Post-response metrics |
| Tool Call Badge | 07-component-patterns.md #4 | Tool invocation preview |
| Tool Result Badge | 07-component-patterns.md #5 | Tool execution result |
| Error Display | 07-component-patterns.md #22 | Provider errors |
| Assistant Message Block | 07-component-patterns.md #2 | Non-streaming response |
| Indicator Bar | 07-component-patterns.md #26 | State feedback |
