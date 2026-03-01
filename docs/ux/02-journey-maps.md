# Journey Maps

Five journey maps covering the complete user lifecycle with BoydCode, from
discovery through advanced configuration. Each phase maps against the personas
defined in [01-personas.md](01-personas.md) and references screen IDs from
[03-screen-inventory.md](03-screen-inventory.md).

---

## Phase 1: Discovery & Install

From hearing about BoydCode to having it installed and ready to run. This phase
happens outside the application, but the decisions made here determine whether
the user ever reaches Phase 2.

**Primary persona:** Priya (First-Timer) -- she needs a reason to try it and a
frictionless install. Dana (Automator) may also be evaluating for CI
integration.

| Step | User Action | System Response | User Feeling | Screen(s) | Opportunities |
|---|---|---|---|---|---|
| D1. Hear about BoydCode | Sees a recommendation from a colleague, blog post, or GitHub README. | n/a | Curious but skeptical. "Another AI coding tool?" | n/a | README must communicate the value proposition in 2 sentences: AI coding assistant with shell access, runs in your terminal, security-first execution model. |
| D2. Visit the repository | Reads README, scans feature list, looks at screenshot or terminal recording. | n/a | Evaluating. Comparing mentally against current tools. | n/a | A terminal recording (asciinema or similar) of a 60-second session -- launch, message, tool execution, response -- is worth more than 500 words of feature descriptions. |
| D3. Check requirements | Looks for .NET version, OS support, dependencies. | n/a | Cautious. ".NET 10 -- do I have that?" | n/a | State requirements upfront in the README. Provide a one-liner to check: `dotnet --version`. If the user needs to install .NET, link directly to the installer. |
| D4. Install BoydCode | Runs the install command (dotnet tool install, build from source, or download binary). | Installation completes. Binary is on PATH. | Hopeful if fast, frustrated if it takes more than 2 minutes or fails. | n/a | Provide a single-line install command. Confirm success with a post-install hint: "Run `boydcode` to get started." |
| D5. Verify installation | Runs `boydcode --version` or `boydcode --help`. | Version string or help text printed to stdout. | Reassured. "It works." | n/a | `--version` should print a single clean line. `--help` should be concise: the chat command, key flags (`--provider`, `--api-key`, `--project`, `--model`), and a one-liner description. |

### Phase 1 Insights

- **Time to first run matters more than feature completeness.** If install
  takes more than 5 minutes, evaluation-mode users (Priya) abandon.
- **The README is the first UI.** It must answer: What does this do? How do I
  install it? How do I get started? -- in that order, above the fold.
- **Post-install guidance bridges to Phase 2.** The install output should end
  with "Run `boydcode` to get started" -- not silence.

---

## Phase 2: First Run & Configuration

The first `boydcode` invocation through successful provider setup. This is the
critical onboarding funnel. Mapped primarily against Priya (First-Timer).

**Primary persona:** Priya (First-Timer). Secondary: Marcus (Daily Driver) on
day 1 -- he goes through this phase once and never returns.

| Step | User Action | System Response | User Feeling | Screen(s) | Opportunities |
|---|---|---|---|---|---|
| F1. Launch BoydCode | Runs `boydcode` in a terminal with no prior configuration. | Banner renders (ASCII art or compact based on terminal height). Info grid shows default provider (Gemini), default model, ambient project, InProcess engine. | Intrigued by the banner. Scanning for "what do I do next?" | STARTUP-01 or STARTUP-02, STARTUP-03 | The banner is a brand moment -- it communicates personality (the tongue-in-cheek metrics sidebar) and competence (clean layout, session info). It must render instantly (< 100ms). |
| F2. See "Not configured" | Reads the footer below the info grid. | Yellow bold "Not configured" followed by dim instruction: "Use /provider setup to configure an API key, or pass --api-key." | Oriented. "OK, I need to set up a provider." The instruction is clear and actionable. | STARTUP-05 | This is the single most important screen for onboarding. If Priya does not understand what to do here, she never reaches a working session. The instruction must name the exact command and be visually distinct from the info grid above it. |
| F3. Layout activates | Observes the terminal transition to split-pane mode. | Conversation view established, separator line appears, input prompt `> ` appears at the bottom. | Slightly disoriented by the layout shift, but the `> ` prompt is a familiar "type here" affordance. | LAYOUT-01, LAYOUT-02 | The layout transition is jarring the first time. Consider whether the banner should remain visible in the conversation view (it does -- it becomes part of scrollback) so the user can scroll up to re-read the setup instruction. |
| F4. Try typing a message (error path) | Types a chat message and presses Enter before configuring a provider. | Red error: "No LLM provider configured. Use /provider setup to configure one." Message is removed from conversation. | Mildly frustrated but not lost -- the error repeats the same instruction from Step F2. | CHAT-08 | The error must feel like guidance, not punishment. It should be the same instruction the user saw at startup, reinforcing the path forward. The message removal is invisible and correct -- no orphaned messages. |
| F5. Run `/provider setup` | Types `/provider setup` and presses Enter. | Interactive prompt takes focus. SelectionPrompt appears: "Select a provider:" with Anthropic, Gemini, OpenAi, Ollama. Green highlight on the current selection. | Engaged. This is a familiar interaction pattern (arrow keys to select). | PROV-03 | The provider list should show the most commonly used providers first. Gemini is the default, but alphabetical order (Anthropic first) is also reasonable. Consider adding a dim one-liner per provider: "Anthropic (Claude models)" so Priya can identify providers by their brand name. |
| F6. Select a provider | Uses arrow keys to navigate, presses Enter to select. | Prompt advances to API key entry. | Confident. The selection felt smooth and responsive. | PROV-03 -> PROV-04 | No opportunities -- this is a well-understood interaction pattern. |
| F7. Enter API key | Pastes or types API key. Characters are masked. Presses Enter. | Key is captured. For Ollama, empty input is accepted. | A moment of mild anxiety: "Did I paste it correctly?" The masked input is reassuring for security but prevents visual verification. | PROV-04 | Consider showing the first 4 characters unmasked (like `/provider list` does in PROV-02) as confirmation after the key is accepted. This is deferred to the confirmation step (F9) where the provider list shows the masked key. |
| F8. Select or accept model | Sees a text prompt with a pre-filled default model name (e.g., "gemini-2.5-pro"). | Presses Enter to accept the default, or types a different model name. | Relieved that there is a sensible default. Most users press Enter here. | PROV-05 | The default value is critical. It should always be a working, current-generation model. If the default model is deprecated or unavailable, the first LLM call will fail (deferred error), which is a poor experience. |
| F9. See confirmation | Reads the success message. | Green: "Provider 'Gemini' configured and activated." Input prompt returns to focus. Status line updates at the bottom of the terminal. | Delighted. The green confirmation is unambiguous. "It worked." | PROV-06, LAYOUT-05 | This is a small celebration moment. The status line update provides ongoing reassurance -- the provider and model are visible at all times. |
| F10. Type first message | Types a question or coding request and presses Enter. | "Thinking..." indicator appears in the output area. | Anticipation. "Will this actually work?" | LAYOUT-02, CHAT-01 | The thinking indicator must appear within 200ms of pressing Enter. Any delay between Enter and visible feedback creates doubt. |
| F11. See first response | Watches tokens stream in character by character. | Response streams into the output area. When complete, a token usage line appears. | Satisfied or impressed, depending on response quality. "OK, this works." | CHAT-02, CHAT-03, CHAT-05 | The streaming creates a sense of responsiveness even when the total response time is several seconds. The token usage line is secondary information -- Priya may not understand it yet, and that is fine. |
| F12. Continue or explore | Types another message, or tries `/help` to see what else is available. | If `/help`: a table of all slash commands with descriptions. If another message: another LLM round. | Curious and engaged. The evaluation is going well. | HELP-01 or CHAT-01 | The `/help` table is Priya's map. It should be concise (fits on one screen at 40 rows), well-organized, and use dim text for subcommand details so the top-level commands are scannable. |

### Phase 2 Insights

- **The funnel is: banner -> "not configured" -> `/provider setup` -> first
  message -> first response.** Every step must succeed or the user drops out.
  Five steps from launch to value. The target is under 3 minutes for a user who
  already has an API key ready.
- **The "not configured" message (STARTUP-05) is the most important piece of
  copy in the application.** It appears to every new user and determines
  whether they proceed or quit. It must be actionable, not just informative.
- **Deferred API key validation (error path E1 in the first-run flow) is a
  known UX debt.** If Priya enters a bad key, she does not find out until Step
  F10 fails. The error message (CHAT-09) must clearly point her back to
  `/provider setup`. Future improvement: validate the key with a lightweight
  API call during setup.
- **The layout transition between steps F3 and F5 is the most visually
  disruptive moment.** The terminal switches from static output to split-pane
  mode, and then `/provider setup` takes focus for interactive prompts.
  This works, but it is worth testing with users unfamiliar with terminal
  applications.

---

## Phase 3: First Chat Session

From first message through the first tool execution and response. This is where
the user builds their mental model of what BoydCode can do. Mapped against Priya
(First-Timer) who has just completed Phase 2, and Marcus (Daily Driver) on his
first day.

**Primary persona:** Priya (First-Timer) continuing from Phase 2. Marcus (Daily
Driver) arrives here on day 1 and returns here every day after.

| Step | User Action | System Response | User Feeling | Screen(s) | Opportunities |
|---|---|---|---|---|---|
| C1. Ask a simple question | Types a coding question or general query. Presses Enter. | Message added to conversation. "Thinking..." appears. Tokens stream in. Response completes. Token usage line appears. | Baseline satisfied. "It works like ChatGPT but in my terminal." | CHAT-01, CHAT-02, CHAT-03, CHAT-05 | This first exchange establishes the interaction cadence: type -> thinking -> stream -> done. The 2-space indent on the AI response visually distinguishes it from the user's input. |
| C2. Ask the AI to read a file | Types something like "Read the README.md in this directory" or "What's in package.json?" | AI decides to use the Shell tool. Text response appears first ("Let me read that file for you."), then a tool call panel shows the command. | Surprised and engaged. "It can actually run commands?" The tool call panel previews the command before execution. | CHAT-02 (text), EXEC-01 (tool call panel) | The tool call panel (EXEC-01) is the user's first encounter with the execution model. It must clearly show what command is about to run. For Priya, this is the "aha moment." |
| C3. Watch tool execution | Observes the execution spinner, then output streaming. | Waiting spinner with "Executing..." and elapsed time. Output lines appear (layout mode: counter; non-layout: scrolling window). Execution completes with green "[Shell]" badge. | Fascinated. Watching the AI execute commands in real time is the differentiating feature. | EXEC-02, EXEC-03 or EXEC-04/05, EXEC-06 or EXEC-07/08 | The execution window must feel fast and responsive. The spinner must appear immediately (< 100ms). The output counter (layout mode) or scrolling window (non-layout) should make it clear that work is happening. |
| C4. Read the AI's analysis | After tool execution, the AI processes the output and streams a response analyzing the file content. | Another "Thinking..." -> streaming -> complete cycle. The response references the file content, demonstrating context. | Impressed. The AI read the file and understood it. This is the value proposition landing. | CHAT-01, CHAT-02, CHAT-03, CHAT-05 | This second LLM round (after the tool call) is where the agentic loop becomes visible to the user. The transition from tool result back to "Thinking..." must be seamless. |
| C5. Ask for a multi-step task | Types something like "Find all TODO comments in the src directory." | AI executes one or more shell commands (grep, find, etc.), potentially in multiple rounds. Each round: text -> tool call -> execution -> result -> text. | Engaged but potentially overwhelmed if there are many tool calls. "How long is this going to take?" | CHAT-01/02/03, EXEC-01 through EXEC-08 (repeated) | Multi-round tool execution is where the experience can degrade. If the AI does 5+ tool calls, the output area fills up with tool panels and execution badges. The collapsed output (EXEC-06) and `/expand` pattern help, but the volume of visual noise is a concern for first-timers. |
| C6. Try cancellation | During a long-running tool execution, presses Esc or Ctrl+C. | First press: dim yellow cancel hint appears: "Press Esc or Ctrl+C again to cancel." Second press (within timeout): execution cancelled, "[Shell] Command cancelled." appears. | In control. The two-press pattern prevents accidental cancellation while keeping the escape hatch discoverable. | CANCEL-01, CANCEL-02, EXEC-14 | The cancel hint auto-clears after 1 second. If the user reads slowly or is distracted, they may miss it. Consider extending to 2-3 seconds. Also: the cancel hint must be visible in the execution area, not the input area, since that is where the user's eyes are. |
| C7. Encounter an error | Triggers a provider error (rate limit, context overflow, network issue). | Red error with category-specific message and actionable suggestion. User message is removed from conversation. | Frustrated but not stuck -- the suggestion tells them what to do. "Let me try again." | CHAT-09 through CHAT-14 | Error quality varies by category. The auth error (CHAT-09) is well-crafted with a clear next step. The generic error (CHAT-14) has no suggestion and is the weakest. Every error should include at least one actionable step. |
| C8. Check help | Types `/help` to see what commands are available. | Table of all slash commands with descriptions, displayed in the conversation view. | Oriented. "There's more I can do here." | HELP-01 | The help table is the user's discovery mechanism for features beyond chat. Subcommands are shown indented and dim, which is correct -- top-level commands are the entry point. |
| C9. End the session | Types `/quit` or `/exit`. | Session auto-saved. Layout deactivated. Terminal returns to normal. | Clean exit. No lingering state or broken terminal. | LAYOUT-02 -> EXIT | The exit must be clean. If the layout deactivation leaves ANSI artifacts (cursor in wrong position, terminal state not restored), the user's next terminal command will look broken. This is tested by `DeactivateLayout` but is fragile across terminal emulators. |

### Phase 3 Insights

- **The tool execution cycle (C2-C4) is the defining UX moment.** This is
  where BoydCode differs from browser-based AI chat. If the execution model
  feels transparent (user sees what commands run), safe (commands are scoped to
  allowed directories), and fast (no unnecessary delays), the user is hooked.
- **Multi-round tool execution (C5) needs visual pacing.** When the AI chains
  3-5 tool calls, the output area fills rapidly. The collapsed-output pattern
  (EXEC-06) mitigates this, but users may still feel overwhelmed. Consider
  a brief "Running step 2 of 3"-style indicator for multi-round turns.
- **Cancellation discoverability (C6) is low.** First-time users do not know
  they can press Esc to cancel. The cancel hint only appears after the first
  press. Consider mentioning Esc in the execution spinner text on the first
  tool execution of a session.
- **Session auto-save on exit (C9) is invisible but critical.** Users who quit
  with `/quit` expect to be able to resume later (via `--resume`). The lack of
  explicit "session saved" feedback is intentional (it is noise for daily
  users) but could confuse first-timers who want reassurance.

---

## Phase 4: Daily Use

A typical work session for a habitual user. Multiple chat turns, tool
executions, maybe a project switch or context compaction. This is where the tool
lives or dies.

**Primary persona:** Marcus (Daily Driver). This is his world -- he is here
every day.

| Step | User Action | System Response | User Feeling | Screen(s) | Opportunities |
|---|---|---|---|---|---|
| W1. Launch with project flag | Runs `boydcode --project backend` from his working directory. | Banner renders with project info: "backend" project name, associated directories with git branches, InProcess engine. "Ready" footer. Start hint. Layout activates. | Routine. He barely reads the banner anymore -- just confirms the project name and git branch in the info grid. | STARTUP-01/02, STARTUP-03, STARTUP-04, STARTUP-06, LAYOUT-01, LAYOUT-02, LAYOUT-05 | For daily users, the banner is visual noise that confirms "I'm in the right place." The info grid (STARTUP-03) is the only part Marcus reads. Consider: is 12 lines of ASCII art the right trade-off for a user who launches the tool 5 times a day? The compact banner (STARTUP-02) at < 30 rows is a good fallback, but Marcus's terminal is usually 35+ rows, so he always gets the full banner. |
| W2. Start working immediately | Types his first message without reading any hints. | "Thinking..." -> streaming response -> token usage. | Productive. He is in his flow within seconds of launch. | CHAT-01, CHAT-02, CHAT-03, CHAT-05 | Startup-to-first-message latency is the key metric for Marcus. Anything over 3 seconds from pressing Enter (on `boydcode --project backend`) to seeing the `> ` prompt feels slow. Engine creation (500ms-2s for InProcess, 3-12s for Container) is the bottleneck. |
| W3. Multi-turn conversation | Engages in a back-and-forth: describes a bug, asks the AI to read relevant files, discusses a fix, asks the AI to implement it. 10-20 messages over 30 minutes. | Each turn: message -> thinking -> optional tool calls -> response -> token usage. Tool executions show command panels, spinners, and results. Output scrolls in the conversation view. | In the zone. The conversation builds context naturally. Tool executions feel like collaborative work. | CHAT-01/02/03/05, EXEC-01 through EXEC-08 (repeated) | The conversation view must handle long sessions gracefully. After 20+ exchanges with tool calls, there is a lot of content in the scrollback. Marcus occasionally scrolls up to reference earlier output -- the conversation view must support this without conflicting with the fixed input area. |
| W4. Check context usage | After 15-20 messages, runs `/context show` to see how full the context window is. | Dashboard with stacked bar chart showing token usage by category. System prompt, tools, messages, free space, and buffer are shown with percentages. | Informed. "I'm at 60%, good for a while" or "85% -- should compact soon." | CTX-02 | The bar chart (72 chars wide) is the quickest way to assess context health. The legend below it has exact token counts for Dana-level precision. The color coding (blue system, green messages, grey free) is intuitive for Marcus. |
| W5. Context compaction (auto or manual) | Either the auto-compaction triggers (CHAT-06) or Marcus runs `/context summarize` manually. | Auto: yellow warning with count of removed messages and estimated token count. Manual: green confirmation with same info. | Slightly uneasy with auto-compaction ("what did it remove?"), satisfied with manual compaction (he chose to do it). | CHAT-06 or CTX-04 | Auto-compaction is the most opaque operation in the application. Marcus knows it happens but does not know exactly which messages were removed. The current warning shows message count and token estimate, but not which messages. Consider: dim list of removed message previews (first 40 chars of each removed user message). |
| W6. Resume after a break | Leaves the session running while he goes to lunch or a meeting. Returns and types a new message. | If the session is still running: normal response. If the terminal was closed: he relaunches with `boydcode --resume <id>` to continue where he left off. | Annoyed if he forgot the session ID. Pleased if `--resume` works smoothly. | STARTUP-07 (resume hint), LAYOUT-01, LAYOUT-02 | Session resume requires knowing the session ID. Marcus can find it via `/conversations list` in a new session, but that adds friction. Consider: `boydcode --resume last` as a shortcut to resume the most recent session. |
| W7. Queue messages while AI is busy | While the AI is processing a long response with tool calls, types ahead -- enters one or more additional messages. | Messages queue. Input line shows dim "[N messages queued]" right-aligned. After the current turn completes, queued messages are processed in order. | Efficient. He does not have to wait for the AI to finish before thinking ahead. | LAYOUT-04 | Message queuing is a power-user feature that Marcus discovers accidentally and then relies on. The dim queue counter is subtle enough not to distract but visible enough to confirm the input was captured. |
| W8. Expand collapsed output | After a tool execution with > 5 lines of output, types `/expand` to see the full output. | Full output renders with 2-space indent per line. | Satisfied. He gets the detail when he needs it without it cluttering the default view. | EXEC-06 (collapsed badge with expand hint), EXPAND-01 | The `/expand` command only shows the last tool output. If Marcus wants to expand output from 3 tool calls ago, he cannot. Consider: `/expand <n>` to expand the nth-most-recent tool output. |
| W9. Switch context mid-session | Realizes he needs to work on a different project. Exits with `/quit`, launches `boydcode --project frontend`. | Session saved, new session started with different project context, directories, and system prompt. | Mild friction from the exit-relaunch cycle, but it is fast (< 5 seconds total). | EXIT -> STARTUP-01/02, STARTUP-03, LAYOUT-01 | Marcus switches projects 2-3 times a day. The exit-relaunch cycle is acceptable but not ideal. A future `/project switch <name>` slash command that reinitializes the session in-place would reduce friction, but it would require re-creating the execution engine, which has side effects. |
| W10. End the workday | Types `/quit` when done for the day. | Session auto-saved. Layout deactivated. Terminal returns to normal shell prompt. | Clean closure. The session is saved and can be resumed tomorrow if needed. | EXIT | Marcus rarely resumes sessions across days -- he usually starts fresh. The auto-save is invisible insurance for the rare case where he wants to pick up where he left off. |

### Phase 4 Insights

- **Startup speed is the daily-use gating factor.** Marcus launches the tool
  5 times a day. At 3 seconds per launch, that is 15 seconds daily of "staring
  at the banner." At 10 seconds per launch (Container mode), it is nearly a
  minute. Engine creation is the bottleneck and the most impactful optimization
  target.
- **Context management is an intermediate skill.** Marcus learned about token
  limits through experience (responses got worse as the context filled up). The
  `/context show` dashboard educated him. Auto-compaction is a safety net, but
  its opacity creates unease. Better visibility into what was removed would
  build trust.
- **The conversation view is the session's canvas.** After 30 minutes of use, the
  scrollback contains 50+ distinct visual elements (messages, tool panels,
  execution badges, token lines). The visual hierarchy (indented AI text, grey
  tool panels, dim token lines) works, but long sessions benefit from the
  Section Rule pattern to create visual breaks.
- **Message queuing (W7) and output expansion (W8) are power-user features
  that reward discovery.** Neither is documented prominently. Both are
  mentioned in the collapse badge hint ("/expand to show full output") and
  the input line state (queue counter), which is the right level of
  discoverability -- not hidden, but not noisy.

---

## Phase 5: Advanced Configuration

Setting up projects, JEA profiles, container execution, and switching providers.
This phase maps against Dana (Automator) who needs full control over the
security and execution model.

**Primary persona:** Dana (Automator). Secondary: Marcus (Daily Driver) who
went through this once to set up his projects and JEA profiles.

| Step | User Action | System Response | User Feeling | Screen(s) | Opportunities |
|---|---|---|---|---|---|
| A1. Create a project | Runs `/project create backend` or `/project create` (prompted for name). | Name prompt (if not provided) -> success confirmation -> "Configure project settings now?" confirmation -> section picker if yes. | Methodical. Dana appreciates that the tool offers guided configuration but does not force it -- she can create the project bare and configure it later. | PROJ-02, PROJ-04, PROJ-05, PROJ-06 | The section picker (PROJ-06) uses a MultiSelectionPrompt with "Directories", "System prompt", "Container settings." This is well-structured but could include a preview of what each section configures (dim one-liner per choice). |
| A2. Configure project directories | Selects "Directories" from the section picker. Enters paths and access levels. | Repeating loop: path prompt (Enter to finish) -> access level selection (ReadWrite / ReadOnly) -> green confirmation per entry. | Satisfied with the loop pattern. Each directory is confirmed individually. The "Enter to finish" escape hatch is discoverable. | PROJ-07 | The access level selection is simple (two choices) but could show the implication: "ReadWrite -- AI can create and modify files" vs. "ReadOnly -- AI can read but not modify." This is important for Dana's security requirements. |
| A3. Configure container execution | Selects "Container settings" from the section picker or edits later via `/project edit`. | Docker image prompt -> require container confirmation -> success messages. | Careful. Dana is configuring the security boundary. She wants to know exactly what "RequireContainer" means. | PROJ-09, PROJ-22, PROJ-23 | The "Require container" confirmation (PROJ-23) should explain the implication: "When enabled, the AI can only execute commands inside the Docker container. Falling back to in-process execution is disabled." Currently it shows a warning if no Docker image is set, which is correct but could be more explicit about the security guarantee. |
| A4. Set up JEA profiles | Runs `/jea create restricted` to create a custom profile. Selects language mode. Adds allowed/denied commands one at a time. | Name prompt -> language mode selection -> repeating loop: Add command / Add module / Done. Each command gets a name prompt and Allow/Deny selection. | Tedious but thorough. The one-at-a-time loop ensures precision but is slow for profiles with 20+ commands. | JEA-08, JEA-11, JEA-12, JEA-13 | The add loop (JEA-12) is the right pattern for small profiles but does not scale. Consider: `/jea import <file>` to bulk-import commands from a text file, or `/jea edit <name>` which opens the profile file in `$EDITOR`. The file-based store (`~/.boydcode/jea/{name}.profile`) is already human-readable, so direct editing is viable. |
| A5. Assign JEA profile to project | Runs `/jea assign restricted`. | Profile selection prompt (if name not provided) -> green confirmation: "Profile 'restricted' assigned to project 'backend'." | Satisfied. The assignment model (profiles to projects) is clear and composable. | JEA-31, JEA-32 | The assignment is contextual -- it assigns to the currently active project. If Dana wants to assign to a different project, she must switch projects first. Consider: `/jea assign <profile> --project <project>` to decouple from the active project. |
| A6. Verify effective security | Runs `/jea effective` to see the composed profile (global + project-specific). | Language mode, allowed command list with green checks, module list, source profile names in dim footer. | Confident. She can see exactly what the AI can and cannot do. The "deny always wins" composition rule is visible in the output. | JEA-30 | The effective view is Dana's audit tool. It shows the result but not the composition logic (which profile contributed which command). Consider: annotating each command with its source profile, e.g., "Get-ChildItem (global)" vs. "docker (restricted)." |
| A7. Configure multiple providers | Runs `/provider setup` multiple times, once per provider (Gemini, Anthropic, Ollama). | Each setup: provider selection -> API key -> model -> confirmation. | Systematic. She is building her toolkit of providers. | PROV-03, PROV-04, PROV-05, PROV-06 (repeated) | After configuring 3 providers, Dana runs `/provider list` (PROV-02) to verify all are ready. The "active" / "ready" status column confirms which one is currently in use. The masked API key column provides security without hiding configuration problems. |
| A8. Switch providers at runtime | Uses `--provider anthropic` on launch or configures via `/provider setup` in-session to switch the active provider. | Provider activated, status line updated. Next LLM call uses the new provider. | Efficient. She can switch providers without exiting the session. | PROV-06, LAYOUT-05 | In-session provider switching via `/provider setup` re-runs the full setup flow (selection, key, model). A lighter `/provider switch <name>` command that activates an already-configured provider would be faster for Dana's use case. |
| A9. Review session history | Runs `/conversations list` to see recent sessions across all projects. | Table with session ID, project, message count, last accessed date, and message preview. Current session marked with green asterisk. | Organized. She can see which sessions belong to which projects and when they were last used. | SESS-02 | The session list is capped at 20 entries. For Dana, who runs many automated sessions, this limit may be too low. Consider: `/conversations list --all` or `/conversations list --project backend` for filtered views. |
| A10. Non-interactive usage | Runs `boydcode --provider anthropic --api-key $KEY --project backend` with stdin piped from a script. | Layout is skipped (non-interactive). Fallback prompt used. No spinners or ANSI formatting. Output goes to stdout, errors to stderr. | Confident if it works. Furious if an interactive prompt blocks the pipeline. | LAYOUT-07 | This is Dana's most critical path. Every interactive prompt in the application must check `AnsiConsole.Profile.Capabilities.Interactive` and fall back to flag-based alternatives. Current coverage: `/provider setup` (PROV-07 error), `/project edit` (PROJ-27 error). Ensure all other interactive-only features degrade gracefully. |

### Phase 5 Insights

- **JEA profile management (A4-A6) is the most complex user-facing feature.**
  The create/edit loop pattern works for small profiles but does not scale to
  enterprise use cases where profiles have 30+ commands. File-based import or
  direct file editing would serve Dana better.
- **The effective view (A6) is the trust-building feature.** Dana cannot
  recommend BoydCode to her security team without it. It must be accurate and
  comprehensive. Annotating commands with their source profile would make it
  auditable.
- **Provider switching (A7-A8) has two modes: setup (full flow) and switch
  (activate existing).** Only the full setup flow exists today. A lightweight
  switch command would reduce friction for users with multiple configured
  providers.
- **Non-interactive mode (A10) is a binary boundary.** Either it works
  completely (no interactive prompts, no ANSI in piped output, correct exit
  codes) or it does not work at all. There is no "partial non-interactive."
  Every new feature must be evaluated against this constraint.
- **Session management at scale (A9) needs filtering.** Twenty sessions across
  multiple projects and providers is a manageable list for Marcus but
  insufficient for Dana's automated workflows. Project-based and date-based
  filtering are natural extensions.

---

## Cross-Phase Opportunities Summary

These are the highest-impact UX opportunities identified across all five phases,
ordered by estimated impact on user retention.

| # | Opportunity | Phase(s) | Persona(s) | Impact |
|---|---|---|---|---|
| 1 | API key validation during `/provider setup` to catch bad keys before the first LLM call | 2, 3 | Priya, Marcus | High -- eliminates the deferred-error path that is confusing for new users |
| 2 | `boydcode --resume last` shortcut to resume the most recent session without knowing the ID | 4 | Marcus | Medium -- reduces daily friction for the primary use case |
| 3 | Lightweight `/provider switch <name>` to activate an already-configured provider without re-entering credentials | 4, 5 | Dana, Marcus | Medium -- simplifies the most common provider-switching workflow |
| 4 | Source-profile annotations in `/jea effective` output | 5 | Dana | Medium -- makes the security model auditable for enterprise adoption |
| 5 | Auto-compaction transparency -- brief description of what was removed | 4 | Marcus | Medium -- builds trust in the context management system |
| 6 | Cancellation hint on the first tool execution of a session ("Press Esc to cancel") | 3 | Priya | Low-medium -- improves discoverability of a safety-critical feature |
| 7 | `/expand <n>` to expand the nth-most-recent tool output | 4 | Marcus | Low -- power-user convenience |
| 8 | `/conversations list --project <name>` for filtered session lists | 5 | Dana | Low -- automation convenience |
| 9 | Bulk JEA profile import from file | 5 | Dana | Low -- enterprise convenience, workaround exists (edit profile files directly) |
| 10 | Post-install hint in the install output ("Run `boydcode` to get started") | 1 | Priya | Low -- one-time improvement to the discovery funnel |
