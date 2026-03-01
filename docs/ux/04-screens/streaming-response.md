# Screen: Streaming Response (Prescriptive)

## Overview

The streaming response covers the visual lifecycle of the LLM's reply to a user
message, from the initial thinking indicator through token-by-token streaming to
the final token usage display. This is the core feedback loop -- the user sends a
message and watches the assistant's response materialize in real time.

Streaming happens inside the **active-turn Live context**. The Content region of
the active-turn Layout shows the streaming text. The Activity region shows the
current agent state with an animated braille spinner. When streaming completes
and the turn ends, the Live context deactivates and all content is flushed to
stdout as scrollback.

No raw ANSI escape sequences are used. All rendering through Spectre.Console.

This spec is PRESCRIPTIVE -- it describes what the screen SHOULD look like.

---

## Lifecycle

A complete response lifecycle follows these phases:

```
1. User submits  -> User message echoed to stdout (Panel with grey23 background)
                    Live context activated

2. Thinking      -> Activity: "{spinner} Thinking..." (yellow)
                    Content: empty or prior turn context

3. First token   -> Activity: "{spinner} Streaming..." (cyan)
                    Content: streaming text begins

4. Streaming     -> Activity: "{spinner} Streaming..." (cyan)
                    Content: text grows token by token

5. Complete      -> Live context deactivated
                    Completed content flushed to stdout as scrollback
                    (assistant text + token usage)

6. Tool call     -> (if applicable) transitions to execution-window.md
                    (Live context stays active for next round)
```

### Key Difference from Old Architecture

In the old architecture, all phases rendered inside a persistent Live context
with an always-visible Indicator bar. In the new hybrid architecture:

- The user message is echoed to **stdout** (scrollback) BEFORE the Live context.
- Phases 2-4 happen inside the Live context.
- Phase 5 **exits the Live context** and flushes content to stdout.
- Between turns, there is no Live context -- the terminal shows scrollback.

---

## Phase 1: User Message Echo (Before Live)

When the user submits a message, it is immediately echoed to stdout as
scrollback BEFORE the Live context activates:

```
 > Can you add error handling to the auth module?
```

The user message is a Panel with grey23 background tint:

```csharp
var userPanel = new Panel(new Markup($"> {Markup.Escape(userText)}"))
    .Border(BoxBorder.None)
    .Padding(1, 0, 1, 0)
    .Style(new Style(background: Color.Grey23));
AnsiConsole.Write(userPanel);
AnsiConsole.WriteLine(); // blank line after user message
```

This ensures the user sees their message reflected immediately with no
perceptible lag. The grey23 background provides a subtle visual distinction
from assistant text.

---

## Phase 2: Thinking (120 columns)

The Live context activates. The Activity region shows the thinking state.

```
  (Content region -- empty or showing prior context from this turn)



⠿ Thinking...
──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
Gemini | gemini-2.5-pro | my-project | main | InProcess                             Esc: cancel  /help: commands
```

### Rendering

The Live context is activated immediately after the user message echo:

```csharp
// Live context starts
layout["Activity"].Update(new Markup($"[yellow]{spinnerFrame} Thinking...[/]"));
layout["Separator"].Update(new Rule().RuleStyle("dim"));
layout["StatusBar"].Update(BuildStatusBar());
ctx.Refresh();
// LLM request dispatched
```

The Activity row shows `[yellow]{spinner} Thinking...[/]` with an animated
braille spinner (8-frame, 100ms/frame). The Content region is empty or shows
context from earlier rounds in this turn.

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

The streaming text is part of the Content region, updated via the Live context.
On each token:

1. Append the token to a `StringBuilder` in the streaming message.
2. Build the content view with the streaming message as the current block.
3. `layout["Content"].Update(contentView)`.
4. `ctx.Refresh()`.

The refresh rate is capped at ~60fps (16ms minimum interval). If tokens arrive
faster than 60fps, they batch into a single refresh.

```csharp
_streamBuffer.Append(token);
if (_timeSinceLastRefresh.ElapsedMilliseconds >= 16)
{
    layout["Content"].Update(BuildTurnContentWithStream());
    ctx.Refresh();
    _timeSinceLastRefresh.Restart();
}
```

### Text Formatting

During streaming, text is rendered as a `Markup` renderable with a 2-space
indent prefix. The text is always `Markup.Escape`d before wrapping so LLM
output cannot inject Spectre markup. Word wrapping occurs naturally at the
terminal width via Spectre.Console's measurement system.

```csharp
var streamBlock = new Markup($"  {Markup.Escape(_streamBuffer.ToString())}");
```

No markdown formatting is applied during streaming. The text is plain escaped
text throughout the streaming phase.

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

## Phase 5: Streaming Complete -- Flush to Scrollback

When all tokens have been received (CompletionChunk processed):

1. The Live context deactivates (lambda exits `Live.StartAsync`).
2. The completed turn content is flushed to stdout as scrollback.
3. The inline input prompt `> _` appears.

The user sees in their scrollback:

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

After streaming completes, the token usage line is flushed as part of the
scrollback content:

```
  4,521 in / 892 out / 5,413 total
```

All dim text. Numbers formatted with thousand separators using the current
culture. This is cumulative for the turn.

---

## Non-Streaming Response

When the provider does not support streaming (`SupportsStreaming == false`):

1. Live context activates with `{spinner} Thinking...` in yellow.
2. When the response returns, Live context deactivates.
3. The full response is flushed to stdout as scrollback.
4. Token usage follows.

The visual result is identical to the post-streaming scrollback state. The only
difference is the user experience: they see the animated thinking spinner for
longer, then the full text appears at once in scrollback.

---

## Multi-Round Agentic Turn (120 columns)

When the response contains tool calls, the Live context stays active across
rounds. The lifecycle extends:

```
Round 1: Think -> Stream -> Tool call detected
         (Live context stays active)
         Tool preview panel renders in Content
         Activity: "{spinner} Executing... (Ns)"
         Tool result badge renders in Content
Round 2: Stream again (Activity: "{spinner} Streaming...")
         ...
Round N: Stream -> end_turn
         Live context deactivates
         All content flushed to stdout
```

The user sees in scrollback after the turn completes:

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
5. During the turn, all of this was rendered inside the Live context's Content
   region. After the turn, it is flushed to scrollback.

---

## Error During Streaming (120 columns)

If an exception occurs during streaming (network failure, API error):

1. The Live context deactivates.
2. Partial text (if any) is flushed to scrollback.
3. The error message is rendered to scrollback.

The user sees:

```
 > Can you add error handling to the auth module?

  I've examined the auth module and found several methods that

Error: Request failed: Connection timed out (api.anthropic.com)
  Suggestion: Check your network connection or try again.

> _
```

### Behavior

1. If partial text was streamed, it is preserved in scrollback.
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

  (Live context activates with Thinking spinner)
```

The warning is rendered to stdout before the user message echo.

---

## States

| State | Activity Row | Content Region | Rendering Mode |
|-------|--------------|----------------|----------------|
| User message echo | N/A | N/A (stdout) | Scrollback |
| Thinking | `[yellow]{spinner} Thinking...[/]` | Empty / prior context | Live |
| Streaming (first token) | `[cyan]{spinner} Streaming...[/]` | First token appears | Live |
| Streaming (ongoing) | `[cyan]{spinner} Streaming...[/]` | Text grows token by token | Live |
| Tool call follows | Transitions to execution states | Text + tool badge | Live |
| Turn complete | N/A | Content flushed to stdout | Scrollback |
| Error during streaming | N/A | Partial text + error in stdout | Scrollback |
| Non-streaming response | `[yellow]{spinner} Thinking...[/]` then flush | Full text in stdout | Live then Scrollback |
| Context compaction | N/A | Warning in stdout | Scrollback |

---

## Performance

### Rendering Budget

At 60fps during streaming, each render cycle has ~16ms. The budget:

| Operation | Target Time |
|-----------|-------------|
| Append token to StringBuilder | < 0.1ms |
| Build content view | < 5ms |
| Layout.Update + ctx.Refresh | < 10ms |
| Total per frame | < 16ms |

During the active turn, only the current turn's content is rendered in the
Content region. Previous turns are already in scrollback -- they do not need
to be included in the renderable. This significantly reduces the rendering
cost compared to the old architecture, which had to fit the entire visible
conversation history into a single Content region.

### Caching Strategy

- The streaming message is rebuilt on every frame (it changes each frame).
- Tool call badges and result badges are cached after construction.
- Token usage lines are simple Markup strings -- no caching needed.
- Previous turns are NOT rendered during the active turn -- they are in
  scrollback. No caching needed for historical messages.

---

## Accessibility

### Screen Reader Behavior

- Thinking: Screen reader announces "Thinking" (static text, no animation).
- Streaming: Tokens are buffered and announced in chunks (not character by
  character). The announcement interval matches the render rate cap.
- Complete: When flushed to scrollback, the full response text is available
  for sequential reading.
- Token usage: Read as "4521 in, 892 out, 5413 total" (plain text).

### NO_COLOR

All streaming text is plain (no color markup). Token usage line loses its dim
styling but remains readable. Tool badges lose their border colors but retain
their structure.

### Non-Interactive

When piped or non-interactive:
- No Live context at any point.
- Thinking writes "Thinking..." to stderr.
- Streaming tokens write directly to stdout via `Console.Write`.
- Token usage writes to stdout.
- User messages echo without grey23 background (no panel styling).
- The output is clean text suitable for piping to another tool.

---

## Component Patterns Used

| Pattern | Reference | Usage |
|---------|-----------|-------|
| User Message Block | 07-component-patterns.md #1 | Grey23 background Panel in scrollback |
| Streaming Text | 07-component-patterns.md #18 | Token accumulation in Content region |
| Activity Region | 07-component-patterns.md #26 | Animated spinner + state label |
| Token Usage Display | 07-component-patterns.md #17 | Post-response metrics in scrollback |
| Tool Call Badge | 07-component-patterns.md #4 | Tool invocation preview |
| Tool Result Badge | 07-component-patterns.md #5 | Tool execution result |
| Error Display | 07-component-patterns.md #22 | Provider errors in scrollback |
| Assistant Message Block | 07-component-patterns.md #2 | Non-streaming response in scrollback |
