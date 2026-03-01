# Personas

Three user personas that ground UX decisions for the BoydCode terminal
application. Every screen, prompt, and error message should be evaluated against
at least one of these personas. When priorities conflict between personas, the
Primary User wins -- this is who the application is built for. The other two
personas represent important edge cases that must not be broken.

---

## Persona 1: Marcus -- The Daily Driver

**Role:** Solo full-stack developer at a 4-person startup. Uses BoydCode as his
primary AI coding assistant, running it in a terminal alongside his editor for
most of the workday.

### Goals

- Get AI help with coding tasks without leaving the terminal or switching
  context.
- Execute shell commands through the AI assistant to explore codebases, run
  builds, and manipulate files.
- Maintain long-running sessions where the AI accumulates context about his
  current work.
- Manage multiple projects (backend API, frontend SPA, infrastructure scripts)
  with distinct directory scopes and system prompts.

### Technical proficiency

- Comfortable with terminals, git, Docker, and shell scripting. Does not need
  hand-holding for basic CLI concepts.
- Familiar with LLM tools (has used ChatGPT, Copilot, and at least one
  terminal-based AI assistant before). Understands token limits, context windows,
  and model differences at a practical level.
- Not a power user of Spectre.Console or terminal internals -- he does not know
  or care how the split-pane layout works, only that it does.

### Terminal setup

- **OS:** Windows 11 with Windows Terminal (primary), occasional WSL2 sessions.
- **Terminal emulator:** Windows Terminal with a dark theme (One Half Dark or
  similar). Supports ANSI, 256 colors, and Unicode box-drawing characters.
- **Window size:** Typically 120-140 columns by 35-45 rows. Sometimes
  side-by-side with VS Code, which narrows it to 80-90 columns.
- **Font:** Cascadia Code, 11pt.
- **Shell:** PowerShell 7 as default, bash in WSL.

### Usage pattern

- Runs BoydCode 3-5 times per day, with sessions lasting 15 minutes to 2 hours.
- Types 5-30 messages per session. Most sessions involve at least one tool
  execution round (shell commands).
- Uses 3-4 slash commands regularly: `/help` (early on, less now), `/context
  show` (to check token usage), `/conversations clear` (to reset when switching tasks), and
  `/project show` (to verify project config).
- Rarely uses `/jea` commands -- set up his profiles once and has not touched
  them since.
- Switches between 2-3 projects per day using `--project <name>` on the command
  line.
- Has one provider configured (Gemini) and occasionally switches to Anthropic
  for specific tasks using `--provider anthropic`.

### Frustrations -- what would make him stop using the tool

- **Slow startup.** If getting to the input prompt takes more than 3 seconds, he
  starts reaching for the browser-based alternative. The banner is charming the
  first time; by day 30 he barely reads it.
- **Lost context.** If the AI "forgets" what he was working on mid-session due
  to silent context compaction, he loses trust. He wants to know when compaction
  happens and what was lost.
- **Opaque errors.** A red "Error" with no actionable guidance means he has to
  go spelunking in logs. He would rather the tool tell him what to do.
- **Prompts that break flow.** Any interactive prompt that appears when he
  expected output (e.g., an unexpected confirmation dialog) disrupts his typing
  cadence.
- **Frozen terminal.** If a tool execution hangs with no spinner or progress
  indicator, he assumes the application is broken and kills it with Ctrl+C.

### Mental model

Marcus thinks of BoydCode as a **chat window with shell access**. He types
messages, gets responses, and sometimes the AI runs commands on his behalf. He
expects the experience to feel like a conversation, not like navigating a menu
system. Slash commands are "settings" -- he uses them to configure things, not as
his primary interaction mode.

He thinks of the split-pane layout as "the chat area is up top and I type at the
bottom." He does not think about scroll regions, ANSI escape codes, or terminal
capabilities. If something looks wrong visually (garbled output, misaligned
text), he blames the tool, not the terminal.

He expects the AI to remember everything from the current session. He does not
have an intuitive sense of token limits -- the `/context show` dashboard is how
he learned that context windows exist. He now checks it occasionally when
responses start feeling "off."

### Key scenarios

1. **Morning startup:** Opens terminal, runs `boydcode --project backend`. Sees
   the banner with project info and git branch. Types a message immediately.
   Expects to be productive within 5 seconds of launch. (Screens: STARTUP-01/02,
   STARTUP-03, STARTUP-04, STARTUP-06, LAYOUT-01, LAYOUT-02)

2. **Multi-turn coding session:** Has a 20-message conversation where the AI
   reads files, suggests changes, and executes build commands. Watches tool
   execution output stream in real time. Occasionally expands collapsed output
   with `/expand`. (Screens: CHAT-01, CHAT-02, CHAT-03, CHAT-05, EXEC-01
   through EXEC-07, EXPAND-01)

3. **Context check and cleanup:** After a long session, runs `/context show` to
   see token usage. Sees the bar chart is 80% full. Runs `/context summarize` to
   free space, or starts a fresh session. (Screens: CTX-02, CTX-07, CHAT-06)

4. **Project switch mid-day:** Finishes work on the backend, exits with `/quit`.
   Runs `boydcode --project frontend` to start a new session in a different
   project context. (Screens: LAYOUT-02 -> EXIT -> STARTUP-01/02 -> LAYOUT-02)

---

## Persona 2: Priya -- The First-Timer

**Role:** Senior developer at a mid-size company. Her team lead shared BoydCode
as a potential alternative to their current AI coding workflow. She has 20
minutes to install it, try it, and decide whether it is worth a deeper
evaluation.

### Goals

- Get from zero to a working chat session as fast as possible.
- Understand what the tool does and whether it is better than her current setup
  (VS Code + Copilot + browser-based Claude/ChatGPT).
- Form an opinion on whether this is worth recommending to her 8-person team.
- Not waste time debugging configuration issues during the evaluation window.

### Technical proficiency

- Strong developer, comfortable with terminals and CLIs. Uses git, npm, and
  Docker daily.
- Has used multiple AI coding tools but has not used a terminal-based chat
  assistant before. Her mental model for AI tools is "a text box in a browser or
  sidebar."
- Understands API keys conceptually (has set up OpenAI and Anthropic keys for
  other tools) but does not know which environment variable names BoydCode
  expects.

### Terminal setup

- **OS:** macOS Sonoma on a MacBook Pro.
- **Terminal emulator:** iTerm2 with a dark theme. Full ANSI and Unicode
  support.
- **Window size:** Usually full-width at 180+ columns, 50+ rows. But during
  evaluation she may have it in a half-screen split at 90 columns, 40 rows.
- **Font:** JetBrains Mono, 12pt.
- **Shell:** zsh with Oh My Zsh.

### Usage pattern

- This is her first time running the application. She will run it exactly once
  during the evaluation.
- She will read the banner, try to send a message, hit the "not configured"
  error, and need to figure out provider setup.
- If `/provider setup` guides her through smoothly, she proceeds. If it is
  confusing or fails without explanation, she closes the terminal and writes
  "needs work" in her evaluation notes.
- She will try 3-5 messages, including at least one that triggers a tool
  execution, to see how the AI interacts with her codebase.
- She will not explore slash commands beyond `/help` unless something in the
  help output catches her eye.
- She will not configure projects, JEA profiles, or container execution.

### Frustrations -- what would make her stop using the tool

- **Unclear first step.** If she launches the app and does not know what to do
  next, she is out. The "Not configured" footer (STARTUP-05) is her critical
  moment -- it must tell her exactly what to do, not just what is wrong.
- **Jargon without context.** "JEA profiles," "constrained runspace,"
  "InProcess" -- these terms mean nothing to her on first encounter. If they
  appear prominently and are not explained, she assumes the tool is not for her.
- **Configuration that requires leaving the tool.** If she has to go edit a JSON
  file, set an environment variable, or read external documentation to complete
  setup, the evaluation fails. Everything must be achievable from within the
  running application.
- **A wall of text on first launch.** The ASCII art banner is delightful for 3
  seconds. The info grid is useful. But if there are more than 2-3 lines of
  instruction after that, she stops reading.
- **No feedback after actions.** If she runs `/provider setup`, enters her key,
  and the tool returns to the prompt silently, she does not know if it worked.
  The green confirmation (PROV-06) is essential.

### Mental model

Priya thinks of BoydCode as **"ChatGPT but in my terminal."** She expects to
type a message and get a response. She does not yet understand the tool execution
model, the project system, or the JEA security layer. These are things she will
discover -- or not -- based on how well the UI teaches them.

She expects the application to guide her. If she does something wrong, she wants
a clear error with a next step. She does not want to read a manual. The `/help`
table is her reference -- she will scan it quickly and try the commands that look
relevant.

She evaluates the tool on three axes: (1) How fast can I get value? (2) How
polished does it feel? (3) Would my team find this usable without training?

### Key scenarios

1. **First launch (no config):** Installs BoydCode, runs `boydcode`. Sees the
   banner, reads "Not configured," follows the instruction to run `/provider
   setup`. Selects Gemini, pastes her API key, accepts the default model. Sees
   "Provider 'Gemini' configured and activated." Feels confident enough to type
   her first message. (Screens: STARTUP-01/02, STARTUP-03, STARTUP-05,
   LAYOUT-01, LAYOUT-02, PROV-03, PROV-04, PROV-05, PROV-06)

2. **First message:** Types "What can you do?" or a simple coding question. Sees
   the "Thinking..." indicator, then streaming tokens. Reads the response. Notes
   the token usage line but does not fully understand it yet. (Screens: CHAT-01,
   CHAT-02, CHAT-03, CHAT-05)

3. **First tool execution:** Asks the AI to list files in the current directory
   or read a specific file. Sees the tool call panel (EXEC-01), the waiting
   spinner (EXEC-02), and the result. This is the "aha moment" -- the AI can
   interact with her filesystem. (Screens: EXEC-01, EXEC-02, EXEC-03/04/05,
   EXEC-06/07/08)

4. **Bad API key:** Pastes her key incorrectly during `/provider setup`. The
   setup succeeds (no validation), but her first message fails with a 401 error
   (CHAT-09). She needs the error message and suggestion to be clear enough
   that she runs `/provider setup` again without external help. (Screens:
   CHAT-09, PROV-03, PROV-04, PROV-05, PROV-06)

---

## Persona 3: Dana -- The Automator

**Role:** DevOps engineer and tooling lead at a mid-size company. Builds CI
pipelines, internal developer tools, and automation scripts. Interested in
BoydCode as a component in automated workflows, not just as an interactive tool.

### Goals

- Use BoydCode in shell scripts and CI pipelines where there is no interactive
  terminal.
- Get machine-parseable output (exit codes, plain text on stdout, errors on
  stderr) for integration with other tools.
- Manage multiple projects and providers programmatically -- configure once, use
  across environments.
- Understand and customize the security model (JEA profiles, container
  execution) to meet her team's compliance requirements.

### Technical proficiency

- Expert-level terminal and shell scripting. Writes bash, PowerShell, and
  Python automation daily.
- Deep understanding of Docker, CI/CD systems (GitHub Actions, Azure DevOps),
  and infrastructure-as-code.
- Understands LLMs at an API level -- comfortable with tokens, models, rate
  limits, and cost optimization.
- Reads source code when documentation is insufficient.

### Terminal setup

- **OS:** Ubuntu 22.04 (WSL2 on her workstation, native Linux in CI). macOS
  for meetings and email.
- **Terminal emulator:** Alacritty on workstation (minimal, fast). CI
  environments have no interactive terminal at all -- stdout/stderr only.
- **Window size:** Varies wildly. Workstation: 200+ columns, 60 rows.
  CI: effectively infinite width but output is captured, not displayed
  interactively. Sometimes SSH sessions at 80x24.
- **Font:** Fira Code, 10pt (workstation). Whatever the CI runner provides.
- **Shell:** bash everywhere. Does not use zsh or fish in automation contexts.

### Usage pattern

- Uses BoydCode interactively 2-3 times per week for complex tasks (code review,
  architecture questions, debugging).
- Uses BoydCode non-interactively in scripts 5-10 times per day via CI or cron
  jobs (automated code analysis, documentation generation, test scaffolding).
- Has 5+ projects configured with different Docker images, directory scopes, and
  JEA profiles.
- Switches providers based on task: Gemini for long-context analysis, Anthropic
  for code generation, Ollama for local-only sensitive work.
- Uses `--api-key`, `--provider`, `--model`, and `--project` flags extensively.
  Rarely relies on stored configuration in automation contexts.
- Has customized JEA profiles per project to restrict available commands in
  production-adjacent environments.
- Manages container execution settings for projects that need filesystem
  isolation.

### Frustrations -- what would make her stop using the tool

- **Interactive prompts in non-interactive mode.** If the tool hangs waiting for
  input when stdin is not a TTY, her CI pipeline times out at 10 minutes and she
  gets paged. Every interactive prompt must have a flag-based alternative.
- **Inconsistent exit codes.** If the tool returns 0 on failure, her scripts
  cannot detect errors. If different failures return the same non-zero code, she
  cannot differentiate them.
- **Markup in piped output.** If stdout contains ANSI escape codes when piped to
  a file or another tool, parsing breaks. She expects the tool to detect
  non-interactive terminals and strip markup automatically.
- **Configuration that does not compose.** If she cannot override stored
  settings with CLI flags, she has to maintain per-environment config files. CLI
  flags must always take priority over stored state.
- **Undocumented security model.** If she cannot explain to her security team
  exactly what commands the AI can and cannot run, she cannot deploy BoydCode in
  regulated environments. The JEA profile system and container isolation need to
  be transparent and auditable.

### Mental model

Dana thinks of BoydCode as **a CLI tool with an LLM backend.** She puts it in
the same category as `curl`, `jq`, and `gh` -- a composable command-line utility
that takes inputs, produces outputs, and can be scripted. The "chat" framing is
secondary to her; she cares about inputs (flags, stdin), outputs (stdout,
stderr, exit codes), and side effects (file changes, command execution).

She evaluates the tool on three axes: (1) Can I automate it? (2) Can I trust
its security model? (3) Can I integrate it with my existing toolchain?

She reads `/help` output carefully, explores all slash command subcommands, and
runs `/jea effective` to audit the security posture. She reads the conversation
logs at `~/.boydcode/logs/` to understand what the tool is doing under the hood.

### Key scenarios

1. **Non-interactive invocation:** Runs `boydcode --provider anthropic --api-key
   $KEY --project backend` with a message piped via stdin (or a future
   `--message` flag). Expects the response on stdout, errors on stderr, and a
   meaningful exit code. No interactive prompts, no layout activation, no
   spinners. (Screens: LAYOUT-07, no ANSI output)

2. **Provider switching for cost optimization:** In a script, runs different
   tasks against different providers. Uses `--provider ollama` for local
   code indexing (free), `--provider gemini` for long-context analysis (cheap
   input tokens), and `--provider anthropic` for final code generation (best
   quality). Passes `--api-key` and `--model` explicitly each time. (No
   interactive screens -- all flag-driven)

3. **JEA profile audit:** Runs `/jea effective` to see the composed profile for
   the current project. Compares it against her security requirements. Creates
   a restrictive profile with `/jea create`, assigns it to a production project
   with `/jea assign`, and verifies with `/jea effective` again. (Screens:
   JEA-30, JEA-08, JEA-11, JEA-12, JEA-13, JEA-31, JEA-32, JEA-30)

4. **Container execution setup:** Configures a project with
   `RequireContainer = true` and a specific Docker image. Tests that the AI
   can only access the mounted directories. Verifies that commands execute
   inside the container, not on the host. (Screens: PROJ-19, PROJ-22, PROJ-23,
   PROJ-24)

---

## Persona Priority Matrix

When a design decision creates tension between personas, use this priority order:

| Rank | Persona | Rationale |
|---|---|---|
| 1 | Marcus (Daily Driver) | The application exists for daily interactive use. If it is not fast and fluid for Marcus, nothing else matters. |
| 2 | Priya (First-Timer) | If Priya cannot get to value in 10 minutes, Marcus never exists. First-run experience is the funnel. |
| 3 | Dana (Automator) | Non-interactive and advanced use cases are important but must not compromise the interactive experience. Flag-based alternatives are the bridge. |

### Design tension examples

- **Banner length vs. fast startup:** Marcus wants to skip the banner after
  day 1. Priya needs it on day 1 to orient herself. Dana wants no banner at
  all in scripts. **Resolution:** Keep the banner (Priya wins for first-run),
  but make it fast to render and visually scannable so Marcus ignores it
  naturally. Non-interactive terminals skip it entirely (Dana wins).

- **Interactive prompts vs. scriptability:** Marcus likes guided prompts for
  `/project create`. Dana needs a non-interactive alternative. Priya benefits
  from prompts during onboarding. **Resolution:** All interactive prompts have
  a flag-based alternative for non-interactive mode. Interactive mode is the
  default when a TTY is detected.

- **Jargon in error messages:** "JEA profile" means nothing to Priya but is
  precise terminology for Dana. Marcus knows the term because he set it up
  once. **Resolution:** Use the precise term but include enough context that
  the term can be understood from usage. In errors, always include the
  actionable next step regardless of jargon.

- **Token usage display:** Marcus wants a quick glance. Dana wants exact
  numbers for cost tracking. Priya does not yet know what tokens are.
  **Resolution:** The dim one-liner (CHAT-05) serves Marcus. The dashboard
  (CTX-02) serves Dana. Priya discovers tokens naturally through the one-liner
  and can explore via `/context show` when curious.
