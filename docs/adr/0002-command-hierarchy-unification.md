# ADR-0002: Command Hierarchy: CLI Subcommands vs Interactive Slash Commands

**Status:** Accepted

---

## Context

BoydCode exposes two distinct command entry points that serve different phases of the application lifecycle.

**CLI subcommands** are registered with Spectre.Console.Cli in `Program.cs` and execute before any session state exists:

```csharp
// Program.cs
app.Configure(config =>
{
  config.SetApplicationName("boydcode");
  config.AddCommand<ChatCommand>("chat").WithDescription("Start an interactive chat session");
  config.AddCommand<LoginCommand>("login").WithDescription("Log in with your LLM provider subscription (Anthropic, Gemini)");
});
app.SetDefaultCommand<ChatCommand>();
```

**Interactive slash commands** are registered with `ISlashCommandRegistry` and dispatched inside the running chat loop. They have full access to DI-hosted session singletons (`ActiveProvider`, `ActiveProject`, `ActiveExecutionEngine`):

```csharp
// Program.cs
hostBuilder.Services.AddTransient<ISlashCommand, ProjectSlashCommand>();
hostBuilder.Services.AddTransient<ISlashCommand, HelpSlashCommand>();
hostBuilder.Services.AddTransient<ISlashCommand, ProviderSlashCommand>();
hostBuilder.Services.AddTransient<ISlashCommand, JeaSlashCommand>();
```

This produces a visible asymmetry: `login` is a CLI subcommand (`boydcode login`), while provider setup is a slash command (`/provider setup`). Users familiar with one entry point may look for the equivalent in the other. The root cause is not a design inconsistency — it reflects a real structural constraint.

**Why `login` must be a CLI subcommand.** `LoginCommand` runs a browser-based OAuth PKCE flow: it binds a local HTTP callback server, opens the system browser, and exchanges an authorization code for tokens. This flow must complete before a chat session starts so that valid credentials are available when `ChatCommand.ExecuteAsync` activates the provider. At that point, no `ActiveProvider` singleton exists yet — the DI container is built but the session singletons are in their default (un-activated) state. `LoginCommand` therefore cannot be a slash command; it has no session to read from or write to.

**Why slash commands cannot be CLI subcommands.** Slash commands such as `/project show`, `/provider setup`, and `/jea effective` operate on live session singletons. `ProjectSlashCommand` reads `ActiveProject.Name` to default the project name when none is given. `ProviderSlashCommand` calls `ActiveProvider.Activate(...)` to switch providers mid-session. `JeaSlashCommand` inspects the current execution engine's effective JEA config. These dependencies are populated only after `ChatCommand.ExecuteAsync` has run its startup sequence (provider activation, engine creation, session construction). Hoisting them to CLI subcommands would require duplicating that startup sequence or introducing a separate bootstrapping path.

---

## Decision

We keep the current two-tier split and define a formal classification rule:

| Tier | Entry point | When it runs | May depend on |
|------|-------------|--------------|---------------|
| Pre-session | CLI subcommand (`boydcode <verb>`) | Before `ChatCommand.ExecuteAsync` | DI container only; no session singletons |
| In-session | Slash command (`/<verb>`) | Inside the chat loop | Full session state: `ActiveProvider`, `ActiveProject`, `ActiveExecutionEngine`, `ISlashCommandRegistry` |

Any new command must be placed in the tier that matches its dependencies. A command that reads or writes `ActiveProvider`, `ActiveProject`, or `ActiveExecutionEngine` is in-session. A command that only needs the DI container (credentials, config, repositories) is pre-session.

### Phase 1 — Current state (this ADR)

Document the split and the classification rule. No changes to `Program.cs`, `ChatCommand`, or any slash command. Help text in both tiers will point users to the correct entry point when they reach the wrong one.

The banner already handles the unconfigured-provider case:

```csharp
// ChatCommand.cs — rendered when no API key is found
AnsiConsole.MarkupLine("  [dim]Use[/] [bold]/provider setup[/] [dim]to configure an API key, or pass[/] [bold]--api-key[/][dim].[/]");
```

### Phase 2 — Read-only CLI mirrors for stateless slash commands

Add CLI subcommands for slash command operations that do not require session state and are useful outside of an interactive session (scripting, CI, dotfiles setup). Candidates:

- `boydcode project list` — delegates to `IProjectRepository.ListNamesAsync`
- `boydcode project show <name>` — delegates to `IProjectRepository.LoadAsync`
- `boydcode provider list` — delegates to `IProviderConfigStore.GetLastUsedProviderAsync`
- `boydcode jea list` — delegates to `IJeaProfileStore.ListNamesAsync`

These operations read from persistence only; they do not touch `ActiveProvider` or `ActiveExecutionEngine`. To avoid duplicating rendering logic, Phase 2 will introduce a shared command implementation layer — thin service classes in `BoydCode.Application` that both the CLI subcommand and the slash command can call. The slash command retains its interactive UX (selection prompts, inline editing); the CLI subcommand emits plain text or structured output.

### Phase 3 — `/login` alias from within a session

Add a `/login` slash command that delegates to the `LoginCommand` OAuth flow from inside a running session. This requires `LoginCommand` to be refactored into an injectable service (`ILoginService` or similar) so it can be consumed by both the CLI subcommand and the slash command without inheriting from `AsyncCommand`. The slash command wrapper would then call into that service and, on success, re-activate the provider via `ActiveProvider.Activate(...)`.

Phase 3 is deferred until a concrete user need is established.

---

## Consequences

### Benefits

- **Explicit rule eliminates future ambiguity.** Every new command has a deterministic home: check whether it reads `ActiveProvider`, `ActiveProject`, or `ActiveExecutionEngine`; if yes, it is a slash command.
- **No breaking changes in Phase 1.** Existing CLI arguments and slash command names are unchanged.
- **Phase 2 mirrors are additive.** Adding `boydcode project list` does not affect `/project list`; both can coexist with the same underlying implementation.

### Costs and risks

- **Visible asymmetry until Phase 2.** Users running `boydcode provider list` from a shell today will receive an "unknown command" error. Help text and the banner message are the only mitigation in Phase 1.
- **Phase 2 requires a shared implementation layer.** Without it, CLI subcommands and slash commands would duplicate rendering and data-access logic. This layer does not yet exist and will need to be designed to remain presentation-neutral (no Spectre.Console primitives in `Application`).
- **Phase 3 requires refactoring `LoginCommand`.** `LoginCommand` currently inherits `AsyncCommand` (a Spectre.Console.Cli type) and holds HTTP/OAuth logic inline. Extracting an `ILoginService` is a meaningful refactor that touches `Infrastructure.Persistence` (credential storage) and `Presentation.Console` (browser launch, callback server). It should not be undertaken until `/login` is a confirmed requirement.
- **New pre-session commands must avoid session singletons.** A developer adding a new CLI subcommand who is unaware of this rule may inadvertently resolve `ActiveProvider` from the container and get the unactivated default. The classification table in this ADR is the primary guard; a follow-up may add a compile-time annotation or a runtime assertion in `ActiveProvider` to make the failure loud.
