# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# BoydCode

AI coding assistant with JEA-constrained PowerShell execution, built on Clean Architecture.

## Build & Test

```bash
# Build
dotnet build src/BoydCode.slnx

# Run all tests
dotnet test src/BoydCode.slnx

# Run a specific test project
dotnet test src/tests/BoydCode.Domain.Tests
dotnet test src/tests/BoydCode.Application.Tests

# Run the console app
dotnet run --project src/BoydCode.Presentation.Console
```

## Architecture

Clean Architecture with strict layer separation. Domain has zero external dependencies.

```
Presentation.Console          → Terminal.Gui TUI + Spectre.Console rendering, DI host, ChatCommand
Application                   → AgentOrchestrator, interfaces
Infrastructure.LLM            → MEAI adapter, multi-provider (Anthropic/Gemini/OpenAI/Ollama)
Infrastructure.PowerShell     → ConstrainedRunspaceEngine (JEA isolation, in-process mode)
Infrastructure.Container      → ContainerExecutionEngine (Docker-based execution, container mode)
Infrastructure.Persistence    → JsonSessionRepository, JsonProjectRepository, FileJeaProfileStore, FileSettingsProvider, auth stores
Domain                        → Entities, records, enums, configuration models (no dependencies)
```

**Dependency rule:** Layers depend inward only. Infrastructure and Presentation depend on Application; Application depends on Domain.

## Key Patterns

- **DI throughout** — Microsoft.Extensions.DependencyInjection; constructor injection
- **IOptions\<T\>** for configuration binding (`AppSettings`, `LlmProviderConfig`, `ExecutionConfig`)
- **Sealed classes** for entities (`Session`, `Conversation`); **sealed records** for DTOs/value objects
- **Immutable collections** — `IReadOnlyList<T>` enforced on public APIs
- **Factory + Adapter** — `LlmProviderFactory` creates the correct LLM adapter per provider: `MeaiLlmProviderAdapter` (Anthropic/OpenAI/Ollama) or `GeminiLlmProviderAdapter` (Gemini); default provider is `gemini-2.5-pro`. `ExecutionEngineFactory` creates the correct execution engine (constrained runspace or container) based on project `ExecutionConfig`
- **Provider capabilities** — `ProviderCapabilities` record on `ILlmProvider.Capabilities` declares what a provider supports (e.g., explicit prompt caching, streaming, extended thinking); `ProviderDefaults.For(LlmProviderType)` returns the canonical `ProviderCapabilities` for each provider; `ProviderDefaults.DefaultModelFor(LlmProviderType)` returns the default model name
- **Provider-agnostic OAuth** — `OAuthProviderRegistry` holds per-provider `OAuthProviderConfig` entries; `ICredentialStore` is keyed by `LlmProviderType` so credentials for each provider are stored and retrieved independently
- **Shell-only execution (ADR-0003)** — Single `Shell` tool delegates to `IExecutionEngine`; no dedicated tools, no permission engine, no hook engine. JEA profiles (in-process) and container boundaries (Docker) are the sole security mechanisms. The orchestrator drives the agentic loop directly
- **Execution engine factory** — `IExecutionEngineFactory` creates the correct engine at session startup based on project config; keyed `ExecutionEngineCreator` delegates are registered by each infrastructure project (PowerShell registers `InProcess`, Container registers `Container`); `ActiveExecutionEngine` singleton holds the current session's engine; falls back to in-process if container is unavailable and `AllowInProcess` is true
- **First-class container config** — `Project.DockerImage` and `Project.RequireContainer` are promoted, user-facing properties; `Project.BuildExecutionConfig()` synthesizes an `ExecutionConfig` from them at runtime (RequireContainer → Container mode + AllowInProcess=false; DockerImage → ContainerConfig); old `Execution` block remains for JEA profiles and internal use
- **Container execution** — Docker containers provide filesystem isolation via volume mount flags (read-only vs read-write per `DirectoryAccessLevel`); `PersistentShellSession` maintains a long-lived stdin/stdout session with sentinel-framed output parsing
- **JEA profiles** — Composable profile system via `IJeaProfileStore`; editable `_global` profile (seeded from `BuiltInJeaProfile` on first access, stored at `~/.boydcode/jea/_global.profile`) replaces the hardcoded built-in in composition; deny-always-wins when multiple profiles are merged; `/jea` slash command provides full CRUD (`list`, `show`, `create`, `edit`, `delete`), `effective` view (composed result for current session), and `assign`/`unassign` for tying profiles to projects
- **Git-aware directory resolution** — `DirectoryResolver` enriches `ProjectDirectory` entries with filesystem existence checks and git metadata (branch, repo root) via pure filesystem reads (no git CLI); concrete `DirectoryGuard.ConfigureResolved()` stores both resolved metadata and the access-level rules (the `IDirectoryGuard` interface exposes only `GetAccessLevel`)
- **LlmRequest envelope and prefix-cache field ordering** — `LlmRequest` sealed record (`Domain/LlmRequests/`) encapsulates all inputs to an LLM API call. Properties are declared in cache-priority tier order: Tier 1 (session-constant: `Model`, `SystemPrompt`, `Tools`, `ToolChoice`, `Directories`), Tier 2 (rarely changed: `Sampling`, `Thinking`, `Metadata`), Tier 3 (per-turn: `Messages`, `Stream`). Adapters serialize in declaration order so stable fields lead the payload and maximize provider-side prefix cache hits. Supporting value objects: `ToolChoiceStrategy` (enum: Auto/Any/None), `SamplingOptions`, `ThinkingConfig`, `RequestMetadata`. See `docs/adr/0001-prefix-cache-optimized-request-ordering.md`
- **System prompt ownership** — `ChatCommand` sets `session.SystemPrompt` to a composite of project name, the project's custom prompt (or `Project.DefaultSystemPrompt`), and resolved directory context. `AgentOrchestrator` prepends `MetaPrompt.Text` (a static Domain constant describing the shell-only execution model) when constructing `LlmRequest.SystemPrompt` each turn — or uses `MetaPrompt.Text` alone if no session prompt is set. `Conversation` is a pure message accumulator and does not hold the system prompt
- **Response envelope and agentic loop** — Every request includes a `tools` array containing the Shell tool's name, description, and JSON parameter schema. The model emits structured `tool_use` content blocks when it decides a command would help. Responses mix `TextBlock` (explanatory text) and `ToolUseBlock` (tool invocation with `Id`, `Name`, `ArgumentsJson`) content blocks. The `stop_reason` field signals intent: `"tool_use"` means there are tool calls to execute (`LlmResponse.HasToolUse`), `"end_turn"` means the turn is complete. The orchestrator runs an agentic loop: stream/render text → detect tool calls → execute via execution engine → add `ToolResultBlock` (keyed by the `tool_use` block's `Id` for correlation) → send updated conversation back to the LLM → repeat until `stop_reason` is `"end_turn"` or max rounds reached
- **Response streaming** — `StreamChunk` discriminated union (`TextChunk`, `ToolCallChunk`, `CompletionChunk`) enables structured streaming from `ILlmProvider.StreamAsync`; `StreamAccumulator` collects chunks into `LlmResponse` for the tool execution loop; `StreamingResponseConverter` bridges MEAI `ChatResponseUpdate` to domain `StreamChunk`; orchestrator streams by default, falls back to `SendAsync` when `SupportsStreaming` is false

## TUI Patterns (Presentation.Console)

UX design docs live in `docs/ux/`. These are prescriptive — they define what the UI SHOULD look like. All implementation must conform to these specs. **When a task intentionally changes the UX design, the docs in `docs/ux/` MUST be updated before or alongside the code changes.** Do not implement code that contradicts the current spec — update the spec first.

### Technology Stack

**Terminal.Gui** (v2) owns the application shell — screen lifecycle, view hierarchy, layout, input handling, windowing. **Spectre.Console** renders rich content (tables, panels, rules, markup) into Terminal.Gui views via string rendering. They are complementary.

### Rendering Principles

- **Compose, don't splat**: Build `IRenderable` trees from Spectre widgets (`Rows`, `Grid`, `Markup`, `Text`, `Rule`, `Panel`). Render to string via `AnsiConsole.Create()` with `StringWriter`, then display in Terminal.Gui views
- **Factory methods returning `IRenderable`**: Follow the `ConversationRenderables` pattern — static factory methods that return composed renderables
- **Data records for renderables**: Decouple renderables from domain types. Pass a flat sealed record (e.g., `BannerData`) so the renderable has zero domain dependencies and is testable by constructing the record directly

### Windowing Model

Non-conversation content opens in Terminal.Gui windows, not the conversation view:
- **Read-only info** (`/help`, `/agent list`, `/jea show`, `/expand`) → modeless Window (agent keeps working, Esc to dismiss)
- **Interactive workflows** (`/project create`, `/provider setup`, `/jea edit`) → modal Dialog (blocks until complete)
- **Conversation content** (user messages, assistant responses, tool badges, streaming) → conversation view only

### Interactive Prompts

For interactive prompts during Terminal.Gui session, prefer Terminal.Gui equivalents (Dialog, ListView, TextField, MessageBox). When Spectre prompts are needed, the Terminal.Gui application must be suspended first.

### TUI Architecture

Terminal.Gui `Application` runs for the entire session. The view hierarchy:
- **ConversationView** — scrollable conversation history (messages, tool output, streaming)
- **ActivityBar** — spinner + state label (Thinking/Streaming/Executing)
- **InputView** — user text input with line editing
- **StatusBar** — session metadata + key hints
- Background threads update views via `Application.Invoke()` for thread safety

## Target & Toolchain

- **.NET 10** / **C# 13** (`net10.0`, `LangVersion 13`)
- Central package management (`Directory.Packages.props`)
- `TreatWarningsAsErrors: true`, `WarningLevel: 9999`, `AnalysisLevel: latest-recommended`
- Nullable reference types enabled
- File-scoped namespaces (enforced as warning)

## Code Style

Enforced via `.editorconfig`:

- **Indentation:** 2 spaces (all file types)
- **Line endings:** LF, UTF-8
- **Braces:** Allman style (new line before all opening braces)
- **Namespaces:** File-scoped (`namespace Foo;`)
- **var:** Preferred everywhere
- **Private fields:** `_camelCase` with underscore prefix
- **Interfaces:** `I` prefix, PascalCase (`IExecutionEngine`)
- **Types:** PascalCase; sealed when not designed for inheritance
- **Expression-bodied:** Properties/accessors yes; methods only when single-line; constructors no
- **Pattern matching** over `is`/`as` checks; null coalescing/propagation preferred
- **Modifier order:** public, private, protected, internal, static, extern, new, virtual, abstract, sealed, override, readonly, unsafe, volatile, async
- **Usings:** System directives first, no blank line separation

## Testing

- **xUnit** — test framework; `[Fact]` for deterministic tests
- **NSubstitute** — mocking/stubbing
- **FluentAssertions** — assertion library (`.Should().Be()` style)
- **Arrange-Act-Assert** pattern
- Test projects mirror source: `BoydCode.{Layer}.Tests`

## Project Structure

```
src/
  BoydCode.Domain/                      # Core domain models, no dependencies
  BoydCode.Application/                 # Use cases, interfaces, orchestration
  BoydCode.Infrastructure.LLM/          # LLM provider integration (MEAI + Gemini native)
  BoydCode.Infrastructure.PowerShell/   # JEA constrained runspace (in-process execution)
  BoydCode.Infrastructure.Container/    # Docker container execution engine
  BoydCode.Infrastructure.Persistence/  # Session storage, settings, stubs
  BoydCode.Presentation.Console/        # CLI entry point (Spectre.Console)
  tests/
    BoydCode.Domain.Tests/
    BoydCode.Application.Tests/
    BoydCode.Infrastructure.LLM.Tests/
    BoydCode.Infrastructure.PowerShell.Tests/
    BoydCode.Infrastructure.Container.Tests/
```

## Key Interfaces

| Interface | Purpose | Implementation |
|---|---|---|
| `ILlmProvider` | LLM communication via `LlmRequest` envelope; exposes `Capabilities` (`ProviderCapabilities`) | `MeaiLlmProviderAdapter` (Anthropic/OpenAI/Ollama), `GeminiLlmProviderAdapter` (Gemini) |
| `IExplicitCacheProvider` | Opt-in explicit prompt caching; `CreateCacheAsync` stores content by name/TTL, `SendWithCacheAsync` references a stored cache ID alongside an `LlmRequest` | `GeminiLlmProviderAdapter` |
| `ILlmProviderFactory` | Provider creation; selects adapter based on `LlmProviderType` | `LlmProviderFactory` |
| `IContextCompactor` | Context window management (messages only; system prompt budget handled by caller) | `EvictionContextCompactor` |
| `IDirectoryGuard` | Enforces per-project directory access rules; `GetAccessLevel(path)` returns `DirectoryAccessLevel` | `DirectoryGuard` |
| `IExecutionEngine` | Command execution; exposes `InitializeAsync`, `ExecuteAsync`, `GetAvailableCommands` | `ConstrainedRunspaceEngine` (in-process), `ContainerExecutionEngine` (Docker) |
| `IExecutionEngineFactory` | Creates the correct execution engine based on `ExecutionConfig.Mode` | `ExecutionEngineFactory` |
| `IJeaProfileStore` | CRUD for JEA profiles (allow/deny command lists) | `FileJeaProfileStore` |
| `ISessionRepository` | Session persistence | `JsonSessionRepository` |
| `IProjectRepository` | CRUD for named projects | `JsonProjectRepository` |
| `IProviderConfigStore` | Persists per-provider profiles and tracks last-used provider | `JsonProviderConfigStore` |
| `ISettingsProvider` | App configuration | `FileSettingsProvider` |
| `ICredentialStore` | Provider-keyed credential storage (takes `LlmProviderType`) | `JsonCredentialStore` |
| `IOAuthClientConfigStore` | OAuth client configuration per provider | `JsonOAuthClientConfigStore` |
| `IUserInterface` | User interaction | `SpectreUserInterface` |
| `ISlashCommandRegistry` | Slash command lookup and dispatch | `SlashCommandRegistry` |
