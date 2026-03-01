using System.ComponentModel;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BoydCode.Presentation.Console.Commands;

public sealed class ChatCommand : AsyncCommand<ChatCommand.Settings>
{
  private readonly IOptions<AppSettings> _appSettings;
  private readonly IExecutionEngineFactory _engineFactory;
  private readonly ActiveExecutionEngine _activeEngine;
  private readonly IProviderConfigStore _providerConfigStore;
  private readonly ActiveProvider _activeProvider;
  private readonly ActiveProject _activeProject;
  private readonly ActiveSession _activeSession;
  private readonly AgentOrchestrator _orchestrator;
  private readonly ProjectResolver _projectResolver;
  private readonly DirectoryResolver _directoryResolver;
  private readonly DirectoryGuard _directoryGuard;
  private readonly IPermissionEngine _permissionEngine;
  private readonly IUserInterface _ui;

  public sealed class Settings : CommandSettings
  {
    [CommandOption("--provider <PROVIDER>")]
    [Description("LLM provider: anthropic, gemini, openai, ollama")]
    public string? Provider { get; set; }

    [CommandOption("--model <MODEL>")]
    [Description("Model name to use")]
    public string? Model { get; set; }

    [CommandOption("--api-key <KEY>")]
    [Description("API key (or set via environment variable)")]
    public string? ApiKey { get; set; }

    [CommandOption("--resume <SESSION_ID>")]
    [Description("Resume a previous session")]
    public string? ResumeSessionId { get; set; }

    [CommandOption("--project <NAME>")]
    [Description("Project name to use for this session")]
    public string? Project { get; set; }
  }

  public ChatCommand(
      IOptions<AppSettings> appSettings,
      IExecutionEngineFactory engineFactory,
      ActiveExecutionEngine activeEngine,
      IProviderConfigStore providerConfigStore,
      ActiveProvider activeProvider,
      ActiveProject activeProject,
      ActiveSession activeSession,
      AgentOrchestrator orchestrator,
      ProjectResolver projectResolver,
      DirectoryResolver directoryResolver,
      DirectoryGuard directoryGuard,
      IPermissionEngine permissionEngine,
      IUserInterface ui)
  {
    _appSettings = appSettings;
    _engineFactory = engineFactory;
    _activeEngine = activeEngine;
    _providerConfigStore = providerConfigStore;
    _activeProvider = activeProvider;
    _activeProject = activeProject;
    _activeSession = activeSession;
    _orchestrator = orchestrator;
    _projectResolver = projectResolver;
    _directoryResolver = directoryResolver;
    _directoryGuard = directoryGuard;
    _permissionEngine = permissionEngine;
    _ui = ui;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
  {
    var workingDirectory = Directory.GetCurrentDirectory();

    // Resolve project
    var project = await _projectResolver.ResolveAsync(settings.Project, workingDirectory);
    _activeProject.Set(project.Name);
    var resolvedDirs = _directoryResolver.Resolve(project.Directories);
    _directoryGuard.ConfigureResolved(resolvedDirs);

    foreach (var dir in resolvedDirs.Where(d => !d.Exists))
    {
      _ui.RenderError($"Warning: Directory does not exist: {dir.Path}");
    }

    if (project.PermissionMode is not null || project.PermissionRules is not null)
    {
      _permissionEngine.Configure(project.PermissionMode, project.PermissionRules);
    }

    // Determine provider type: CLI > last-used > appsettings default
    LlmProviderType providerType;
    if (!string.IsNullOrEmpty(settings.Provider))
    {
      var (parsed, wasRecognized) = ParseProvider(settings.Provider);
      providerType = parsed;
      if (!wasRecognized)
      {
        _ui.RenderError(
            $"Unknown provider '{settings.Provider}'. Valid options: anthropic, gemini, openai, ollama. Defaulting to Gemini.");
      }
    }
    else
    {
      var lastUsed = await _providerConfigStore.GetLastUsedProviderAsync();
      providerType = lastUsed ?? _appSettings.Value.Llm.ProviderType;
    }

    // Load stored profile for this provider
    var profile = await _providerConfigStore.GetAsync(providerType);

    // Build LlmProviderConfig
    var llmConfig = new LlmProviderConfig
    {
      ProviderType = providerType,
      Model = settings.Model
            ?? profile?.DefaultModel
            ?? ProviderDefaults.DefaultModelFor(providerType),
      ApiKey = settings.ApiKey
            ?? profile?.ApiKey
            ?? GetApiKeyFromEnvironment(providerType)
            ?? _appSettings.Value.Llm.ApiKey,
      BaseUrl = _appSettings.Value.Llm.BaseUrl,
      MaxTokens = _appSettings.Value.Llm.MaxTokens,
    };

    // Resolve execution config early so it's available for banner and status line
    var executionConfig = project.DockerImage is not null || project.RequireContainer
        ? project.BuildExecutionConfig()
        : _appSettings.Value.Execution;

    // Activate provider if we have credentials (or Ollama which needs none)
    var isConfigured = false;
    if (!string.IsNullOrEmpty(llmConfig.ApiKey) || providerType == LlmProviderType.Ollama)
    {
      try
      {
        _activeProvider.Activate(llmConfig);
        isConfigured = true;
        var primaryBranch = resolvedDirs.FirstOrDefault(d => d.IsGitRepository)?.GitBranch;
        _ui.StatusLine = primaryBranch is not null
            ? $"{llmConfig.ProviderType} | {llmConfig.Model} | {project.Name} | {primaryBranch} | {executionConfig.Mode}"
            : $"{llmConfig.ProviderType} | {llmConfig.Model} | {project.Name} | {executionConfig.Mode}";
        await _providerConfigStore.SetLastUsedProviderAsync(providerType);
      }
      catch (InvalidOperationException ex)
      {
        _ui.RenderError($"Failed to initialize provider: {ex.Message}");
      }
    }

    RenderBanner(llmConfig, workingDirectory, isConfigured, project.Name, resolvedDirs, executionConfig.Mode, project.DockerImage);

    if (isConfigured)
    {
      _ui.RenderHint("Type a message to start, or /help for available commands.");
    }

    // Create execution engine via factory
    var engine = await _engineFactory.CreateAsync(executionConfig, resolvedDirs, project.Name);
    await _activeEngine.SetAsync(engine, executionConfig.Mode);

    // Create session
    var session = new Session(workingDirectory);
    session.ProjectName = project.Name;
    session.SystemPrompt = BuildSystemPrompt(project, resolvedDirs);
    _activeSession.Set(session);

    // Run the interactive loop
    try
    {
      await _orchestrator.RunSessionAsync(session);
    }
    catch (OperationCanceledException)
    {
      // User cancelled (Ctrl+C) — exit gracefully
      return (int)ExitCode.UserCancelled;
    }
    catch (Exception ex)
    {
      _ui.RenderError($"Fatal error: {ex.Message}\n  Suggestion: The session has ended. Please restart boydcode.");
      return (int)ExitCode.GeneralError;
    }

    return (int)ExitCode.Success;
  }

  private static (LlmProviderType Type, bool WasRecognized) ParseProvider(string provider) => provider.ToUpperInvariant() switch
  {
    "ANTHROPIC" or "CLAUDE" => (LlmProviderType.Anthropic, true),
    "GEMINI" or "GOOGLE" => (LlmProviderType.Gemini, true),
    "OPENAI" or "GPT" => (LlmProviderType.OpenAi, true),
    "OLLAMA" or "LOCAL" => (LlmProviderType.Ollama, true),
    _ => (LlmProviderType.Gemini, false),
  };

  private static string? GetApiKeyFromEnvironment(LlmProviderType provider) => provider switch
  {
    LlmProviderType.Anthropic => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
    LlmProviderType.Gemini => Environment.GetEnvironmentVariable("GEMINI_API_KEY")
        ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY"),
    LlmProviderType.OpenAi => Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    LlmProviderType.Ollama => null,
    _ => null,
  };

  private static void RenderBanner(LlmProviderConfig config, string workingDirectory, bool isConfigured, string projectName, IReadOnlyList<ResolvedDirectory>? resolvedDirectories, ExecutionMode executionMode, string? dockerImage)
  {
    // Detect short terminals and use compact banner
    var isCompact = false;
    try
    {
      isCompact = System.Console.WindowHeight < 30;
    }
    catch
    {
      // WindowHeight may throw on non-interactive terminals
    }

    AnsiConsole.WriteLine();

    if (isCompact)
    {
      AnsiConsole.MarkupLine("  [bold cyan]BOYD[/][bold blue]CODE[/]  [dim]v0.1  AI Coding Assistant[/]");
    }
    else
    {
      // BOYD ascii art with dim metadata floated right of rows 2-6.
      AnsiConsole.MarkupLine("  [bold cyan]██████╗  ██████╗ ██╗   ██╗██████╗[/]           [dim]Users:      1[/]");
      AnsiConsole.MarkupLine("  [bold cyan]██╔══██╗██╔═══██╗╚██╗ ██╔╝██╔══██╗[/]          [dim]Revenue:    $0[/]");
      AnsiConsole.MarkupLine("  [bold cyan]██████╔╝██║   ██║ ╚████╔╝ ██║  ██║[/]          [dim]Valuation:  $0,000,000,000[/]");
      AnsiConsole.MarkupLine("  [bold cyan]██╔══██╗██║   ██║  ╚██╔╝  ██║  ██║[/]          [dim]Commas:     tres[/]");
      AnsiConsole.MarkupLine("  [bold cyan]██████╔╝╚██████╔╝   ██║   ██████╔╝[/]          [dim]Status:     pre-unicorn[/]");
      AnsiConsole.MarkupLine("  [bold cyan]╚═════╝  ╚═════╝    ╚═╝   ╚═════╝[/]");

      // CODE ascii art indented beneath BOYD
      AnsiConsole.MarkupLine("  [bold blue]                 ██████╗  ██████╗ ██████╗ ███████╗[/]");
      AnsiConsole.MarkupLine("  [bold blue]                ██╔════╝ ██╔═══██╗██╔══██╗██╔════╝[/]");
      AnsiConsole.MarkupLine("  [bold blue]                ██║      ██║   ██║██║  ██║█████╗[/]");
      AnsiConsole.MarkupLine("  [bold blue]                ██║      ██║   ██║██║  ██║██╔══╝[/]");
      AnsiConsole.MarkupLine("  [bold blue]                ╚██████╗ ╚██████╔╝██████╔╝███████╗[/]");
      AnsiConsole.MarkupLine("  [bold blue]                 ╚═════╝  ╚═════╝ ╚═════╝ ╚══════╝[/]");

      // Version + tagline on one line
      AnsiConsole.MarkupLine("  [dim]v0.1  Artificial Intelligence, Personal Edition[/]");
    }

    AnsiConsole.WriteLine();

    // Thin rule separator
    AnsiConsole.Write(new Rule().RuleStyle("dim"));
    AnsiConsole.WriteLine();

    // Session info as a clean two-column grid (label / value / label / value)
    var grid = new Grid();
    grid.AddColumn(new GridColumn().PadLeft(2).PadRight(1).NoWrap());
    grid.AddColumn(new GridColumn().PadRight(4));
    grid.AddColumn(new GridColumn().PadRight(1).NoWrap());
    grid.AddColumn(new GridColumn());

    grid.AddRow(
        new Markup("[dim]Provider[/]"),
        new Markup($"[cyan]{Markup.Escape(config.ProviderType.ToString())}[/]"),
        new Markup("[dim]Project[/]"),
        new Markup($"[cyan]{Markup.Escape(projectName)}[/]"));

    grid.AddRow(
        new Markup("[dim]Model[/]"),
        new Markup($"[cyan]{Markup.Escape(config.Model)}[/]"),
        new Markup("[dim]Engine[/]"),
        new Markup($"[cyan]{Markup.Escape(executionMode.ToString())}[/]"));

    // cwd gets full width (value spans remaining columns)
    grid.AddRow(
        new Markup("[dim]cwd[/]"),
        new Markup($"[cyan]{Markup.Escape(workingDirectory)}[/]"),
        new Markup(""),
        new Markup(""));

    if (dockerImage is not null)
    {
      grid.AddRow(
          new Markup("[dim]Docker[/]"),
          new Markup($"[cyan]{Markup.Escape(dockerImage)}[/]"),
          new Markup(""),
          new Markup(""));
    }

    if (resolvedDirectories is not null)
    {
      foreach (var dir in resolvedDirectories.Where(d => d.IsGitRepository))
      {
        var branchInfo = dir.GitBranch is not null ? $" ({dir.GitBranch})" : "";
        grid.AddRow(
            new Markup("[dim]Git[/]"),
            new Markup($"[cyan]{Markup.Escape(dir.RepoRoot ?? dir.Path)}{Markup.Escape(branchInfo)}[/]"),
            new Markup(""),
            new Markup(""));
      }
    }

    AnsiConsole.Write(grid);
    AnsiConsole.WriteLine();

    // Status footer
    if (isConfigured)
    {
      var engineNote = executionMode == ExecutionMode.Container
          ? "Commands execute inside a Docker container."
          : "Commands run in a constrained PowerShell runspace.";
      AnsiConsole.MarkupLine($"  [green]Ready[/]  [dim]{engineNote}[/]");
    }
    else
    {
      AnsiConsole.MarkupLine("  [yellow bold]Not configured[/]");
      AnsiConsole.MarkupLine("  [dim]Use[/] [bold]/provider setup[/] [dim]to configure an API key, or pass[/] [bold]--api-key[/][dim].[/]");
    }

    AnsiConsole.WriteLine();
  }

  internal static string BuildSystemPrompt(Project project, IReadOnlyList<ResolvedDirectory> resolvedDirs)
  {
    var customPrompt = project.SystemPrompt ?? Project.DefaultSystemPrompt;
    var userPrompt = $"You are working on project '{project.Name}'.\n\n{customPrompt}";

    var dirContext = BuildDirectoryContext(resolvedDirs);
    if (dirContext is not null)
    {
      userPrompt += $"\n\n{dirContext}";
    }

    return userPrompt;
  }

  private static string? BuildDirectoryContext(IReadOnlyList<ResolvedDirectory> directories)
  {
    if (directories.Count == 0)
    {
      return null;
    }

    var lines = new List<string> { "## Working Directories" };
    foreach (var dir in directories)
    {
      var parts = new List<string> { $"- `{dir.Path}` ({dir.AccessLevel})" };
      if (dir.IsGitRepository)
      {
        var details = new List<string> { "git repo" };
        if (dir.GitBranch is not null)
        {
          details.Add($"branch: {dir.GitBranch}");
        }
        parts.Add($"  [{string.Join(", ", details)}]");
      }
      else if (!dir.Exists)
      {
        parts.Add("  [does not exist]");
      }
      lines.AddRange(parts);
    }
    return string.Join("\n", lines);
  }
}
