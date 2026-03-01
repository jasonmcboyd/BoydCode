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
      AnsiConsole.MarkupLine($"[yellow]Warning: Directory does not exist: {Markup.Escape(dir.Path)}[/]");
    }

    if (project.PermissionMode is not null || project.PermissionRules is not null)
    {
      _permissionEngine.Configure(project.PermissionMode, project.PermissionRules);
    }

    // Determine provider type: CLI > last-used > appsettings default
    LlmProviderType providerType;
    if (!string.IsNullOrEmpty(settings.Provider))
    {
      providerType = ParseProvider(settings.Provider);
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
        AnsiConsole.MarkupLine($"[yellow]Failed to initialize provider: {Markup.Escape(ex.Message)}[/]");
      }
    }

    RenderBanner(llmConfig, workingDirectory, isConfigured, project.Name, resolvedDirs, executionConfig.Mode, project.DockerImage);

    // Create execution engine via factory
    var engine = await _engineFactory.CreateAsync(executionConfig, resolvedDirs, project.Name);
    await _activeEngine.SetAsync(engine, executionConfig.Mode);

    // Create session
    var session = new Session(workingDirectory);
    session.ProjectName = project.Name;

    var customPrompt = project.SystemPrompt ?? Project.DefaultSystemPrompt;
    var userPrompt = $"You are working on project '{project.Name}'.\n\n{customPrompt}";

    var dirContext = BuildDirectoryContext(resolvedDirs);
    if (dirContext is not null)
    {
      userPrompt += $"\n\n{dirContext}";
    }

    session.SystemPrompt = userPrompt;

    // Run the interactive loop
    try
    {
      await _orchestrator.RunSessionAsync(session);
    }
    catch (OperationCanceledException)
    {
      // User cancelled (Ctrl+C) — exit gracefully
    }
    catch (Exception ex)
    {
      AnsiConsole.MarkupLine($"[red]Fatal error: {Markup.Escape(ex.Message)}[/]");
      AnsiConsole.MarkupLine("[dim]The session has ended. Please restart boydcode.[/]");
      return 1;
    }

    return 0;
  }

  private static LlmProviderType ParseProvider(string provider) => provider.ToUpperInvariant() switch
  {
    "ANTHROPIC" or "CLAUDE" => LlmProviderType.Anthropic,
    "GEMINI" or "GOOGLE" => LlmProviderType.Gemini,
    "OPENAI" or "GPT" => LlmProviderType.OpenAi,
    "OLLAMA" or "LOCAL" => LlmProviderType.Ollama,
    _ => LlmProviderType.Gemini,
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
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold cyan]██████╗  ██████╗ ██╗   ██╗██████╗[/]                              [dim]Users:         1[/]");
    AnsiConsole.MarkupLine("[bold cyan]██╔══██╗██╔═══██╗╚██╗ ██╔╝██╔══██╗[/]                             [dim]Revenue:       $0[/]");
    AnsiConsole.MarkupLine("[bold cyan]██████╔╝██║   ██║ ╚████╔╝ ██║  ██║[/]                             [dim]Valuation:     $0,000,000,000[/]");
    AnsiConsole.MarkupLine("[bold cyan]██╔══██╗██║   ██║  ╚██╔╝  ██║  ██║[/]                             [dim]Commas:        tres[/]");
    AnsiConsole.MarkupLine("[bold cyan]██████╔╝╚██████╔╝   ██║   ██████╔╝[/]                             [dim]Status:        pre-unicorn[/]");
    AnsiConsole.MarkupLine("[bold cyan]╚═════╝  ╚═════╝    ╚═╝   ╚═════╝[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold blue]                 ██████╗  ██████╗ ██████╗ ███████╗[/]");
    AnsiConsole.MarkupLine("[bold blue]                ██╔════╝ ██╔═══██╗██╔══██╗██╔════╝[/]");
    AnsiConsole.MarkupLine("[bold blue]                ██║      ██║   ██║██║  ██║█████╗[/]");
    AnsiConsole.MarkupLine("[bold blue]                ██║      ██║   ██║██║  ██║██╔══╝[/]");
    AnsiConsole.MarkupLine("[bold blue]                ╚██████╗ ╚██████╔╝██████╔╝███████╗[/]");
    AnsiConsole.MarkupLine("[bold blue]                 ╚═════╝  ╚═════╝ ╚═════╝ ╚══════╝[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[dim]Boyd Code™ v0.1[/]");
    AnsiConsole.MarkupLine("[dim italic]Artificial Intelligence, Personal Edition[/]");
    AnsiConsole.WriteLine();

    var lines = new List<string>
        {
            $"  Provider [cyan]{Markup.Escape(config.ProviderType.ToString())}[/]",
            $"  Model    [cyan]{Markup.Escape(config.Model)}[/]",
            $"  cwd      [cyan]{Markup.Escape(workingDirectory)}[/]",
            $"  Project  [cyan]{Markup.Escape(projectName)}[/]",
            $"  Engine   [cyan]{Markup.Escape(executionMode.ToString())}[/]",
        };

    if (dockerImage is not null)
    {
      lines.Add($"  Docker   [cyan]{Markup.Escape(dockerImage)}[/]");
    }

    if (resolvedDirectories is not null)
    {
      foreach (var dir in resolvedDirectories.Where(d => d.IsGitRepository))
      {
        var branchInfo = dir.GitBranch is not null ? $" ({dir.GitBranch})" : "";
        lines.Add($"  Git      [cyan]{Markup.Escape(dir.RepoRoot ?? dir.Path)}{Markup.Escape(branchInfo)}[/]");
      }
    }

    if (isConfigured)
    {
      var engineNote = executionMode == ExecutionMode.Container
          ? "[dim]Commands execute inside a Docker container.[/]"
          : "[dim]Commands run in a constrained PowerShell runspace.[/]";
      lines.Add($"  Status   [green]Ready[/]");
      lines.Add("");
      lines.Add($"  [dim]Type[/] /quit [dim]or[/] exit [dim]to exit.[/] {engineNote}");
    }
    else
    {
      lines.Add("");
      lines.Add("  [yellow bold]Not configured[/]");
      lines.Add("  [dim]Use[/] [bold]/provider setup[/] [dim]to configure an API key, or pass[/] [bold]--api-key[/][dim].[/]");
    }

    var panel = new Panel(new Markup(string.Join("\n", lines)))
        .Border(BoxBorder.Rounded)
        .BorderColor(Color.Blue)
        .Padding(1, 0, 1, 0);

    AnsiConsole.Write(panel);
    AnsiConsole.WriteLine();
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
