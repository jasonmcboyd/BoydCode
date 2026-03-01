# Flow: New Session

## Overview

Starting a new chat session when the provider is already configured. This
flow covers the startup sequence from `boydcode` invocation through reaching
the input prompt, including project resolution, directory resolution with git
detection, provider activation, engine creation, session creation, system
prompt assembly, and layout activation. Also covers the `--resume` variant
for resuming a previous session.

## Preconditions

- BoydCode is installed and available on the user's PATH.
- At least one provider has been configured (API key stored, or Ollama which
  needs no key, or key passed via `--api-key` or environment variable).
- The terminal is interactive (non-interactive fallback is noted where
  applicable).

## Flow Diagram

```
    [User runs `boydcode` with optional flags]
         |
         v
    [Resolve project]
    --project flag? --> Use named project
    CWD matches?    --> Use matched project
    Neither?        --> Use _default (ambient)
         |
         v
    [Resolve directories]
    For each project directory:
      - Check filesystem existence
      - Detect git repo (read .git/HEAD)
      - Extract branch name
         |
    +----+----+
    |         |
    v         v
  [All      [Some dirs
  exist]    missing]
    |         |
    |         v
    |    STARTUP-10: Red error per missing dir
    |         |
    +----+----+
         |
         v
    [Determine provider type]
    --provider flag? --> Parse (with STARTUP-09 on unknown)
    Last-used stored? --> Use that
    Neither? --> Use appsettings default (Gemini)
         |
         v
    [Build LlmProviderConfig]
    Model: --model > stored profile > ProviderDefaults
    API key: --api-key > stored profile > env var > appsettings
         |
         v
    [Activate provider]
         |
    +----+----+
    |         |
    v         v
  [Success] [Failure]
    |         |
    |         v
    |    STARTUP-11: "Failed to initialize provider: ..."
    |    isConfigured = false
    |         |
    +----+----+
         |
         v
    STARTUP-01/02: Banner
    STARTUP-03: Info grid
         |
    +----+----+
    |         |
    v         v
  [Configured]  [Not configured]
    |              |
    v              v
  STARTUP-04     STARTUP-05
  "Ready"        "Not configured"
  STARTUP-06     (No start hint)
  "Type a
  message..."
    |              |
    +----+----+----+
         |
         v
    [Create execution engine]
    ExecutionEngineFactory.CreateAsync(executionConfig, resolvedDirs, projectName)
         |
    +----+----+
    |         |
    v         v
  [InProcess]  [Container]
  Constrained  Docker
  PowerShell   container
    |           |
    +----+-----+
         |
         v
    [Create or resume session?]
         |
    +----+----+
    |         |
    v         v
  [New]     [--resume]
    |         |
    v         v
  Session()  LoadAsync(id)
  new ID       |
    |     +----+----+
    |     |         |
    |     v         v
    |   [Found]   [Not found]
    |     |         |
    |     v         v
    |   STARTUP-07 STARTUP-08
    |   "Resumed   "Session not
    |   session    found."
    |   {id}..."   EXIT (code 1)
    |     |
    +-----+
         |
         v
    [Build system prompt]
    MetaPrompt.Build() + project name + custom prompt + directory context
         |
         v
    [Activate layout]
    [Activate layout]
         |
    +----+----+
    |         |
    v         v
  [Interactive,   [Non-interactive
  height >= 10]   or height < 10]
    |              |
    v              v
  LAYOUT-01      LAYOUT-07
  Split-pane     Fallback prompt
  activated
    |              |
    +----+----+----+
         |
         v
    [Session loop begins]
    AgentOrchestrator.RunSessionAsync(session)
```

## Steps (Detailed)

### Step 1: Project Resolution

- **Screen**: No visual output for this step.
- **User sees**: Nothing yet -- this happens before any rendering.
- **User action**: The user may have passed `--project <NAME>` on the
  command line.
- **System response**: `ProjectResolver.ResolveAsync` runs the resolution
  chain:
  1. If `--project` flag is provided, load that project by name. If not
     found, the resolver creates a transient project with that name.
  2. If no flag, scan all stored projects to see if any have a directory
     matching the current working directory (CWD auto-match).
  3. If no match, use the ambient `_default` project (no directory
     restrictions, default system prompt).
- **Transitions to**: Step 2

### Step 2: Directory Resolution

- **Screen**: STARTUP-10 (only if directories are missing)
- **User sees**: For projects with configured directories, each directory
  is checked for filesystem existence and git metadata. If any directory
  does not exist on disk:
  ```
  Error: Warning: Directory does not exist: C:\path\to\missing
  ```
  One error line per missing directory.
- **User action**: None -- informational output.
- **System response**: `DirectoryResolver.Resolve` processes each
  `ProjectDirectory` entry:
  - Checks `Directory.Exists(path)` for the `Exists` flag.
  - Reads `.git/HEAD` to detect git repositories and extract the current
    branch name.
  - Returns `IReadOnlyList<ResolvedDirectory>` with metadata.
  `DirectoryGuard.ConfigureResolved` stores the resolved directories and
  their access levels for runtime enforcement.
- **Transitions to**: Step 3

### Step 3: Provider Type Determination

- **Screen**: STARTUP-09 (only on unknown provider name)
- **User sees**: If `--provider foobar` was passed:
  ```
  Error: Unknown provider 'foobar'. Valid options: anthropic, gemini, openai,
  ollama. Defaulting to Gemini.
  ```
- **User action**: None.
- **System response**: Provider resolution chain:
  1. `--provider` CLI flag: parsed case-insensitively, with aliases
     (`claude` -> Anthropic, `google` -> Gemini, `gpt` -> OpenAI,
     `local` -> Ollama). Unrecognized names default to Gemini with a
     warning.
  2. Stored last-used provider from the provider config store.
  3. Default provider from application settings (Gemini).
- **Transitions to**: Step 4

### Step 4: Provider Configuration Build

- **Screen**: No visual output.
- **User sees**: Nothing.
- **System response**: `LlmProviderConfig` is assembled:
  - **Model**: `--model` flag > stored profile's `DefaultModel` >
    `ProviderDefaults.DefaultModelFor(providerType)`.
  - **ApiKey**: `--api-key` flag > stored profile's `ApiKey` >
    environment variable (provider-specific: `ANTHROPIC_API_KEY`,
    `GEMINI_API_KEY`/`GOOGLE_API_KEY`, `OPENAI_API_KEY`) >
    appsettings `Llm.ApiKey`.
  - **BaseUrl**, **MaxTokens**: From appsettings.
- **Transitions to**: Step 5

### Step 5: Execution Config Resolution

- **Screen**: No visual output.
- **User sees**: Nothing.
- **System response**: If the project has a `DockerImage` set or
  `RequireContainer` is true, the execution config is built from the
  project's `BuildExecutionConfig()`. Otherwise, the global
  the global execution settings are used. This determines whether the
  engine will be InProcess (constrained PowerShell) or Container (Docker).
- **Transitions to**: Step 6

### Step 6: Provider Activation

- **Screen**: STARTUP-11 (only on failure)
- **User sees**: On success, nothing. On failure:
  ```
  Error: Failed to initialize provider: {message}
  ```
- **User action**: None.
- **System response**: If an API key is available (or provider is Ollama),
  The provider is activated with the assembled configuration. This
  creates the LLM provider instance via the factory. On success:
  - `isConfigured` is set to true.
  - The status line is assembled: `"{Provider} | {Model} | {Project} |
    {Branch} | {Mode}"` (Branch is included only if a git repo was
    detected).
  - The last-used provider is persisted.
  On failure (e.g., `InvalidOperationException`): the error is rendered
  and `isConfigured` remains false.
- **Transitions to**: Step 7

### Step 7: Banner Rendering

- **Screen**: STARTUP-01 or STARTUP-02, STARTUP-03, STARTUP-04 or STARTUP-05
- **User sees**: The full startup display:

  **Full banner** (terminal height >= 30):
  ```
    BOYD (cyan ASCII art)        Users:      1
                                 Revenue:    $0
                                 Valuation:  $0,000,000,000
                                 Commas:     tres
                                 Status:     pre-unicorn
         CODE (blue ASCII art)
    v0.1  Artificial Intelligence, Personal Edition

  ────────────────────────────────────────────────
    Provider  Gemini           Project   myproject
    Model     gemini-2.5-pro   Engine    InProcess
    cwd       C:\Users\jason\source\repos\myproject
    Git       C:\Users\jason\source\repos\myproject (main)

    Ready  Commands run in a constrained PowerShell runspace.
  ```

  **Compact banner** (terminal height < 30):
  ```
    BOYDCODE  v0.1  AI Coding Assistant
  ```
  Followed by the same info grid and footer.

  **If not configured**: STARTUP-05 replaces the "Ready" footer with
  "Not configured" and setup instructions. The start hint (STARTUP-06)
  is suppressed.
- **User action**: None -- reads the startup information.
- **System response**: The application renders the banner, info grid,
  and footer. The start hint is rendered only when the provider is
  configured.
- **Transitions to**: Step 8

### Step 8: Engine Creation

- **Screen**: No visual output (engine creation happens after the banner).
- **User sees**: Nothing additional.
- **System response**: The execution engine factory creates the engine:
  - **InProcess**: Creates a `ConstrainedRunspaceEngine` with JEA
    profiles composed from the global profile and any project-assigned
    profiles.
  - **Container**: Creates a `ContainerExecutionEngine` with Docker
    container setup, volume mounts per directory, and a persistent
    shell session.
  The engine is stored as the active execution engine for the session.
- **Transitions to**: Step 9

### Step 9: Session Creation (New) or Resume

- **Screen**: STARTUP-07 (resume success) or STARTUP-08 (resume not found)
- **User sees**:
  - **New session**: Nothing additional at this step.
  - **Resume success**: Dim italic hint:
    ```
    Resumed session abc123 (42 messages from 2026-02-25 14:30)
    ```
  - **Resume not found**:
    ```
    Error: Session 'abc123' not found.
    ```
    Application exits with code 1.
- **System response**:
  - **New session**: `new Session(workingDirectory)` creates a session with
    a fresh GUID ID and empty conversation. `ProjectName` and
    `SystemPrompt` are set. The conversation logger is initialized.
  - **Resume**: The session repository attempts to load the
    session. If found, the session's working directory and project name
    are updated, the system prompt is rebuilt (to reflect any project
    changes since the last session), and the resume is logged.
- **Transitions to**: Step 10

### Step 10: System Prompt Assembly

- **Screen**: No visual output.
- **User sees**: Nothing.
- **System response**: The system prompt is assembled from
  three parts:
  1. **Project context**: `"You are working on project '{name}'."`
  2. **Custom prompt**: The project's `SystemPrompt` property, or
     `Project.DefaultSystemPrompt` if none is set.
  3. **Directory context** (if directories exist): A "Working Directories"
     section listing each directory with its access level, git status,
     and branch name. Container path mappings are used when in container
     mode.

  Later, when the orchestrator builds the LLM request, it prepends the
  meta prompt (describing the
  shell-only execution model and available commands) to the session's
  system prompt.
- **Transitions to**: Step 11

### Step 11: Layout Activation

- **Screen**: LAYOUT-01 (split-pane) or no change (non-interactive)
- **User sees**:
  - **Interactive, height >= 10**: The screen transitions to the split-pane
    layout. The conversation view is established, a box-drawing separator
    appears, the input prompt `> ` appears, and the status bar appears.
  - **Non-interactive or height < 10**: Layout is skipped. The fallback
    prompt mode will be used (LAYOUT-07).
- **User action**: None.
- **System response**: The layout activation checks whether the terminal
  is interactive and has sufficient height. If conditions are met:
  - The screen is cleared and the layout regions are established.
  - The input handler is created and started.
  - The status bar is updated if one was set during provider activation.
- **Transitions to**: Step 12

### Step 12: Session Loop Begins

- **Screen**: LAYOUT-02 (empty input prompt)
- **User sees**: The input prompt `> ` with a blinking cursor, ready for
  their first message.
- **User action**: Types a message, slash command, or exit command.
- **System response**: The orchestrator enters the
  main session loop. This loop reads user input, dispatches slash commands,
  and runs agent turns until the user types `/quit`, `/exit`, `quit`, or
  `exit`.
- **Transitions to**: Chat turn flow (see `chat-turn.md`) or slash command
  handling.

## Decision Points

| # | Decision Point | Condition | Outcome |
|---|---|---|---|
| D1 | Project resolution | `--project` flag provided | Load named project |
|    |                    | CWD matches a project directory | Use matched project |
|    |                    | Neither | Use `_default` (ambient) |
| D2 | Missing directories | Directory exists on disk | Continue normally |
|    |                     | Directory does not exist | STARTUP-10 error per directory |
| D3 | Provider type | `--provider` flag, recognized | Use specified provider |
|    |               | `--provider` flag, unrecognized | STARTUP-09 warning, default to Gemini |
|    |               | No flag, last-used stored | Use stored last-used |
|    |               | No flag, no stored | Use appsettings default (Gemini) |
| D4 | API key source | `--api-key` flag | Use flag value |
|    |                | Stored profile has key | Use stored key |
|    |                | Environment variable set | Use env var |
|    |                | appsettings has key | Use appsettings key |
|    |                | None of the above | No key; not configured |
| D5 | Provider activation | Key available (or Ollama) | Activate; `isConfigured = true` |
|    |                     | No key (non-Ollama) | Skip; `isConfigured = false` |
|    |                     | Activate throws | STARTUP-11 error; `isConfigured = false` |
| D6 | Banner size | Terminal height >= 30 | STARTUP-01 (full ASCII art) |
|    |             | Terminal height < 30 (or error) | STARTUP-02 (compact) |
| D7 | Execution config | Project has DockerImage or RequireContainer | Project's `BuildExecutionConfig()` |
|    |                  | Neither | Global execution settings from appsettings |
| D8 | Engine mode | Config mode is InProcess | `ConstrainedRunspaceEngine` |
|    |             | Config mode is Container | `ContainerExecutionEngine` |
| D9 | Resume flag | `--resume` not set | New session created |
|    |             | `--resume` set, session found | Session loaded and resumed |
|    |             | `--resume` set, session not found | STARTUP-08 error; exit code 1 |
| D10 | Layout activation | Interactive, height >= 10 | Split-pane layout (LAYOUT-01) |
|     |                   | Non-interactive or height < 10 | Fallback mode (LAYOUT-07) |

## Error Paths

### E1: Missing Directories

Non-fatal. One error line per missing directory appears in the output.
The session continues; commands targeting missing directories will fail
at execution time.

### E2: Unknown Provider Flag

Non-fatal. Warning is rendered (STARTUP-09). Provider defaults to Gemini.
If Gemini has no stored key, the session enters "not configured" state.

### E3: Provider Initialization Failure

Non-fatal. Error is rendered (STARTUP-11). The session starts in "not
configured" state. User can fix via `/provider setup` during the session.

### E4: Session Resume Not Found

Fatal. Error is rendered (STARTUP-08). Application exits with code 1
(`ExitCode.GeneralError`). No session loop is entered.

### E5: Fatal Error During Session

If an unhandled exception occurs in the session loop:
- **Screen**: CHAT-19 -- Red error: "Fatal error: {message}" + Suggestion:
  "The session has ended. Please restart boydcode."
- The conversation logger records a session end with reason "error".
- Application exits with code 1.

### E6: Ctrl+C During Startup

If the user presses Ctrl+C during engine creation or session setup:
- `OperationCanceledException` is caught by the `ChatCommand` try/catch.
- The conversation logger records a session end with reason "cancel".
- Application exits with code 2 (`ExitCode.UserCancelled`).

## Screen Sequence

### New session (happy path):

1. STARTUP-01 or STARTUP-02 -- Banner
2. STARTUP-03 -- Info grid
3. STARTUP-04 -- Ready footer
4. STARTUP-06 -- Start hint
5. LAYOUT-01 -- Split-pane layout activated
6. LAYOUT-05 -- Status line populated
7. LAYOUT-02 -- Empty input prompt

### Resumed session:

1. STARTUP-01 or STARTUP-02 -- Banner
2. STARTUP-03 -- Info grid
3. STARTUP-04 -- Ready footer
4. STARTUP-07 -- Resume hint (session ID, message count, date)
5. LAYOUT-01 -- Split-pane layout activated
6. LAYOUT-05 -- Status line populated
7. LAYOUT-02 -- Empty input prompt

### Not configured (no key):

1. STARTUP-01 or STARTUP-02 -- Banner
2. STARTUP-03 -- Info grid
3. STARTUP-05 -- Not configured footer
4. LAYOUT-01 -- Split-pane layout activated
5. LAYOUT-02 -- Empty input prompt

### Session not found (--resume with invalid ID):

1. STARTUP-01 or STARTUP-02 -- Banner
2. STARTUP-03 -- Info grid
3. STARTUP-04 -- Ready footer (provider may still be configured)
4. STARTUP-08 -- "Session not found" error
5. EXIT (code 1)

## Timing Notes

- **Banner rendering**: Immediate, < 50ms.
- **Project resolution**: Involves filesystem reads for stored projects.
  Typically < 100ms.
- **Directory resolution**: Involves `Directory.Exists` checks and reading
  `.git/HEAD` files. Typically < 50ms per directory.
- **Provider activation**: Creates the LLM client instance. Typically
  < 200ms (no network calls at this point).
- **Engine creation (InProcess)**: Creates PowerShell runspace with JEA
  constraints. Typically 500ms-2s.
- **Engine creation (Container)**: May involve `docker run` to start a
  container. Can take 2-10s depending on whether the image is cached.
- **Total startup time**: 1-3s for InProcess, 3-12s for Container.

There is no progress indicator during startup. The banner renders first,
providing visual feedback, while engine creation happens after the banner.
The layout activation happens last, so the user sees the banner and info
grid as static output in their scrollback before the split-pane layout
takes over.
