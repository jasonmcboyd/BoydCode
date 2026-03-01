using System.ComponentModel;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Presentation.Console.Renderables;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BoydCode.Presentation.Console.Commands;

public sealed class ChatCommand : AsyncCommand<ChatCommand.Settings>
{
  private static readonly string[] ToolNames = ["Shell"];

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
  private readonly IUserInterface _ui;
  private readonly ISessionRepository _sessionRepository;
  private readonly IConversationLogger _conversationLogger;
  private readonly IAgentRegistry _agentRegistry;
  private readonly ISettingsProvider _settingsProvider;

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

    [CommandOption("--accessible")]
    [Description("Enable accessible mode (reduced animation, text-only indicators)")]
    public bool Accessible { get; set; }
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
      IUserInterface ui,
      ISessionRepository sessionRepository,
      IConversationLogger conversationLogger,
      IAgentRegistry agentRegistry,
      ISettingsProvider settingsProvider)
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
    _ui = ui;
    _sessionRepository = sessionRepository;
    _conversationLogger = conversationLogger;
    _agentRegistry = agentRegistry;
    _settingsProvider = settingsProvider;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
  {
    if (settings.Accessible)
    {
      AccessibilityConfig.Accessible = true;
    }

    var workingDirectory = Directory.GetCurrentDirectory();

    // Resolve project
    var project = await _projectResolver.ResolveAsync(settings.Project, workingDirectory);
    _activeProject.Set(project.Name);
    var resolvedDirs = _directoryResolver.Resolve(project.Directories);
    _directoryGuard.ConfigureResolved(resolvedDirs);

    foreach (var dir in resolvedDirs.Where(d => !d.Exists))
    {
      AnsiConsole.MarkupLine($"[yellow]![/] [yellow]Warning:[/] Directory does not exist: {Markup.Escape(dir.Path)}");
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

    // Create execution engine via factory
    var engine = await _engineFactory.CreateAsync(executionConfig, resolvedDirs, project.Name);
    await _activeEngine.SetAsync(engine, executionConfig.Mode);

    // Initialize agent registry (discovers user + project agent definitions)
    await _agentRegistry.InitializeAsync(workingDirectory);

    // Load BOYDCODE.md extensions from global + project directories
    var promptExtensions = _settingsProvider.GetSystemPromptExtensions(workingDirectory);

    // Create or resume session
    Session session;
    if (!string.IsNullOrEmpty(settings.ResumeSessionId))
    {
      var loaded = await _sessionRepository.LoadAsync(settings.ResumeSessionId);
      if (loaded is null)
      {
        _ui.RenderError($"Session '{settings.ResumeSessionId}' not found.");
        return (int)ExitCode.GeneralError;
      }

      session = loaded;
      session.WorkingDirectory = workingDirectory;
      session.ProjectName = project.Name;
      session.SystemPrompt = BuildSystemPrompt(project, resolvedDirs, _activeEngine.Engine?.PathMappings, promptExtensions);
      session.PromptExtensions = promptExtensions;
      _activeSession.Set(session);

      await _conversationLogger.InitializeAsync(session.Id);
      await _conversationLogger.LogSessionResumeAsync(
          session.Conversation.Messages.Count, session.CreatedAt);
      var metaPromptText = MetaPrompt.Build(_activeEngine.Mode, _activeEngine.Engine?.GetAvailableCommands() ?? []);
      await _conversationLogger.LogLlmContextAsync(
          session.SystemPrompt ?? "", metaPromptText,
          ToolNames, llmConfig.Model, llmConfig.ProviderType);
    }
    else
    {
      session = new Session(workingDirectory);
      session.ProjectName = project.Name;
      session.SystemPrompt = BuildSystemPrompt(project, resolvedDirs, _activeEngine.Engine?.PathMappings, promptExtensions);
      session.PromptExtensions = promptExtensions;
      _activeSession.Set(session);

      await _conversationLogger.InitializeAsync(session.Id);
      await _conversationLogger.LogSessionStartAsync(
          llmConfig.ProviderType, llmConfig.Model, project.Name,
          executionConfig.Mode, workingDirectory);
      var metaPromptText = MetaPrompt.Build(_activeEngine.Mode, _activeEngine.Engine?.GetAvailableCommands() ?? []);
      await _conversationLogger.LogLlmContextAsync(
          session.SystemPrompt ?? "", metaPromptText,
          ToolNames, llmConfig.Model, llmConfig.ProviderType);
    }

    // Render the banner BEFORE layout activation so it writes directly to the terminal
    int termHeight;
    try { termHeight = System.Console.WindowHeight; } catch { termHeight = 24; }
    var termWidth = AnsiConsole.Profile.Width;

    var bannerData = new BannerData
    {
      ProviderName = llmConfig.ProviderType.ToString(),
      ModelName = llmConfig.Model,
      ProjectName = project.Name,
      ExecutionMode = executionConfig.Mode.ToString(),
      WorkingDirectory = workingDirectory,
      Version = GetAssemblyVersion(),
      DockerImage = project.DockerImage,
      GitRepositories = resolvedDirs
        .Where(d => d.IsGitRepository)
        .Select(d => new BannerData.GitInfo(d.RepoRoot ?? d.Path, d.GitBranch))
        .ToList(),
      IsConfigured = isConfigured,
      TerminalHeight = termHeight,
      TerminalWidth = termWidth,
      Accessible = AccessibilityConfig.Accessible,
      SupportsUnicode = AnsiConsole.Profile.Capabilities.Unicode,
      IsResumedSession = !string.IsNullOrEmpty(settings.ResumeSessionId),
      ResumeSessionId = !string.IsNullOrEmpty(settings.ResumeSessionId) ? session.Id : null,
      ResumeMessageCount = !string.IsNullOrEmpty(settings.ResumeSessionId) ? session.Conversation.Messages.Count : 0,
      ResumeTimestamp = !string.IsNullOrEmpty(settings.ResumeSessionId) ? session.CreatedAt : null,
    };

    var bannerRenderable = BannerRenderable.Build(bannerData);

    // Write banner to stdout scrollback (part of terminal history)
    AnsiConsole.Write(bannerRenderable);
    AnsiConsole.WriteLine();

    // Run the interactive loop
    _ui.ActivateLayout();

    // Also add banner to TUI content region so it's visible after layout takes over.
    // Without this, the TUI immediately fills the screen and pushes the banner above
    // the visible area — the user would have to scroll up to see it.
    SpectreHelpers.OutputRenderable(bannerRenderable);

    try
    {
      await _orchestrator.RunSessionAsync(session);
      await _conversationLogger.LogSessionEndAsync("quit");
    }
    catch (OperationCanceledException)
    {
      await _conversationLogger.LogSessionEndAsync("cancel");
      // User cancelled (Ctrl+C) — exit gracefully
      return (int)ExitCode.UserCancelled;
    }
    catch (Exception ex)
    {
      await _conversationLogger.LogSessionEndAsync("error");
      _ui.RenderError($"Fatal error: {ex.Message}\n  Suggestion: The session has ended. Please restart boydcode.");
      return (int)ExitCode.GeneralError;
    }
    finally
    {
      _ui.DeactivateLayout();
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

  private static string GetAssemblyVersion()
  {
    var assembly = System.Reflection.Assembly.GetEntryAssembly();
    var version = assembly?.GetName().Version;
    if (version is null) return "dev";
    return version.Build > 0
      ? $"{version.Major}.{version.Minor}.{version.Build}"
      : $"{version.Major}.{version.Minor}";
  }

  private static string? GetApiKeyFromEnvironment(LlmProviderType provider) => provider switch
  {
    LlmProviderType.Anthropic => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
    LlmProviderType.Gemini => Environment.GetEnvironmentVariable("GEMINI_API_KEY")
        ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY"),
    LlmProviderType.OpenAi => Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    LlmProviderType.Ollama => null,
    _ => null,
  };

  internal static string BuildSystemPrompt(
      Project project,
      IReadOnlyList<ResolvedDirectory> resolvedDirs,
      IReadOnlyDictionary<string, string>? pathMappings = null,
      string? promptExtensions = null)
  {
    var customPrompt = project.SystemPrompt ?? Project.DefaultSystemPrompt;
    var userPrompt = $"You are working on project '{project.Name}'.\n\n{customPrompt}";

    var dirContext = BuildDirectoryContext(resolvedDirs, pathMappings);
    if (dirContext is not null)
    {
      userPrompt += $"\n\n{dirContext}";
    }

    if (promptExtensions is not null)
    {
      userPrompt += $"\n\n---\n\n{promptExtensions}";
    }

    return userPrompt;
  }

  private static string? BuildDirectoryContext(
      IReadOnlyList<ResolvedDirectory> directories,
      IReadOnlyDictionary<string, string>? pathMappings = null)
  {
    if (directories.Count == 0)
    {
      return null;
    }

    var lines = new List<string> { "## Working Directories" };
    foreach (var dir in directories)
    {
      var displayPath = pathMappings is not null
          && pathMappings.TryGetValue(dir.Path, out var containerPath)
          ? containerPath
          : dir.Path;
      var parts = new List<string> { $"- `{displayPath}` ({dir.AccessLevel})" };
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
