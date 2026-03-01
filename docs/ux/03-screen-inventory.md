# Screen Inventory

Master catalog of every distinct visual state the user encounters in the BoydCode terminal application.

## How to read this document

Each entry represents a distinct visual state -- something the user sees on screen at a specific moment. A "screen" in a terminal application is not a page or window; it is a rendered output state that may be transient (a spinner), persistent (a table), or interactive (a prompt).

**Columns:**

- **ID** -- Short stable identifier for cross-referencing in specs and flows
- **Area** -- Functional grouping
- **Screen Name** -- Human-readable label
- **Trigger** -- How the user reaches this state
- **Rendering Context** -- Where in the UI this state appears
- **Has Spec** -- Whether a detailed screen spec exists (all No initially)
- **Notes** -- What the user sees

---

## Screen Inventory Table

### Startup

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| STARTUP-01 | Startup | Full Banner | App launch, terminal height >= 30 | Conversation view | No | ASCII art "BOYD CODE" in cyan/blue, dim metadata sidebar (Users, Revenue, Valuation, Commas, Status), version tagline, dim rule separator |
| STARTUP-02 | Startup | Compact Banner | App launch, terminal height < 30 | Conversation view | No | Single line: "BOYDCODE v0.1 AI Coding Assistant" in bold cyan/blue + dim metadata |
| STARTUP-03 | Startup | Info Grid | App launch, after banner | Conversation view | No | Two-column grid showing Provider, Model, Project, Engine, cwd, Docker image (if set), Git repo/branch (per resolved directory) |
| STARTUP-04 | Startup | Ready Footer | App launch, provider configured | Conversation view | No | Green "Ready" + dim engine description (constrained PowerShell or Docker container) |
| STARTUP-05 | Startup | Not Configured Footer | App launch, no API key | Conversation view | No | Yellow bold "Not configured" + instructions to use `/provider setup` or `--api-key` |
| STARTUP-06 | Startup | Start Hint | App launch, provider configured | Conversation view | No | Dim italic hint: "Type a message to start, or /help for available commands." |
| STARTUP-07 | Startup | Session Resumed Hint | App launch with `--resume` | Conversation view | No | Dim italic hint showing session ID, message count, and creation date |
| STARTUP-08 | Startup | Session Not Found Error | `--resume` with invalid ID | Pre-application output | No | Red error: "Session '{id}' not found." Returns exit code 1 |
| STARTUP-09 | Startup | Unknown Provider Warning | `--provider` with unrecognized name | Pre-application output | No | Red error listing valid options, defaults to Gemini |
| STARTUP-10 | Startup | Missing Directory Warning | Project has directories that don't exist on disk | Pre-application output | No | Red error per missing directory path |
| STARTUP-11 | Startup | Provider Init Failure | Provider activation throws | Pre-application output | No | Red error: "Failed to initialize provider: {message}" |

### Terminal Layout

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| LAYOUT-01 | Layout | Application Init | Application activated (interactive, height >= 10) | Full screen | No | Screen divided into conversation view, separator, input area, and status bar |
| LAYOUT-02 | Layout | Input Line - Empty | Waiting for user input | Input area | No | "> " prompt with blinking cursor |
| LAYOUT-03 | Layout | Input Line - With Text | User typing | Input area | No | "> {text}" with cursor at edit position |
| LAYOUT-04 | Layout | Input Line - Queued Messages | Agent busy, user has typed additional messages | Input area | No | "> " prompt with dim "[N messages queued]" right-aligned |
| LAYOUT-05 | Layout | Status Line | Active session | Status bar | No | Dim text: "{Provider} | {Model} | {Project} | {Branch} | {Mode}" at bottom row |
| LAYOUT-06 | Layout | Separator Row | Layout active | Full screen | No | Full-width horizontal line using box-drawing character (U+2500) |
| LAYOUT-07 | Layout | Fallback Input Prompt | Non-interactive or layout disabled | Pre-application output | No | Text prompt with blue bold ">" prefix, optional dim status line and stale settings warning above |

### Chat Loop -- LLM Interaction

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| CHAT-01 | Chat | Thinking Indicator | LLM request sent | Activity bar | No | Dim italic "Thinking..." text |
| CHAT-02 | Chat | Streaming Response | LLM streaming tokens | Conversation view | No | Tokens appended character-by-character in the output area; 2-space indent on first token |
| CHAT-03 | Chat | Streaming Complete | Stream finished | Conversation view | No | Resets output cursor, adds trailing blank lines to separate from next content |
| CHAT-04 | Chat | Assistant Text (Static) | Non-streaming LLM response | Conversation view | No | Panel with no border, 1-char left padding, escaped text content |
| CHAT-05 | Chat | Token Usage Line | After each LLM response | Conversation view | No | Dim text: "Tokens: {in} in / {out} out / {total} total" with locale-formatted numbers |
| CHAT-06 | Chat | Context Compaction Warning | Auto-compaction triggered by token count exceeding threshold | Conversation view | No | Yellow warning: "Context compacted: N message(s) removed..." with estimated and target token counts |
| CHAT-07 | Chat | Max Rounds Error | 50 tool call rounds reached | Conversation view | No | Red error: "Reached maximum tool call rounds (50). Stopping to prevent runaway execution." |
| CHAT-08 | Chat | No Provider Error | User sends message without configured provider | Conversation view | No | Red error: "No LLM provider configured. Use /provider setup to configure one." |
| CHAT-09 | Chat | Provider Error (Auth) | API returns 401/403 | Conversation view | No | Red bold "Error:" + extracted message + yellow suggestion: "Check your API key with /provider setup" |
| CHAT-10 | Chat | Provider Error (Rate Limit) | API returns 429 | Conversation view | No | Red error + suggestion: "Wait a moment and retry, or switch providers" |
| CHAT-11 | Chat | Provider Error (Context) | Token limit exceeded | Conversation view | No | Red error + suggestion: "Start a new session or switch to a model with a larger context window" |
| CHAT-12 | Chat | Provider Error (Network) | Connection/timeout failure | Conversation view | No | Red error + suggestion: "Check your internet connection and try again" |
| CHAT-13 | Chat | Provider Error (Server) | 500/503 from provider | Conversation view | No | Red error + suggestion: "The provider may be experiencing issues" |
| CHAT-14 | Chat | Provider Error (Generic) | Unclassified LLM error | Conversation view | No | Red error with raw message, no suggestion |
| CHAT-15 | Chat | Unknown Slash Command (with suggestion) | User types unrecognized `/foo` that is close to a valid command | Conversation view | No | Red error: "Unknown command. Did you mean '{suggestion}'?" |
| CHAT-16 | Chat | Unknown Slash Command (no suggestion) | User types unrecognized `/foo` with no close match | Conversation view | No | Red error: "Unknown command. Type /help for available commands." |
| CHAT-17 | Chat | Input Error | Exception reading user input | Conversation view | No | Red error: "Input error: {message}" |
| CHAT-18 | Chat | Slash Command Error | Exception during slash command execution | Conversation view | No | Red error: "Command error: {message}" |
| CHAT-19 | Chat | Fatal Error | Unhandled exception in session loop | Conversation view | No | Red error: "Fatal error: {message}" + suggestion to restart boydcode |

### Execution Window

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| EXEC-01 | Execution | Tool Call Panel | LLM emits tool_use block | Conversation view | No | Grey-bordered Panel with dim tool name header; body shows formatted preview (command text) |
| EXEC-02 | Execution | Waiting Spinner | Tool execution starts, no output yet | Activity bar | No | Animated braille spinner (8 frames at 100ms) with "Executing..." and elapsed time counter |
| EXEC-03 | Execution | Streaming Output (Compact) | Output lines arriving, compact display active | Activity bar | No | Single updating line: "Executing... [N lines | elapsed]" |
| EXEC-04 | Execution | Streaming Output (Inline, Filling) | Output lines arriving, <= 5 lines | Conversation view | No | Lines written one at a time with 2-space indent |
| EXEC-05 | Execution | Streaming Output (Inline, Scrolling) | Output lines arriving, > 5 lines | Conversation view | No | 5-line sliding window with in-place rewrite; line counter + elapsed time on first line |
| EXEC-06 | Execution | Tool Result (Success, Collapsed) | Execution complete, > 5 output lines, ANSI mode | Conversation view | No | Green "[Shell]" badge + dim "{N} lines | {elapsed}" + dim italic "/expand to show full output" |
| EXEC-07 | Execution | Tool Result (Success, Short) | Execution complete, 1-5 output lines | Conversation view | No | Green "[Shell]" badge + dim "{N} lines | {elapsed}" (lines remain visible above) |
| EXEC-08 | Execution | Tool Result (Success, No Output) | Execution complete, 0 output lines | Conversation view | No | Green "[Shell]" badge + dim truncated result text (max 200 chars) |
| EXEC-09 | Execution | Tool Result (Error, Collapsed) | Execution error, > 5 output lines, ANSI mode | Conversation view | No | Red "[Shell error]" badge + dim line count + "/expand" hint |
| EXEC-10 | Execution | Tool Result (Error, Short) | Execution error, 1-5 output lines | Conversation view | No | Red "[Shell error]" badge + dim line count |
| EXEC-11 | Execution | Tool Result (Error, No Output) | Execution error, 0 output lines | Conversation view | No | Red "[Shell error]" badge + escaped error text (max 500 chars) |
| EXEC-12 | Execution | Tool Result (Non-ANSI Success) | Execution complete, non-ANSI terminal | Conversation view | No | Green "[Shell]" badge + dim truncated result |
| EXEC-13 | Execution | Tool Result (Non-ANSI Error) | Execution error, non-ANSI terminal | Conversation view | No | Red "[Shell error]" badge + truncated error text |
| EXEC-14 | Execution | Tool Result (Cancelled) | User cancels with Esc/Ctrl+C during execution | Conversation view | No | "[Shell]" badge with "Command cancelled." text |
| EXEC-15 | Execution | Tool Execution Error | Exception during command execution | Conversation view | No | "[Shell error]" badge: "Error executing command: {message}" |
| EXEC-16 | Execution | Unknown Tool Error | LLM calls a tool name other than "Shell" | Conversation view (silent) | No | Tool result added to conversation: "Error: Unknown tool '{name}'. Use the Shell tool." (no visual render) |
| EXEC-17 | Execution | Engine Not Initialized Error | Tool call before engine ready | Conversation view (silent) | No | Tool result: "Error: Execution engine not initialized." (no visual render) |

### Cancellation

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| CANCEL-01 | Cancellation | Cancel Hint | First press of Esc or Ctrl+C during execution | Activity bar | No | Dim italic yellow: "Press Esc or Ctrl+C again to cancel" (auto-clears after 1 second) |
| CANCEL-02 | Cancellation | Cancel Hint Cleared | Timer expires or second press occurs | Activity bar | No | Hint text erased; previous activity state restored |

### Help

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| HELP-01 | Help | Help Table | `/help` | Window | No | Rounded blue-bordered table with Command + Description columns; lists /quit, /exit, then all registered slash commands with their subcommands (subcommands indented + dim) |

### Project Management

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| PROJ-01 | Project | Usage Hint | `/project` with invalid subcommand | Conversation view | No | Yellow "Usage:" + valid subcommands |
| PROJ-02 | Project | Create -- Name Prompt | `/project create` (no name given, interactive) | Dialog | No | TextPrompt: "Project name:" with non-empty validation |
| PROJ-03 | Project | Create -- Already Exists Error | Name matches existing project | Conversation view | No | Red error: "Project {name} already exists." |
| PROJ-04 | Project | Create -- Success | Project saved | Conversation view | No | Green "v" + "Project {name} created." |
| PROJ-05 | Project | Create -- Configure Prompt | After create, interactive | Dialog | No | Confirm prompt: "Configure project settings now?" (default: No) |
| PROJ-06 | Project | Create -- Section Picker | User chooses to configure | Dialog | No | MultiSelectionPrompt: "Directories", "System prompt", "Container settings" |
| PROJ-07 | Project | Create -- Directory Loop | Configuring directories | Dialog | No | Repeating prompt for path (Enter to finish) + access level selection (ReadWrite/ReadOnly) + green "v Added" confirmation per entry |
| PROJ-08 | Project | Create -- System Prompt | Configuring system prompt | Dialog | No | TextPrompt with default value showing `Project.DefaultSystemPrompt`; success message on custom prompt |
| PROJ-09 | Project | Create -- Container Settings | Configuring container | Dialog | No | Docker image prompt (Enter to skip) + require container confirm + success messages; or dim "Skipped" |
| PROJ-10 | Project | Create -- Saved | Configuration complete | Conversation view | No | Green "v" + "Project {name} saved." |
| PROJ-11 | Project | Create -- Tip (No Configure) | User declines to configure | Conversation view | No | Dim tip: "Use /project edit {name} to configure later." |
| PROJ-12 | Project | Create -- Usage (Non-Interactive) | `/project create` without name in non-interactive mode | Conversation view | No | Yellow "Usage:" + "/project create <name>" |
| PROJ-13 | Project | List -- Table | `/project list` with projects | Window | No | SimpleTable: Name, Dirs (right-aligned), Docker, Last used; ambient project shows "(ambient)" suffix; Docker column shows image or dim "--"; required projects show "(required)" |
| PROJ-14 | Project | List -- Empty | `/project list` with no projects | Conversation view | No | "No projects found." + dim "Create one with /project create <name>" |
| PROJ-15 | Project | Show -- Detail View | `/project show [name]` | Window | No | InfoGrid (Project, Created, Last used, Engine, Docker, Container status) + directory table (path, access level, git branch) + meta prompt text + system prompt + JEA profiles + stale settings warning if applicable |
| PROJ-16 | Project | Show -- Minimal Tip | Show for unconfigured project | Window | No | Dim tip: "Use /project edit {name} to configure settings." |
| PROJ-17 | Project | Show -- Not Found Error | Project name doesn't exist | Conversation view | No | Red error: "Project {name} not found." |
| PROJ-18 | Project | Show -- Usage (Non-Interactive) | No name, no active project, non-interactive | Conversation view | No | Yellow "Usage:" + "/project show <name>" |
| PROJ-19 | Project | Edit -- Menu Loop | `/project edit [name]` | Dialog | No | SelectionPrompt with choices: Directories (N configured), System prompt (custom/default), Docker image (name/none), Require container (Yes/No), Done; loops until "Done" |
| PROJ-20 | Project | Edit -- Directories | "Directories" selected in edit menu | Dialog | No | Current directory table (#, Path, Access) + action selection: Add/Remove/Change access level/Back; then prompts and confirmation per action |
| PROJ-21 | Project | Edit -- System Prompt | "System prompt" selected in edit menu | Dialog | No | Shows current prompt (with "(default)" label if default), action selection: Set new / Reset to default / Back |
| PROJ-22 | Project | Edit -- Docker Image | "Docker image" selected in edit menu | Dialog | No | Shows current image or "(not set)", prompt to enter new or Enter to clear |
| PROJ-23 | Project | Edit -- Require Container | "Require container" selected in edit menu | Dialog | No | Shows current value (Yes/No), warning if no Docker image configured, confirm prompt |
| PROJ-24 | Project | Edit -- Saved | After each edit action | Conversation view | No | Green "v" + "Project saved." |
| PROJ-25 | Project | Edit -- Stale Warning | Container/engine settings changed on active project | Conversation view | No | Stale settings warning: "Project settings changed. Run /context refresh to apply." |
| PROJ-26 | Project | Edit -- Not Found Error | Project name doesn't exist | Conversation view | No | Red error: "Project {name} not found." |
| PROJ-27 | Project | Edit -- Non-Interactive Error | `/project edit` in non-interactive mode | Conversation view | No | Red error: "/project edit requires an interactive terminal." |
| PROJ-28 | Project | Delete -- Confirmation | `/project delete [name]` | Dialog | No | Shows what will be deleted (directory mappings, custom prompt, Docker image, container requirement) + confirm prompt (default: No) |
| PROJ-29 | Project | Delete -- Success | Confirmed deletion | Conversation view | No | Green "v" + "Project {name} deleted." |
| PROJ-30 | Project | Delete -- Cancelled | User declines confirmation | Conversation view | No | Dim "Cancelled." |
| PROJ-31 | Project | Delete -- Ambient Error | Attempting to delete _default | Conversation view | No | Red error: "Cannot delete the ambient project _default." |
| PROJ-32 | Project | Delete -- Not Found Error | Project doesn't exist | Conversation view | No | Red error: "Project {name} not found." |
| PROJ-33 | Project | Delete -- Usage (Non-Interactive) | No name, non-interactive | Conversation view | No | Yellow "Usage:" + "/project delete <name>" |

### Provider Management

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| PROV-01 | Provider | Usage Hint | `/provider` with invalid subcommand | Conversation view | No | Yellow "Usage:" + valid subcommands |
| PROV-02 | Provider | List Table | `/provider list` or `/provider` | Window | No | Table with Provider, Status (green bold "active" / dim "ready" / empty), Model, API Key (masked: first 4 chars + "****" or dim "not set") |
| PROV-03 | Provider | Setup -- Provider Selection | `/provider setup` (no name) | Dialog | No | SelectionPrompt listing all provider types |
| PROV-04 | Provider | Setup -- API Key Prompt | Provider selected | Dialog | No | Secret TextPrompt: "API key:" (allow empty for Ollama) |
| PROV-05 | Provider | Setup -- Model Prompt | After API key | Dialog | No | TextPrompt with default value from provider defaults |
| PROV-06 | Provider | Setup -- Success | Provider saved and activated | Conversation view | No | Green: "Provider '{name}' configured and activated." |
| PROV-07 | Provider | Setup -- Non-Interactive Error | Non-interactive terminal | Conversation view | No | Red error: "/provider setup requires an interactive terminal. Use --api-key instead." |
| PROV-08 | Provider | Setup -- Usage (Non-Interactive) | No name, non-interactive | Conversation view | No | Yellow "Usage:" + "/provider setup <name>" |
| PROV-09 | Provider | Show -- Detail Panel | `/provider show` with active provider | Window | No | Rounded-border Panel titled "Active Provider" with Provider, Model, Context window (formatted with thousands separator) |
| PROV-10 | Provider | Show -- No Provider | `/provider show` without active provider | Conversation view | No | Yellow: "No provider is currently active. Use /provider setup to configure one." |
| PROV-11 | Provider | Remove -- Provider Selection | `/provider remove` (no name) | Dialog | No | SelectionPrompt listing all provider types |
| PROV-12 | Provider | Remove -- Success | Provider removed | Conversation view | No | Green: "Provider '{name}' removed." |
| PROV-13 | Provider | Remove -- Active Warning | Removing the currently active provider | Conversation view | No | Yellow warning: "'{name}' is the active provider. It will remain active for this session but won't persist." + green removal confirmation |
| PROV-14 | Provider | Remove -- Usage (Non-Interactive) | No name, non-interactive | Conversation view | No | Yellow "Usage:" + "/provider remove <name>" |

### JEA Profile Management

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| JEA-01 | JEA | Usage Hint | `/jea` with invalid subcommand | Conversation view | No | Yellow "Usage:" + valid subcommands |
| JEA-02 | JEA | List Table | `/jea list` | Window | No | SimpleTable: Name (with "(global)" suffix for _global), Language Mode, Commands (right-aligned), Modules (right-aligned) |
| JEA-03 | JEA | List -- Empty | No profiles exist | Conversation view | No | "No JEA profiles found." + dim "Create one with /jea create <name>" |
| JEA-04 | JEA | Show -- Detail Panel | `/jea show [name]` | Window | No | Rounded-border Panel with profile name header; body shows Language mode, Allowed commands (green "v"), Denied commands (red "x"), Modules, file path (dim) |
| JEA-05 | JEA | Show -- Not Found Error | Profile doesn't exist | Conversation view | No | Red error: "Profile {name} not found." |
| JEA-06 | JEA | Show -- Profile Selection | No name given | Dialog | No | SelectionPrompt listing all profile names |
| JEA-07 | JEA | Show -- No Profiles | No profiles for selection prompt | Conversation view | No | "No JEA profiles found." + dim hint |
| JEA-08 | JEA | Create -- Name Prompt | `/jea create` (no name) | Dialog | No | TextPrompt: "Profile name:" with non-empty validation |
| JEA-09 | JEA | Create -- Name Validation Error | Invalid characters or reserved name | Dialog | No | Red error for empty, reserved name, or invalid characters |
| JEA-10 | JEA | Create -- Already Exists Error | Name matches existing profile | Conversation view | No | Red error: "Profile {name} already exists." |
| JEA-11 | JEA | Create -- Language Mode Selection | After valid name | Dialog | No | SelectionPrompt: FullLanguage, ConstrainedLanguage, RestrictedLanguage, NoLanguage |
| JEA-12 | JEA | Create -- Add Loop | Building profile entries | Dialog | No | Repeating SelectionPrompt: "Add command" / "Add module" / "Done"; command adds name prompt + Allow/Deny selection; module adds name prompt; each shows green "v" confirmation |
| JEA-13 | JEA | Create -- Success | Profile saved | Conversation view | No | Green "v" + "Profile {name} created." + dim file path |
| JEA-14 | JEA | Edit -- Menu Loop | `/jea edit [name]` | Dialog | No | SelectionPrompt with choices: Change language mode, Add command, Remove command, Toggle command deny, Add module, Remove module, Done; loops until "Done" |
| JEA-15 | JEA | Edit -- Change Language Mode | Selected from edit menu | Dialog | No | SelectionPrompt for language mode + green "v" confirmation |
| JEA-16 | JEA | Edit -- Add Command | Selected from edit menu | Dialog | No | Name prompt + Allow/Deny selection + colored confirmation |
| JEA-17 | JEA | Edit -- Remove Command | Selected from edit menu | Dialog | No | SelectionPrompt of current commands + green "v" removal confirmation; yellow "No commands to remove" if empty |
| JEA-18 | JEA | Edit -- Toggle Deny | Selected from edit menu | Dialog | No | SelectionPrompt showing command names with colored Allow/Deny status + toggle confirmation; yellow "No commands to toggle" if empty |
| JEA-19 | JEA | Edit -- Add Module | Selected from edit menu | Dialog | No | Name prompt + green "v" added confirmation |
| JEA-20 | JEA | Edit -- Remove Module | Selected from edit menu | Dialog | No | SelectionPrompt of current modules + green "v" removal; yellow "No modules to remove" if empty |
| JEA-21 | JEA | Edit -- Saved | Profile saved after edit | Conversation view | No | Green "v" + "Profile {name} saved." + dim file path |
| JEA-22 | JEA | Edit -- Not Found Error | Profile doesn't exist | Conversation view | No | Red error: "Profile {name} not found." |
| JEA-23 | JEA | Delete -- Selection | `/jea delete` (no name) | Dialog | No | SelectionPrompt of deletable profiles (excludes _global) |
| JEA-24 | JEA | Delete -- No Profiles | No deletable profiles | Conversation view | No | "No profiles available to delete." |
| JEA-25 | JEA | Delete -- Confirmation | Profile selected | Dialog | No | Confirm prompt: "Delete profile {name}?" (default: No) |
| JEA-26 | JEA | Delete -- Success | Confirmed deletion | Conversation view | No | Green "v" + "Profile {name} deleted." |
| JEA-27 | JEA | Delete -- Cancelled | User declines | Conversation view | No | Dim "Cancelled." |
| JEA-28 | JEA | Delete -- Global Error | Attempting to delete _global | Conversation view | No | Red error: "Cannot delete the global profile _global." |
| JEA-29 | JEA | Delete -- Not Found Error | Profile doesn't exist | Conversation view | No | Red error: "Profile {name} not found." |
| JEA-30 | JEA | Effective -- Detail View | `/jea effective` | Window | No | Language mode + command count, Section "Allowed commands" with green "v" per command, Section "Modules" listing, dim "Source profiles:" footer |
| JEA-31 | JEA | Assign -- Selection | `/jea assign` (no name) | Dialog | No | SelectionPrompt of assignable profiles (excludes _global) |
| JEA-32 | JEA | Assign -- Success | Profile assigned | Conversation view | No | Green "v" + "Profile {name} assigned to project {project}." |
| JEA-33 | JEA | Assign -- Already Assigned | Profile already on project | Conversation view | No | "Profile {name} is already assigned to project {project}." |
| JEA-34 | JEA | Assign -- No Project Error | No active project or ambient | Conversation view | No | Red error: "No project selected. Use /project create or --project..." |
| JEA-35 | JEA | Assign -- Profile Not Found | Profile doesn't exist | Conversation view | No | Red error: "Profile {name} not found." |
| JEA-36 | JEA | Assign -- Project Not Found | Active project doesn't exist | Conversation view | No | Red error: "Project {project} not found." |
| JEA-37 | JEA | Assign -- No Profiles Available | No profiles to assign | Conversation view | No | "No profiles available to assign." + dim hint |
| JEA-38 | JEA | Unassign -- Selection | `/jea unassign` (no name) | Dialog | No | SelectionPrompt of currently assigned profiles |
| JEA-39 | JEA | Unassign -- Success | Profile unassigned | Conversation view | No | Green "v" + "Profile {name} unassigned from project {project}." |
| JEA-40 | JEA | Unassign -- Not Assigned Error | Profile not on project | Conversation view | No | Red error: "Profile {name} is not assigned to project {project}." |
| JEA-41 | JEA | Unassign -- No Project Error | No active project or ambient | Conversation view | No | Red error: same as JEA-34 |
| JEA-42 | JEA | Unassign -- No Profiles Assigned | No profiles on project | Conversation view | No | "No JEA profiles assigned to project {project}." |
| JEA-43 | JEA | Unassign -- Project Not Found | Active project doesn't exist | Conversation view | No | Red error: "Project {project} not found." |

### Context Management

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| CTX-01 | Context | Usage Hint | `/context` with no or invalid subcommand | Conversation view | No | Yellow "Usage:" + valid subcommands |
| CTX-02 | Context | Show -- Dashboard | `/context show` | Window | No | Header line (provider, model, token usage with color-coded percentage), stacked bar chart (72 chars wide: blue system, purple tools, green messages, grey free, orange buffer), legend with per-category token counts and percentages, system prompt breakdown tree (meta prompt + session prompt), message breakdown tree (user text, assistant text, tool calls, tool results), tool inventory tree |
| CTX-03 | Context | Show -- No Session Error | No active session | Conversation view | No | Red "Error: No active session." |
| CTX-04 | Context | Summarize -- Menu | `/context summarize [topic]` | Dialog | No | Four-option menu: Apply, Fork conversation, Revise, Cancel |
| CTX-05 | Context | Summarize -- Too Few Messages | < 4 messages in conversation | Conversation view | No | "Not enough conversation to summarize (need at least 4 messages)." |
| CTX-06 | Context | Summarize -- No Session Error | No active session | Conversation view | No | Red "Error: No active session." |
| CTX-07 | Context | Summarize -- No Provider Error | No LLM configured | Conversation view | No | Red "Error: No LLM provider configured." |
| CTX-08 | Context | Summarize -- Failure | LLM returns empty or throws | Conversation view | No | Red error: "Summarization produced no output." or "Summarization failed: {message}" |
| CTX-09 | Context | Prune -- Confirmation | `/context prune` | Dialog | No | LLM-assisted topic boundary pruning with interactive confirmation of pruning boundaries |
| CTX-10 | Context | Prune -- Success | User confirms pruning | Conversation view | No | Green confirmation with count of pruned messages and estimated token savings |
| CTX-11 | Context | Prune -- Cancelled | User declines pruning | Conversation view | No | Dim "Cancelled." |

### Conversation Management

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| SESS-01 | Conversations | Usage Hint | `/conversations` with invalid subcommand | Conversation view | No | Yellow "Usage:" + valid subcommands |
| SESS-02 | Conversations | List Table | `/conversations list` | Window | No | SimpleTable: ID (green + "*" for current), Project, Messages (right-aligned), Last accessed, Preview (first user message, dim, truncated); sorted by last accessed, max 20; footer: "* = current session" |
| SESS-03 | Conversations | List -- Empty | No saved sessions | Conversation view | No | "No saved sessions found." |
| SESS-04 | Conversations | Show -- Detail View | `/conversations show [id]` | Window | No | InfoGrid (Session ID, Created, Last used, Project, Messages, Directory) + "Recent messages" section showing first 5 messages (role-colored: blue user, green assistant) with truncation + "...N more message(s)" if > 5 + dim "Resume with: boydcode --resume {id}" |
| SESS-05 | Conversations | Show -- Not Found Error | Invalid session ID | Conversation view | No | Red error: "Session {id} not found." |
| SESS-06 | Conversations | Show -- Usage | No ID provided | Conversation view | No | Yellow "Usage:" + "/conversations show <id>" |
| SESS-07 | Conversations | Delete -- Confirmation | `/conversations delete [id]` (interactive) | Dialog | No | Shows session ID, message count, project name + confirm prompt "Delete?" (default: No) |
| SESS-08 | Conversations | Delete -- Success | Confirmed deletion | Conversation view | No | Green "v" + "Session {id} deleted." |
| SESS-09 | Conversations | Delete -- Cancelled | User declines | Conversation view | No | Dim "Cancelled." |
| SESS-10 | Conversations | Delete -- Active Session Error | Trying to delete current session | Conversation view | No | Red error: "Cannot delete the current active session." |
| SESS-11 | Conversations | Delete -- Not Found Error | Invalid session ID | Conversation view | No | Red error: "Session {id} not found." |
| SESS-12 | Conversations | Delete -- Usage | No ID provided | Conversation view | No | Yellow "Usage:" + "/conversations delete <id>" |
| SESS-13 | Conversations | Rename -- Name Prompt | `/conversations rename [id]` | Dialog | No | TextPrompt: "New name:" with non-empty validation; updates session display name |
| SESS-14 | Conversations | Rename -- Success | Name provided | Conversation view | No | Green "v" + "Conversation renamed." |
| SESS-15 | Conversations | Rename -- Not Found Error | Invalid session ID | Conversation view | No | Red error: "Session {id} not found." |

### Context Refresh

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| REFRESH-01 | Context | Success Summary | `/context refresh` | Conversation view | No | Green "v" + "Session context refreshed." + summary table with Directories (count + git count), Git branch (with "was:" if changed), Engine (refreshed/kept previous), System prompt (updated with char count diff or unchanged) -- changed items in bold, unchanged in dim |
| REFRESH-02 | Context | No Session Error | No active session | Conversation view | No | Red error: "No active session. Nothing to refresh." |
| REFRESH-03 | Context | Project Not Found Error | Active project deleted | Conversation view | No | Red error: "Project '{name}' not found. It may have been deleted." |
| REFRESH-04 | Context | Missing Directory Warning | Project directory doesn't exist on disk | Conversation view | No | Yellow warning per missing directory |
| REFRESH-05 | Context | Engine Refresh Failure | Engine factory throws | Conversation view | No | Yellow warning: "Engine refresh failed (keeping previous): {message}" |

### Conversations Clear

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| CLEAR-01 | Conversations | Success | `/conversations clear` | Conversation view | No | Green "v" + "Cleared N message(s) from conversation history." |
| CLEAR-02 | Conversations | No Session Error | No active session | Conversation view | No | Red error: "No active session." |

### Expand

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| EXPAND-01 | Expand | Expanded Output | `/expand` with buffered output | Window | No | Full output from last tool execution, each line with 2-space indent |
| EXPAND-02 | Expand | No Output | `/expand` with no buffered output | Conversation view | No | Dim: "No tool output to expand." |
| EXPAND-03 | Expand | Already Expanded | `/expand` called twice | Conversation view | No | Dim: "Output already expanded." |

### Authentication

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| AUTH-01 | Auth | Non-Interactive Error | `boydcode login` in non-interactive terminal | Pre-application output | No | Red error: "Login requires an interactive terminal. Use --api-key or set the appropriate environment variable instead." |
| AUTH-02 | Auth | No OAuth Support Error | Provider doesn't support OAuth | Pre-application output | No | Red: "Provider '{name}' does not support OAuth login." |
| AUTH-03 | Auth | Login Start | `boydcode login` | Pre-application output | No | Bold: "Logging in to {Provider}..." |
| AUTH-04 | Auth | Client Credentials Prompt | Provider requires user-supplied OAuth credentials | Pre-application output | No | Yellow: "This provider requires your own OAuth client credentials." + dim link to Google Cloud Console + Client ID prompt (non-empty) + Client Secret prompt (secret, if required) + GCP Project ID + GCP Location (with default "us-central1") |
| AUTH-05 | Auth | Client ID Missing Error | Empty client ID after resolution | Pre-application output | No | Red: "OAuth client ID is required. Please try again." |
| AUTH-06 | Auth | Browser Opening | Authorization URL built | Pre-application output | No | "Opening browser for authentication..." + dim "If the browser doesn't open, visit:" + clickable URL |
| AUTH-07 | Auth | Waiting for Auth | After browser opens | Pre-application output | No | "Waiting for authorization..." (blocks up to 5 minutes) |
| AUTH-08 | Auth | Token Exchange | Auth code received | Pre-application output | No | "Exchanging authorization code for tokens..." |
| AUTH-09 | Auth | Login Success | Tokens saved | Pre-application output | No | Green: "Successfully logged in!" |
| AUTH-10 | Auth | Login Timeout | 5-minute timeout | Pre-application output | No | Red: "Login timed out. Please try again." |
| AUTH-11 | Auth | Auth Error | OAuth callback error | Pre-application output | No | Red: "{error message}" from callback server |
| AUTH-12 | Auth | Token Exchange Failure | HTTP error during token exchange | Pre-application output | No | Red: "Token exchange failed ({status}):" + error body |
| AUTH-13 | Auth | Token Exchange Null | Response parsed but null | Pre-application output | No | Red: "Failed to exchange authorization code for tokens." |

### Agent Management

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| AGENT-01 | Agents | List Table | `/agent list` | Window | No | Table of available agents with Name, Scope (user/project), Model (or default), Max turns |
| AGENT-02 | Agents | Show -- Detail Panel | `/agent show <name>` | Window | No | Panel with agent name header; body shows description, model, max turns, scope, instruction preview |
| AGENT-03 | Agents | Show -- Not Found Error | Agent name doesn't exist | Conversation view | No | Red error: "Agent '{name}' not found." |

### Context Fork

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| FORK-01 | Context | Fork Success | Fork selected from summarize menu | Conversation view | No | Creates new session seeded with summary; auto-named via LLM; green confirmation with new session name |
| FORK-02 | Context | Fork Naming Failure | LLM auto-naming fails | Conversation view | No | Fork succeeds but falls back to "Fork of {id}" as session name |

### System / Error

| ID | Area | Screen Name | Trigger | Rendering Context | Has Spec | Notes |
|---|---|---|---|---|---|---|
| SYS-01 | System | Crash Panel | Unhandled exception at startup | Pre-application output | No | Red-bordered Panel with header "boydcode crash"; body shows red bold "An unexpected error occurred.", red error message, dim "Details have been written to:" + cyan log file path |
| SYS-02 | System | Crash Fallback | Crash panel rendering itself fails | Pre-application output | No | Plain stderr: "Fatal error: {message}" + "Error log: {path}" |
| SYS-03 | System | Welcome (Legacy) | Legacy welcome rendering | Pre-application output | No | FigletText "BoydCode" in blue + bold tagline + model + working directory + dim usage hint (superseded by STARTUP-01/02) |
| SYS-04 | System | Markdown Panel | Markdown content rendering | Conversation view | No | Rounded-border Panel with escaped markdown text (no actual markdown rendering) |
| SYS-05 | System | Hint Message | Various hint triggers | Conversation view | No | Dim italic text with 2-space indent |
| SYS-06 | System | Success Message | Various success triggers | Conversation view | No | Green "v" + escaped message text |
| SYS-07 | System | Warning Message | Various warning triggers | Conversation view | No | Yellow "Warning:" + escaped message text |
| SYS-08 | System | Error Message | Various error triggers | Conversation view | No | Red bold "Error:" + red message; if message contains "\n  Suggestion: ", splits into error part + yellow "Suggestion:" + dim suggestion text |
| SYS-09 | System | Section Rule | Various section dividers | Conversation view | No | Blank line + left-justified Rule with bold escaped title, dim rule style |

---

## Screen Count Summary

| Area | Count |
|---|---|
| Startup | 11 |
| Terminal Layout | 7 |
| Chat Loop | 19 |
| Execution Window | 17 |
| Cancellation | 2 |
| Help | 1 |
| Project Management | 33 |
| Provider Management | 14 |
| JEA Profile Management | 43 |
| Context Management | 16 |
| Conversation Management | 17 |
| Expand | 3 |
| Agent Management | 3 |
| Context Fork | 2 |
| Authentication | 13 |
| System / Error | 9 |
| **Total** | **210** |

---

## Navigation Map

This diagram shows how screens connect through user actions. Arrows indicate transitions; labels show the triggering action.

```
APPLICATION LAUNCH
    |
    +-- [provider configured] --> STARTUP-01/02 (Banner)
    |       |
    |       +--> STARTUP-03 (Info Grid)
    |       +--> STARTUP-04 (Ready Footer)
    |       +--> STARTUP-06 (Start Hint)
    |       |
    |       +-- [--resume valid] --> STARTUP-07 (Session Resumed)
    |       +-- [--resume invalid] --> STARTUP-08 (Not Found) --> EXIT
    |       |
    |       +--> LAYOUT-01 (Split-Pane Activated)
    |               |
    |               +--> CHAT LOOP (below)
    |
    +-- [no credentials] --> STARTUP-01/02 (Banner)
            |
            +--> STARTUP-03 (Info Grid)
            +--> STARTUP-05 (Not Configured Footer)
            |
            +--> LAYOUT-01 (Split-Pane) --> CHAT LOOP


CHAT LOOP (repeating)
    |
    +-- LAYOUT-02/03 (Input Line) <-- user types
    |       |
    |       +-- [/quit, /exit] --> EXIT
    |       |
    |       +-- [user message] --> CHAT-01 (Thinking)
    |       |       |
    |       |       +-- [streaming] --> CHAT-02 (Streaming Tokens)
    |       |       |       +--> CHAT-03 (Streaming Complete)
    |       |       |       +--> CHAT-05 (Token Usage)
    |       |       |
    |       |       +-- [non-streaming] --> CHAT-04 (Assistant Text)
    |       |               +--> CHAT-05 (Token Usage)
    |       |
    |       |   +-- [tool_use in response] --> TOOL EXECUTION (below)
    |       |   |       +--> (loops back to CHAT-01 for next LLM round)
    |       |   |
    |       |   +-- [end_turn] --> LAYOUT-02 (back to input)
    |       |
    |       +-- [/help] --> HELP-01 (Help Table)
    |       +-- [/project ...] --> PROJECT screens (PROJ-*)
    |       +-- [/provider ...] --> PROVIDER screens (PROV-*)
    |       +-- [/jea ...] --> JEA screens (JEA-*)
    |       +-- [/context ...] --> CONTEXT screens (CTX-*)
    |       +-- [/context prune] --> CTX-09 (Prune Confirmation)
    |       +-- [/conversations ...] --> CONVERSATION screens (SESS-*)
    |       +-- [/context refresh] --> REFRESH-01 (Summary)
    |       +-- [/conversations clear] --> CLEAR-01 (Success)
    |       +-- [/agent list] --> AGENT-01 (Table)
    |       +-- [/agent show] --> AGENT-02 (Detail Panel)
    |       +-- [/expand] --> EXPAND-01/02/03
    |       +-- [unknown /cmd] --> CHAT-15/16 (Unknown Command)
    |       |
    |       +-- [error] --> CHAT-09..14 (Provider Errors)
    |       +-- [fatal] --> CHAT-19 (Fatal Error) --> EXIT


TOOL EXECUTION (per tool_use block)
    |
    +--> EXEC-01 (Tool Call Panel)
    +--> EXEC-02 (Waiting Spinner)
    |       |
    |       +-- [output arrives] --> EXEC-03/04/05 (Streaming Output)
    |       +-- [Esc/Ctrl+C] --> CANCEL-01 (Cancel Hint)
    |       |       +-- [2nd press] --> EXEC-14 (Cancelled)
    |       |       +-- [timeout] --> CANCEL-02 (Hint Cleared)
    |       |
    |       +--> EXEC-06..13 (Tool Result variants)
    |               |
    |               +-- [/expand] --> EXPAND-01 (Expanded Output)


SLASH COMMAND FLOWS
    |
    +-- /project create --> PROJ-02 (Name) --> PROJ-04 (Success)
    |       +-- [configure?] --> PROJ-06 (Section Picker)
    |               +--> PROJ-07 (Dirs) / PROJ-08 (Prompt) / PROJ-09 (Container)
    |               +--> PROJ-10 (Saved)
    |
    +-- /project list --> PROJ-13 (Table) or PROJ-14 (Empty)
    |
    +-- /project show --> PROJ-15 (Detail View)
    |
    +-- /project edit --> PROJ-19 (Edit Menu Loop)
    |       +--> PROJ-20 (Dirs) / PROJ-21 (Prompt) / PROJ-22 (Docker) / PROJ-23 (Require)
    |       +--> PROJ-24 (Saved) --> back to PROJ-19
    |
    +-- /project delete --> PROJ-28 (Confirmation) --> PROJ-29 (Success) or PROJ-30 (Cancelled)
    |
    +-- /provider list --> PROV-02 (Table)
    +-- /provider setup --> PROV-03 (Selection) --> PROV-04 (API Key) --> PROV-05 (Model) --> PROV-06 (Success)
    +-- /provider show --> PROV-09 (Panel) or PROV-10 (No Provider)
    +-- /provider remove --> PROV-11 (Selection) --> PROV-12 (Success)
    |
    +-- /jea list --> JEA-02 (Table)
    +-- /jea show --> JEA-04 (Panel)
    +-- /jea create --> JEA-08 (Name) --> JEA-11 (Language) --> JEA-12 (Add Loop) --> JEA-13 (Success)
    +-- /jea edit --> JEA-14 (Edit Menu Loop) --> JEA-21 (Saved)
    +-- /jea delete --> JEA-23 (Selection) --> JEA-25 (Confirm) --> JEA-26 (Success)
    +-- /jea effective --> JEA-30 (Detail View)
    +-- /jea assign --> JEA-31 (Selection) --> JEA-32 (Success)
    +-- /jea unassign --> JEA-38 (Selection) --> JEA-39 (Success)
    |
    +-- /context show --> CTX-02 (Dashboard)
    +-- /context summarize --> CTX-04 (Menu) --> FORK-01 (Fork Success) or apply/revise/cancel
    +-- /context prune --> CTX-09 (Confirmation) --> CTX-10 (Success) or CTX-11 (Cancelled)
    |
    +-- /agent list --> AGENT-01 (Table)
    +-- /agent show --> AGENT-02 (Detail Panel) or AGENT-03 (Not Found)
    |
    +-- /conversations list --> SESS-02 (Table)
    +-- /conversations show --> SESS-04 (Detail View)
    +-- /conversations rename --> SESS-13 (Rename Prompt) --> SESS-14 (Success)
    +-- /conversations delete --> SESS-07 (Confirmation) --> SESS-08 (Success)
    +-- /conversations clear --> CLEAR-01 (Success)
    |
    +-- /context refresh --> REFRESH-01 (Summary)


AUTHENTICATION (separate command: boydcode login)
    |
    +--> AUTH-03 (Login Start)
    +--> AUTH-04 (Client Credentials, if needed)
    +--> AUTH-06 (Browser Opening)
    +--> AUTH-07 (Waiting)
    +--> AUTH-08 (Token Exchange)
    +--> AUTH-09 (Success) --> EXIT
    |
    +-- [timeout] --> AUTH-10 --> EXIT
    +-- [error] --> AUTH-11/12/13 --> EXIT


CRASH RECOVERY
    |
    +--> SYS-01 (Crash Panel) --> EXIT
    +-- [panel fails] --> SYS-02 (Fallback stderr) --> EXIT
```
