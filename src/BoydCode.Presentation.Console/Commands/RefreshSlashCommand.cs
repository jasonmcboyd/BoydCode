using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.SlashCommands;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed class RefreshSlashCommand : ISlashCommand
{
  private static readonly string[] ToolNames = ["Shell"];

  private readonly IProjectRepository _projectRepository;
  private readonly ActiveProject _activeProject;
  private readonly ActiveSession _activeSession;
  private readonly ActiveProvider _activeProvider;
  private readonly ActiveExecutionEngine _activeEngine;
  private readonly DirectoryResolver _directoryResolver;
  private readonly DirectoryGuard _directoryGuard;
  private readonly IExecutionEngineFactory _engineFactory;
  private readonly IUserInterface _ui;
  private readonly AppSettings _appSettings;
  private readonly IConversationLogger _conversationLogger;

  public RefreshSlashCommand(
      IProjectRepository projectRepository,
      ActiveProject activeProject,
      ActiveSession activeSession,
      ActiveProvider activeProvider,
      ActiveExecutionEngine activeEngine,
      DirectoryResolver directoryResolver,
      DirectoryGuard directoryGuard,
      IExecutionEngineFactory engineFactory,
      IUserInterface ui,
      IOptions<AppSettings> appSettings,
      IConversationLogger conversationLogger)
  {
    _projectRepository = projectRepository;
    _activeProject = activeProject;
    _activeSession = activeSession;
    _activeProvider = activeProvider;
    _activeEngine = activeEngine;
    _directoryResolver = directoryResolver;
    _directoryGuard = directoryGuard;
    _engineFactory = engineFactory;
    _ui = ui;
    _appSettings = appSettings.Value;
    _conversationLogger = conversationLogger;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/refresh",
      "Refresh session context (project, directories, permissions, engine)",
      []);

  public async Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    var trimmed = input.Trim();
    if (!trimmed.Equals("/refresh", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    await RefreshAsync(ct);
    return true;
  }

  private async Task RefreshAsync(CancellationToken ct)
  {
    // 1. Guard: session and project must exist
    var session = _activeSession.Session;
    var projectName = _activeProject.Name;

    if (session is null || projectName is null)
    {
      SpectreHelpers.Error("No active session. Nothing to refresh.");
      return;
    }

    // 2. Capture "before" snapshot
    var beforeBranch = _directoryGuard.ResolvedDirectories
        .FirstOrDefault(d => d.IsGitRepository)?.GitBranch;
    var beforeDirCount = _directoryGuard.ResolvedDirectories.Count;
    var beforeMode = _activeEngine.Mode;
    var beforePromptLength = session.SystemPrompt?.Length ?? 0;

    // 3. Reload project from repository
    var project = await _projectRepository.LoadAsync(projectName, ct);
    if (project is null)
    {
      SpectreHelpers.Error($"Project '{projectName}' not found. It may have been deleted.");
      return;
    }

    // 4. Re-resolve directories + warn on missing
    var resolvedDirs = _directoryResolver.Resolve(project.Directories);
    foreach (var dir in resolvedDirs.Where(d => !d.Exists))
    {
      SpectreHelpers.Warning($"Directory does not exist: {dir.Path}");
    }

    // 5. Reconfigure directory guard
    _directoryGuard.ConfigureResolved(resolvedDirs);

    // 6. Rebuild + assign session system prompt
    session.SystemPrompt = ChatCommand.BuildSystemPrompt(project, resolvedDirs);

    // 7. Build ExecutionConfig (same logic as ChatCommand)
    var executionConfig = project.DockerImage is not null || project.RequireContainer
        ? project.BuildExecutionConfig()
        : _appSettings.Execution;

    // 8. Create new engine via factory, set on ActiveExecutionEngine
    var engineRefreshed = false;
    try
    {
      var engine = await _engineFactory.CreateAsync(executionConfig, resolvedDirs, project.Name, ct);
      await _activeEngine.SetAsync(engine, executionConfig.Mode);
      engineRefreshed = true;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      SpectreHelpers.Warning($"Engine refresh failed (keeping previous): {ex.Message}");
    }

    // 9. Rebuild status line
    if (_activeProvider.Config is not null)
    {
      var primaryBranch = resolvedDirs.FirstOrDefault(d => d.IsGitRepository)?.GitBranch;
      _ui.StatusLine = primaryBranch is not null
          ? $"{_activeProvider.Config.ProviderType} | {_activeProvider.Config.Model} | {project.Name} | {primaryBranch} | {executionConfig.Mode}"
          : $"{_activeProvider.Config.ProviderType} | {_activeProvider.Config.Model} | {project.Name} | {executionConfig.Mode}";
    }

    // 10. Render summary with before/after diff indicators
    var afterBranch = resolvedDirs.FirstOrDefault(d => d.IsGitRepository)?.GitBranch;
    var afterDirCount = resolvedDirs.Count;
    var afterMode = executionConfig.Mode;
    var afterPromptLength = session.SystemPrompt?.Length ?? 0;
    var gitDirCount = resolvedDirs.Count(d => d.IsGitRepository);

    AnsiConsole.WriteLine();
    SpectreHelpers.Success("Session context refreshed.");
    _ui.StaleSettingsWarning = null;

    var metaPromptText = MetaPrompt.Build(_activeEngine.Mode, _activeEngine.Engine?.GetAvailableCommands() ?? []);
    await _conversationLogger.LogLlmContextAsync(
        session.SystemPrompt ?? "", metaPromptText,
        ToolNames,
        _activeProvider.Config?.Model ?? "unknown",
        _activeProvider.Config?.ProviderType ?? LlmProviderType.Gemini,
        ct);

    AnsiConsole.WriteLine();

    // Directories
    var dirLabel = $"{afterDirCount} ({gitDirCount} git)";
    var dirChanged = afterDirCount != beforeDirCount;
    RenderSummaryLine("Directories", dirLabel, dirChanged);

    // Git branch
    var branchDisplay = afterBranch ?? "none";
    var branchChanged = !string.Equals(beforeBranch, afterBranch, StringComparison.Ordinal);
    if (branchChanged && beforeBranch is not null)
    {
      RenderSummaryLine("Git branch", $"{branchDisplay}  [dim](was: {Markup.Escape(beforeBranch)})[/]", true);
    }
    else
    {
      RenderSummaryLine("Git branch", branchDisplay, false);
    }

    // Engine
    var engineLabel = engineRefreshed
        ? $"{afterMode} (refreshed)"
        : $"{afterMode} (kept previous)";
    var engineChanged = engineRefreshed || afterMode != beforeMode;
    RenderSummaryLine("Engine", engineLabel, engineChanged);

    // System prompt
    var promptChanged = afterPromptLength != beforePromptLength;
    var promptLabel = promptChanged
        ? $"updated ({beforePromptLength:N0} → {afterPromptLength:N0} chars)"
        : $"unchanged ({afterPromptLength:N0} chars)";
    RenderSummaryLine("System prompt", promptLabel, promptChanged);

    AnsiConsole.WriteLine();
  }

  private static void RenderSummaryLine(string label, string value, bool changed)
  {
    var style = changed ? "bold" : "dim";
    AnsiConsole.MarkupLine($"    [dim]{Markup.Escape(label),-16}[/][{style}]{value}[/]");
  }
}
