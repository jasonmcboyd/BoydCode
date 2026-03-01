# Flow: Chat Turn

## Overview

A single complete chat turn from user input through AI response. This is the
core interaction loop of the application: the user types a message, the AI
processes it and responds, optionally executing tool calls along the way. A
single "turn" may involve multiple LLM request-response rounds if the AI
decides to use tools.

## Preconditions

- A session is active (`_activeSession` is set).
- The provider is configured (`_activeProvider.IsConfigured` is true).
- The execution engine is initialized (`_activeEngine.IsInitialized` is true).
- The layout is active and the user is at the input prompt (LAYOUT-02).

## Flow Diagram

```
    [User types message at LAYOUT-02/03]
         |
         v
    [Enter pressed]
    Message added to AsyncInputReader history
    Written to Channel<string>
         |
         v
    [Orchestrator reads input]
         |
    +----+----+----+
    |    |         |
    v    v         v
  [Empty] [Slash  [Chat
  string]  cmd]   message]
    |      |       |
    v      v       v
  (skip) (dispatch) [Log user message]
                    [Add to conversation]
                     |
                     v
                [RunAgentTurnAsync]
                [SetAgentBusy(true)]
                     |
                     v
            +========+========+ AGENT TURN LOOP
            |   (max 50 rounds)
            v
      [CompactIfNeededAsync]
      Check token estimate vs threshold
            |
       +----+----+
       |         |
       v         v
     [Under    [Over
     threshold] threshold]
       |         |
       |         v
       |    CHAT-06: Context compaction warning
       |    "Context compacted: N message(s) removed..."
       |         |
       +----+----+
            |
            v
      [Build LlmRequest]
      baseRequest with { Messages = current, Stream = capability }
      [Log LLM request]
            |
            v
      CHAT-01: "Thinking..." indicator
            |
       +----+----+
       |         |
       v         v
     [Streaming] [Non-streaming]
       |           |
       v           v
     [StreamResponse] [SendAsync]
       |           |
       v           v
     [First chunk  [Response
     arrives]      received]
       |           |
       v           v
     CHAT-01       CHAT-01
     cleared       cleared
       |           |
       v           v
     CHAT-02:      CHAT-04:
     Tokens        Static
     stream in     text panel
       |           |
       v           v
     CHAT-03:      |
     Streaming     |
     complete      |
       |           |
       +-----+-----+
             |
             v
       [Update token counters]
       [Log LLM response]
       CHAT-05: Token usage line
             |
             v
       [Add assistant message to conversation]
             |
             v
       [Response has tool calls?]
             |
        +----+----+
        |         |
        v         v
      [No]      [Yes]
        |         |
        v         v
      [SetAgentBusy(false)]
      [AutoSaveSessionAsync]  [ProcessToolCallsAsync]
      [Return to input]       (see tool-execution.md)
        |                       |
        v                       v
      LAYOUT-02              [Tool results added
      (turn complete)        to conversation]
                               |
                               v
                             [Loop back to top
                             of AGENT TURN LOOP
                             for next round]
            |
            +========+========+
```

## Steps (Detailed)

### Step 1: User Input

- **Screen**: LAYOUT-02 (empty input) or LAYOUT-03 (with text)
- **User sees**: The `> ` prompt. As they type, characters appear inline.
  The `AsyncInputReader` supports:
  - Left/Right arrow: cursor movement within the line
  - Home/End: jump to start/end of line
  - Up/Down arrow: command history navigation
  - Backspace/Delete: character deletion
  - Enter: submit the line
- **User action**: Types a message and presses Enter.
- **System response**: The `AsyncInputReader.HandleEnter` method captures
  the line buffer contents, adds the line to command history (avoiding
  consecutive duplicates, capped at 100 entries), writes the line to the
  `Channel<string>`, and resets the buffer and cursor position.
- **Transitions to**: Step 2

### Step 2: Input Dispatch

- **Screen**: No visual change.
- **User sees**: Nothing immediately -- the message has been submitted.
  If the agent was already busy (from a previous turn's tool loop), the
  message enters the queue and LAYOUT-04 updates to show "[N messages
  queued]".
- **System response**: `AgentOrchestrator.RunSessionAsync` reads from
  `_ui.GetUserInputAsync`, which pulls from the `AsyncInputReader` channel.
  The input is classified:
  - Empty/whitespace: skipped, loop back to input.
  - `/quit`, `/exit`, `quit`, `exit`: break out of session loop.
  - Starts with `/`: dispatch as slash command.
  - Otherwise: treat as chat message.
- **Transitions to**: Step 3 (for chat messages)

### Step 3: Message Logging and Conversation Update

- **Screen**: No visual output.
- **User sees**: Nothing.
- **System response**: Two things happen:
  1. `_conversationLogger.LogUserMessageAsync(input)` -- writes a
     `user_message` event to the JSONL log.
  2. `session.Conversation.AddUserMessage(input)` -- adds a `Message`
     with role `User` and a `TextBlock` content to the conversation's
     message list.
- **Transitions to**: Step 4

### Step 4: Agent Turn Start

- **Screen**: No immediate visual output, but the status area updates.
- **User sees**: If using the layout, the input prompt may show agent
  busy state.
- **System response**: `RunAgentTurnAsync` is called. First,
  `_ui.SetAgentBusy(true)` is called, which tells the `TerminalLayout`
  that the agent is processing. This affects how the input area displays
  queued message counts.

  The base `LlmRequest` is constructed once and reused across rounds:
  - **SystemPrompt**: MetaPrompt (execution model description) + session
    system prompt (project context + custom prompt + directory context),
    joined with `\n\n---\n\n`.
  - **Tools**: `[ShellToolDefinition]` (single Shell tool).
  - **ToolChoice**: `Auto` (model decides whether to use tools).
  - **Model**: From the active provider config.
- **Transitions to**: Step 5

### Step 5: Context Compaction Check

- **Screen**: CHAT-06 (only if compaction occurs)
- **User sees**: If compaction is needed:
  ```
  Warning: Context compacted: 12 message(s) removed to fit context window.
  Estimated tokens: 45,000 (target: 50,000).
  ```
- **System response**: `CompactIfNeededAsync` estimates the current
  token count:
  1. `session.Conversation.EstimateTokenCount()` for message tokens.
  2. System prompt tokens estimated as `(metaPrompt.Length +
     sessionPrompt.Length) / 4`.
  3. If the total exceeds `contextLimit * compactionThresholdPercent /
     100` (where `contextLimit` comes from provider capabilities or
     appsettings), the compactor runs.
  4. The compactor evicts older messages to bring the count to
     `contextLimit / 2`.
  5. A warning is rendered showing how many messages were removed and
     the new estimated token count.
- **Transitions to**: Step 6

### Step 6: LLM Request

- **Screen**: CHAT-01
- **User sees**: The "Thinking..." indicator appears:
  - **Layout mode**: Raw text "Thinking..." in the output scroll region
    (overwriting the current line).
  - **Non-layout mode**: Dim italic "[dim italic]Thinking...[/]" via
    Spectre markup.
- **System response**: The per-round request is built as
  `baseRequest with { Messages = currentMessages, Stream = capability }`.
  The request is logged to the conversation logger.
  `_ui.RenderThinkingStart()` displays the indicator.
- **Transitions to**: Step 7a (streaming) or Step 7b (non-streaming)

### Step 7a: Streaming Response

- **Screen**: CHAT-01 -> CHAT-02 -> CHAT-03
- **User sees**:
  1. "Thinking..." is cleared when the first chunk arrives.
  2. Tokens appear character by character with 2-space indent on the
     first token. Text flows naturally, wrapping at terminal width.
  3. When streaming completes, trailing blank lines are added to
     separate the response from subsequent output.
- **System response**: `StreamResponseAsync` iterates over
  `_activeProvider.Provider.StreamAsync(request)`:
  - The `StreamAccumulator` collects all chunks to build the final
    `LlmResponse`.
  - `TextChunk` tokens are rendered via `_ui.RenderStreamingToken`.
  - `ToolCallChunk` tokens are accumulated silently (tool calls are
    processed after streaming completes).
  - The `CompletionChunk` signals the end of the stream.
  - The `finally` block ensures "Thinking..." is cleared and streaming
    is completed even on error/cancellation.
- **Transitions to**: Step 8

### Step 7b: Non-Streaming Response

- **Screen**: CHAT-01 -> CHAT-04
- **User sees**:
  1. "Thinking..." is cleared when the response arrives.
  2. The full response text appears as a borderless panel with 1-char
     left padding.
- **System response**: `_activeProvider.Provider.SendAsync(request)` makes
  a single blocking call. `_ui.RenderThinkingStop()` clears the indicator.
  If the response has text content, `_ui.RenderAssistantText(text)` renders
  it as a `Panel` with `BoxBorder.None`.
- **Transitions to**: Step 8

### Step 8: Token Usage and Response Processing

- **Screen**: CHAT-05
- **User sees**: A dim token usage line:
  ```
  Tokens: 3,456 in / 234 out / 3,690 total
  ```
  Numbers use locale-appropriate formatting (thousands separators).
  These are cumulative counts across all rounds in the session, not just
  this turn.
- **System response**:
  1. `_totalInputTokens` and `_totalOutputTokens` are incremented.
  2. The response is logged to the conversation logger.
  3. `_ui.RenderTokenUsage` displays the cumulative counts.
  4. The assistant's response (all content blocks) is added to the
     conversation via `session.Conversation.AddAssistantMessage`.
- **Transitions to**: Step 9

### Step 9: Tool Use Decision

- **Screen**: No immediate visual change.
- **User sees**: Nothing at this decision point.
- **System response**: `response.HasToolUse` is checked:
  - **No tool calls** (`stop_reason == "end_turn"`):
    - `_ui.SetAgentBusy(false)` releases the busy state.
    - `AutoSaveSessionAsync` persists the session to disk.
    - The method returns, and the session loop waits for the next input.
  - **Has tool calls** (`stop_reason == "tool_use"`):
    - `ProcessToolCallsAsync` is called to execute each tool.
    - After all tool results are added to the conversation, the loop
      continues to the next round (back to Step 5).
- **Transitions to**: LAYOUT-02 (turn complete) or tool execution flow

### Step 10: Auto-Save

- **Screen**: No visual output.
- **User sees**: Nothing.
- **System response**: `AutoSaveSessionAsync` updates
  `session.LastAccessedAt` and calls `_sessionRepository.SaveAsync`.
  If the save fails, the error is logged but not shown to the user
  (best-effort persistence).
- **Transitions to**: LAYOUT-02 (input prompt)

## Multi-Round Tool Use

When the AI response includes tool calls, the turn enters a multi-round
loop. Each round follows this sequence:

```
Round 1:  User message -> LLM request -> Response with tool calls
          -> Execute tools -> Add results to conversation
Round 2:  -> LLM request (same base, updated messages) -> Response
          -> May have more tool calls -> Execute -> Add results
Round N:  -> LLM request -> Response with end_turn
          -> SetAgentBusy(false) -> AutoSave -> Return to input
```

Key behaviors during multi-round turns:

- The base `LlmRequest` (system prompt, tools, tool choice) is built once
  and reused. Only `Messages` changes each round.
- Context compaction is checked at the start of every round, not just the
  first.
- The "Thinking..." indicator appears for each LLM request, including
  subsequent rounds after tool execution.
- Token usage updates are cumulative -- each round adds to the running
  totals.
- The user can queue additional messages while the agent is busy. These
  are buffered in the `AsyncInputReader` channel and shown as
  "[N messages queued]" in LAYOUT-04.
- The maximum is 50 rounds per turn. If reached, CHAT-07 renders:
  ```
  Error: Reached maximum tool call rounds (50). Stopping to prevent
  runaway execution.
  ```
  The session is auto-saved and the turn ends.

## Decision Points

| # | Decision Point | Condition | Outcome |
|---|---|---|---|
| D1 | Input type | Empty/whitespace | Skip, wait for next input |
|    |            | `/quit`, `/exit`, `quit`, `exit` | Break session loop |
|    |            | Starts with `/` | Slash command dispatch |
|    |            | Other text | Chat message |
| D2 | Provider configured? | `_activeProvider.IsConfigured == false` | CHAT-08 error; user message removed |
| D3 | Context compaction | Estimated tokens <= threshold | No compaction |
|    |                    | Estimated tokens > threshold | Compact; CHAT-06 warning |
| D4 | Streaming support | `SupportsStreaming == true` | Stream response (CHAT-02) |
|    |                   | `SupportsStreaming == false` | Blocking send (CHAT-04) |
| D5 | Response has tool calls | `HasToolUse == true` | Process tools, loop back |
|    |                         | `HasToolUse == false` | Turn complete, auto-save |
| D6 | Round count | `round < 50` | Continue loop |
|    |             | `round >= 50` | CHAT-07 error, force stop |

## Error Paths

### E1: Provider Not Configured

- **Screen**: CHAT-08
- **User sees**:
  ```
  Error: No LLM provider configured. Use /provider setup to configure one.
  ```
- **System response**: The user message is removed from the conversation
  (`RemoveLastMessage`) to prevent a dangling user message. The turn ends
  without calling the LLM. Control returns to the input prompt.

### E2: Provider Error (Auth)

- **Screen**: CHAT-09
- **User sees**:
  ```
  Error: Your API key is invalid or expired.
    Suggestion: Check your API key with /provider setup or pass --api-key.
  ```
  The error part appears in red, the suggestion in yellow with dim text.
- **System response**: `FormatProviderError` extracts the human-readable
  message from the provider's exception (parsing embedded JSON error bodies
  if present). `ClassifyAndSuggest` matches keywords like "API KEY",
  "UNAUTHORIZED", "PERMISSION_DENIED" to provide the suggestion. The user
  message is removed from the conversation.

### E3: Provider Error (Rate Limit)

- **Screen**: CHAT-10
- **User sees**: Error + "Suggestion: Wait a moment and retry, or switch
  providers with /provider setup."
- **System response**: Same pattern as E2. Keywords matched: "RATE LIMIT",
  "TOO MANY REQUESTS", "429", "QUOTA".

### E4: Provider Error (Context Window)

- **Screen**: CHAT-11
- **User sees**: Error + "Suggestion: Start a new session or switch to a
  model with a larger context window."
- **System response**: Keywords matched: "CONTEXT", "TOKEN LIMIT",
  "TOO LONG".

### E5: Provider Error (Network)

- **Screen**: CHAT-12
- **User sees**: Error + "Suggestion: Check your internet connection and
  try again."
- **System response**: Matched on `HttpRequestException` or keywords
  "CONNECTION", "TIMEOUT", "NETWORK".

### E6: Provider Error (Server)

- **Screen**: CHAT-13
- **User sees**: Error + "Suggestion: The provider may be experiencing
  issues. Try again in a few moments."
- **System response**: Keywords matched: "500", "SERVER ERROR",
  "OVERLOADED", "503".

### E7: Provider Error (Generic)

- **Screen**: CHAT-14
- **User sees**: The raw error message with no suggestion.
- **System response**: No keyword match. The original error message is
  displayed as-is.

### E8: Maximum Rounds Reached

- **Screen**: CHAT-07
- **User sees**:
  ```
  Error: Reached maximum tool call rounds (50). Stopping to prevent
  runaway execution.
  ```
- **System response**: The session is auto-saved. `SetAgentBusy(false)` is
  called. Control returns to the input prompt. The conversation contains all
  the rounds' messages, so the next user message will have full context.

### E9: Input Error

- **Screen**: CHAT-17
- **User sees**:
  ```
  Error: Input error: {message}
  ```
- **System response**: Exceptions from `_ui.GetUserInputAsync` (other than
  `OperationCanceledException`) are caught, logged, and rendered. The
  session loop continues (waits for next input).

## Screen Sequence

### Simple turn (no tool calls):

1. LAYOUT-02/03 -- User types message
2. CHAT-01 -- Thinking indicator
3. CHAT-02 -- Streaming tokens (or CHAT-04 for non-streaming)
4. CHAT-03 -- Streaming complete
5. CHAT-05 -- Token usage
6. LAYOUT-02 -- Input prompt (turn complete)

### Turn with one round of tool calls:

1. LAYOUT-02/03 -- User types message
2. CHAT-01 -- Thinking indicator
3. CHAT-02/03 -- Streaming response (includes text + tool call decision)
4. CHAT-05 -- Token usage
5. EXEC-01 -- Tool call panel (see tool-execution.md)
6. EXEC-02 -- Execution spinner
7. EXEC-03/04/05 -- Execution output
8. EXEC-06/07/08 -- Tool result
9. CHAT-01 -- Thinking indicator (round 2)
10. CHAT-02/03 -- Streaming response (final text)
11. CHAT-05 -- Token usage (cumulative)
12. LAYOUT-02 -- Input prompt (turn complete)

### Turn with provider error:

1. LAYOUT-02/03 -- User types message
2. CHAT-01 -- Thinking indicator
3. CHAT-09..14 -- Provider error with suggestion
4. LAYOUT-02 -- Input prompt (message removed from conversation)
