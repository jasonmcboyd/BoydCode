# Flow: First Run

## Overview

The complete journey from the very first invocation of `boydcode` -- when no
provider has been configured -- through reaching a working chat session with a
first AI response. This flow covers the "time to value" gap: the user has
installed the application but has never run it before.

## Preconditions

- BoydCode is installed and available on the user's PATH.
- No provider configuration exists at `~/.boydcode/providers/`.
- No API key is passed via `--api-key` flag or environment variable.
- The terminal is interactive (not piped or redirected).

## Flow Diagram

```
    [User runs `boydcode`]
         |
         v
    STARTUP-01/02: Banner rendered
    STARTUP-03: Info grid (Provider: Gemini, Model: gemini-2.5-pro, no key)
         |
         v
    STARTUP-05: "Not configured" footer
    "Use /provider setup to configure an API key, or pass --api-key."
         |
         v
    LAYOUT-01: Split-pane layout activated
    LAYOUT-02: Empty input prompt "> "
         |
         v
    [User types message without configuring]
         |
    +----+----+
    |         |
    v         v
  [Chat      [Slash
  message]   command]
    |         |
    v         |
  CHAT-08:   |
  "No LLM    |
  provider   |
  configured"|
    |         |
    v         v
  (back to   [User types /provider setup]
  input)      |
              v
         PROV-03: Provider selection prompt
         "Select a provider:"
         > Anthropic / Gemini / OpenAi / Ollama
              |
              v
         PROV-04: API key prompt
         "API key:" (masked input)
              |
         +----+----+
         |         |
         v         v
       [Valid    [Empty/
        key]     invalid]
         |         |
         v         v
       PROV-05   [Ollama:
       Model     continue
       prompt    without
         |       key]
         v         |
       [Enter    +-+
       for       |
       default]  |
         |       |
         +---+---+
             |
             v
        PROV-06: "Provider 'X' configured and activated."
        Status line updates: "Gemini | gemini-2.5-pro | _default | InProcess"
             |
             v
        LAYOUT-02: Input prompt "> "
             |
             v
        [User types first message]
             |
             v
        CHAT-01: "Thinking..." indicator
             |
             v
        CHAT-02: Streaming tokens appear
             |
             v
        CHAT-03: Streaming complete
        CHAT-05: Token usage line
             |
             v
        LAYOUT-02: Input prompt "> "
        (User has reached a working session)
```

## Steps (Detailed)

### Step 1: Application Launch

- **Screen**: STARTUP-01 or STARTUP-02 (banner), STARTUP-03 (info grid)
- **User sees**: The ASCII art banner (or compact version if terminal height
  < 30), followed by a dim rule separator and the info grid. The grid shows
  the default provider (Gemini), default model (gemini-2.5-pro), project
  (_default), engine (InProcess), and current working directory. No Docker or
  Git rows appear for the ambient project.
- **User action**: None -- this renders automatically.
- **System response**: The application resolves the ambient `_default`
  project, resolves an empty directory list, determines the provider type
  (Gemini by default), and finds no API key from any source (CLI flag,
  stored profile, environment variable, appsettings).
- **Transitions to**: Step 2

### Step 2: Not Configured Footer

- **Screen**: STARTUP-05
- **User sees**: Two lines below the info grid:
  ```
    Not configured
    Use /provider setup to configure an API key, or pass --api-key.
  ```
  "Not configured" appears in yellow bold. The instruction line uses bold for
  `/provider setup` and `--api-key`, dim for the connecting words.
- **User action**: None -- this renders automatically.
- **System response**: The `isConfigured` flag remains false. No provider is
  activated. The start hint (STARTUP-06) is NOT shown because it is gated on
  `isConfigured`.
- **Transitions to**: Step 3

### Step 3: Layout Activation and Input Prompt

- **Screen**: LAYOUT-01 (split-pane), LAYOUT-02 (empty input line)
- **User sees**: The terminal transitions to the split-pane layout with a
  horizontal separator line and an empty `> ` input prompt below it. The
  status line row is present but may be empty or show the default status.
- **User action**: User can now type.
- **System response**: The application creates the execution engine,
  creates a new session, initializes the conversation logger, and
  activates the layout. The input handler starts accepting key input.
- **Transitions to**: Step 4a or Step 4b

### Step 4a: User Sends a Chat Message (Error Path)

- **Screen**: CHAT-08
- **User sees**: After typing a message and pressing Enter:
  ```
  Error: No LLM provider configured. Use /provider setup to configure one.
  ```
  The error appears in the output scroll region. The message is NOT added to
  the conversation (it is removed after the error).
- **User action**: Reads the error, realizes they need to configure a
  provider.
- **System response**: The orchestrator checks whether a provider is
  configured, finds it is not, renders the error, removes the dangling
  user message from the conversation, and returns without calling the LLM.
- **Transitions to**: Step 5

### Step 4b: User Types an Unknown Command (Error Path)

- **Screen**: CHAT-15 or CHAT-16
- **User sees**: If they typed something close to a valid slash command:
  ```
  Error: Unknown command. Did you mean '/provider'?
  ```
  Or if no close match:
  ```
  Error: Unknown command. Type /help for available commands.
  ```
- **User action**: Corrects their command.
- **Transitions to**: Step 5

### Step 5: User Runs `/provider setup`

- **Screen**: PROV-03
- **User sees**: A `SelectionPrompt` listing all available providers:
  ```
  Select a provider:
  > Anthropic
    Gemini
    OpenAi
    Ollama
  ```
  The currently highlighted item uses green text. Navigation is via
  Up/Down arrow keys, selection via Enter.
- **User action**: Navigates to their preferred provider and presses Enter.
- **System response**: The interactive prompt takes focus for provider
  selection. The selected provider type is parsed from the string.
- **Transitions to**: Step 6

### Step 6: API Key Entry

- **Screen**: PROV-04
- **User sees**: A secret text prompt:
  ```
  API key:
  ```
  Characters are masked as they are typed (displayed as `*`).
  For Ollama, the prompt allows empty input (no key required).
- **User action**: Pastes or types their API key and presses Enter.
- **System response**: The key is captured. If empty and not Ollama, the key
  is stored as null (the provider may fail on first LLM call with an auth
  error).
- **Transitions to**: Step 7

### Step 7: Model Selection

- **Screen**: PROV-05
- **User sees**: A text prompt with a default value:
  ```
  Model: gemini-2.5-pro
  ```
  The default model comes from `ProviderDefaults.DefaultModelFor()`. The
  user can press Enter to accept the default or type a different model name.
- **User action**: Presses Enter to accept the default, or types a model
  name.
- **System response**: A `ProviderProfile` is created and saved to
  `~/.boydcode/providers/{type}.json`. The provider is activated by
  activating the provider. The status line is updated.
  The last-used provider is recorded.
- **Transitions to**: Step 8

### Step 8: Provider Configured Confirmation

- **Screen**: PROV-06
- **User sees**:
  ```
  Provider 'Gemini' configured and activated.
  ```
  In green text. The status line at the bottom of the terminal updates to
  show the new provider and model.
- **User action**: None.
- **System response**: The layout resumes after the prompt sequence
  completes. Control returns to the session loop.
- **Transitions to**: Step 9

### Step 9: User Types First Message

- **Screen**: LAYOUT-02 (input line), then CHAT-01
- **User sees**: The input prompt `> ` is ready. The user types their first
  message and presses Enter. The "Thinking..." indicator appears in the
  output area.
- **User action**: Types a message and presses Enter.
- **System response**: The message is added to the conversation via
  The message is added to the conversation. The agent turn begins.
  The provider is now configured, so the turn proceeds. An LLM request is
  built with the system prompt (meta prompt + session prompt), the Shell
  tool definition, and the conversation messages. The thinking indicator
  is displayed.
- **Transitions to**: Step 10

### Step 10: First AI Response

- **Screen**: CHAT-02, then CHAT-03, then CHAT-05
- **User sees**: The "Thinking..." indicator is replaced by streaming tokens
  that appear character by character with 2-space indent. When streaming
  completes, a blank line separates the response from the token usage line:
  ```
  Tokens: 1,234 in / 567 out / 1,801 total
  ```
- **User action**: Reads the response. The user now has a working session.
- **System response**: `StreamResponseAsync` iterates over
  `IAsyncEnumerable<StreamChunk>`, rendering `TextChunk` tokens via
  `RenderStreamingToken`. On completion, `RenderStreamingComplete` adds
  trailing blank lines. `RenderTokenUsage` displays cumulative counts.
  The session is auto-saved.
- **Transitions to**: LAYOUT-02 (input prompt, ready for next message)

## Decision Points

| # | Decision Point | Condition | Outcome |
|---|---|---|---|
| D1 | Banner size | Terminal height >= 30 | STARTUP-01 (full ASCII art) |
|    |              | Terminal height < 30 | STARTUP-02 (compact single-line) |
| D2 | Provider configured? | API key found or Ollama | STARTUP-04 (Ready footer) + STARTUP-06 (start hint) |
|    |                      | No API key | STARTUP-05 (Not configured footer) |
| D3 | User input type | Chat message | CHAT-08 (no provider error) |
|    |                 | `/provider setup` | Provider setup flow |
|    |                 | `/help` | HELP-01 |
|    |                 | Unknown `/cmd` | CHAT-15 or CHAT-16 |
| D4 | Provider selection | User selects from list | Continues to API key prompt |
| D5 | API key empty? | Ollama selected | Continues (key not required) |
|    |                | Other provider, empty | Key stored as null; may fail at LLM call |
| D6 | Model input | User presses Enter | Default model used |
|    |             | User types model name | Custom model stored |
| D7 | First message response | Provider has streaming support | CHAT-02 streaming tokens |
|    |                        | No streaming support | CHAT-04 static text panel |
| D8 | Response has tool calls | `stop_reason == "tool_use"` | Tool execution sub-flow |
|    |                         | `stop_reason == "end_turn"` | Response complete, back to input |

## Error Paths

### E1: Bad API Key (Deferred Error)

The `/provider setup` flow does not validate the API key at configuration
time. The error surfaces on the first LLM request:

1. User configures provider with an invalid key (Step 6-8 succeed normally).
2. User types a message (Step 9).
3. The agent turn sends the request to the provider.
4. The provider returns a 401/403 error.
5. **Screen**: CHAT-09 -- Red bold "Error:" + extracted message + yellow
   "Suggestion: Check your API key with /provider setup or pass --api-key."
6. The user message is removed from the conversation to keep state
   consistent.
7. User returns to input prompt and can run `/provider setup` again.

### E2: Network Issues

1. User configures provider (Steps 5-8 succeed).
2. User types a message (Step 9).
3. The LLM request fails with a connection/timeout error.
4. **Screen**: CHAT-12 -- Red error + "Suggestion: Check your internet
   connection and try again."
5. The user message is removed from the conversation.
6. User returns to input prompt.

### E3: Unknown Provider via `--provider` Flag

If the user launched with `boydcode --provider foobar`:

1. **Screen**: STARTUP-09 -- Red error listing valid options.
2. Provider defaults to Gemini.
3. Startup continues normally (still "Not configured" if no key for Gemini).

### E4: Provider Init Failure

If `ActiveProvider.Activate` throws (e.g., invalid configuration):

1. **Screen**: STARTUP-11 -- Red error: "Failed to initialize provider:
   {message}"
2. `isConfigured` remains false.
3. The "Not configured" footer appears (STARTUP-05).
4. User can still use `/provider setup` to reconfigure.

### E5: Non-Interactive Terminal

If the user runs `boydcode` in a non-interactive terminal (piped input):

1. Layout activation is skipped (`ActivateLayout` checks `IsInteractive`).
2. Fallback input prompt (LAYOUT-07) is used.
3. `/provider setup` will fail with PROV-07: "/provider setup requires an
   interactive terminal. Use --api-key instead."
4. The user must use `boydcode --api-key <KEY>` instead.

## Screen Sequence

Happy path from launch to first response:

1. STARTUP-01 or STARTUP-02 -- Banner
2. STARTUP-03 -- Info grid
3. STARTUP-05 -- Not configured footer
4. LAYOUT-01 -- Split-pane layout activated
5. LAYOUT-02 -- Empty input prompt
6. _(User types `/provider setup`)_
7. PROV-03 -- Provider selection prompt
8. PROV-04 -- API key prompt
9. PROV-05 -- Model prompt
10. PROV-06 -- Provider configured confirmation
11. LAYOUT-02 -- Input prompt (provider now active)
12. _(User types first message)_
13. CHAT-01 -- Thinking indicator
14. CHAT-02 -- Streaming tokens
15. CHAT-03 -- Streaming complete
16. CHAT-05 -- Token usage
17. LAYOUT-02 -- Input prompt (session is working)

## Alternative Entry: `--api-key` Flag

Users who already have an API key can skip the interactive setup entirely:

```
boydcode --api-key sk-abc123
```

1. STARTUP-01/02 -- Banner
2. STARTUP-03 -- Info grid (Provider: Gemini, Model: gemini-2.5-pro)
3. STARTUP-04 -- "Ready" footer (green)
4. STARTUP-06 -- Start hint: "Type a message to start, or /help for
   available commands."
5. LAYOUT-01 -- Split-pane layout activated
6. LAYOUT-02 -- Input prompt (immediately ready for messages)

The `--api-key` flag takes priority over stored profiles and environment
variables. Combined with `--provider` and `--model`, a user can go from
install to first response in a single command:

```
boydcode --provider anthropic --model claude-sonnet-4-20250514 --api-key sk-abc123
```
