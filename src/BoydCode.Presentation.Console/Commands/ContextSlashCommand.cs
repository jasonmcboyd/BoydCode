using System.Globalization;
using System.Text;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.SlashCommands;
using BoydCode.Domain.Tools;
using BoydCode.Presentation.Console.Terminal;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed class ContextSlashCommand : ISlashCommand
{
  private static readonly string[] ToolNames = ["Shell"];
  private static readonly string[] SummarizeChoices = ["Apply", "Fork conversation", "Revise", "Cancel"];

  private readonly ActiveSession _activeSession;
  private readonly ActiveProvider _activeProvider;
  private readonly AppSettings _settings;
  private readonly ActiveExecutionEngine _activeEngine;
  private readonly IConversationLogger _conversationLogger;
  private readonly IProjectRepository _projectRepository;
  private readonly ISessionRepository _sessionRepository;
  private readonly IContextCompactor _contextCompactor;
  private readonly ActiveProject _activeProject;
  private readonly DirectoryResolver _directoryResolver;
  private readonly DirectoryGuard _directoryGuard;
  private readonly IExecutionEngineFactory _engineFactory;
  private readonly IUserInterface _ui;

  public ContextSlashCommand(
      ActiveSession activeSession,
      ActiveProvider activeProvider,
      IOptions<AppSettings> settings,
      ActiveExecutionEngine activeEngine,
      IConversationLogger conversationLogger,
      IProjectRepository projectRepository,
      ISessionRepository sessionRepository,
      IContextCompactor contextCompactor,
      ActiveProject activeProject,
      DirectoryResolver directoryResolver,
      DirectoryGuard directoryGuard,
      IExecutionEngineFactory engineFactory,
      IUserInterface ui)
  {
    _activeSession = activeSession;
    _activeProvider = activeProvider;
    _settings = settings.Value;
    _activeEngine = activeEngine;
    _conversationLogger = conversationLogger;
    _projectRepository = projectRepository;
    _sessionRepository = sessionRepository;
    _contextCompactor = contextCompactor;
    _activeProject = activeProject;
    _directoryResolver = directoryResolver;
    _directoryGuard = directoryGuard;
    _engineFactory = engineFactory;
    _ui = ui;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/context",
      "View and manage conversation context",
      [
          new("show", "Show detailed context breakdown with chart"),
          new("summarize [topic]", "Summarize conversation using LLM"),
          new("prune", "Prune older topics to free context space"),
          new("refresh", "Refresh session context (project, directories, engine)"),
      ]);

  public async Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (tokens.Length == 0 || !tokens[0].Equals("/context", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    var subcommand = tokens.Length > 1 ? tokens[1].ToLowerInvariant() : string.Empty;

    switch (subcommand)
    {
      case "show":
        HandleShow();
        break;
      case "summarize":
        var focusTopic = tokens.Length > 2
            ? string.Join(' ', tokens.Skip(2))
            : null;
        await HandleSummarizeAsync(focusTopic, ct);
        break;
      case "prune":
        await HandlePruneAsync(ct);
        break;
      case "refresh":
        await HandleRefreshAsync(ct);
        break;
      case "":
      default:
        SpectreHelpers.Usage("/context show|summarize|prune|refresh");
        break;
    }

    return true;
  }

  // ──────────────────────────────────────────────
  //  SHOW (detailed breakdown with chart)
  // ──────────────────────────────────────────────

  private void HandleShow()
  {
    var session = _activeSession.Session;
    if (session is null)
    {
      SpectreHelpers.Error("No active session.");
      return;
    }

    var conversation = session.Conversation;
    var shellTool = AgentOrchestrator.ShellToolDefinition;

    // ── Compute token categories ──────────────────

    var metaPromptTokens = EstimateStringTokens(MetaPrompt.Build(_activeEngine.Mode, _activeEngine.Engine?.GetAvailableCommands() ?? []));
    var sessionPromptTokens = EstimateStringTokens(session.SystemPrompt);
    var systemPromptTokens = metaPromptTokens + sessionPromptTokens;

    var toolTokensTotal = EstimateToolDefinitionTokens(shellTool);

    var messageBreakdown = ComputeMessageBreakdown(conversation.Messages);
    var messageTokensTotal = messageBreakdown.UserTextTokens
        + messageBreakdown.AssistantTextTokens
        + messageBreakdown.ToolCallTokens
        + messageBreakdown.ToolResultTokens;

    var contextLimit = _activeProvider.Provider?.Capabilities.MaxContextWindowTokens > 0
        ? _activeProvider.Provider!.Capabilities.MaxContextWindowTokens
        : _settings.ContextWindowTokenLimit;
    var compactThreshold = contextLimit * _settings.CompactionThresholdPercent / 100;
    var bufferTokens = contextLimit - compactThreshold;

    var totalUsed = systemPromptTokens + toolTokensTotal + messageTokensTotal;
    var freeTokens = Math.Max(0, contextLimit - totalUsed - bufferTokens);

    var usagePercent = contextLimit > 0
        ? (double)totalUsed / contextLimit * 100
        : 0;

    // ── Header line ───────────────────────────────

    var providerName = _activeProvider.Config?.ProviderType.ToString() ?? "unknown";
    var modelName = _activeProvider.Config?.Model ?? "unknown";
    var usageColor = usagePercent switch
    {
      < 50 => "green",
      < 80 => "yellow",
      _ => "red",
    };

    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup(string.Format(
        CultureInfo.InvariantCulture,
        "  [bold]{0}[/] [dim]\u00b7[/] [bold]{1}[/] [dim]\u00b7[/] [{2}]{3}/{4} tokens ({5})[/]",
        Markup.Escape(providerName),
        Markup.Escape(modelName),
        usageColor,
        SpectreHelpers.FormatCompact(totalUsed),
        SpectreHelpers.FormatCompact(contextLimit),
        SpectreHelpers.FormatPercent(usagePercent)));
    SpectreHelpers.OutputLine();

    // ── Stacked bar ───────────────────────────────

    RenderStackedBar(systemPromptTokens, toolTokensTotal, messageTokensTotal, freeTokens, bufferTokens, contextLimit);
    SpectreHelpers.OutputLine();

    // ── Legend ─────────────────────────────────────

    SpectreHelpers.OutputMarkup("  [bold]Estimated usage by category[/]");
    RenderLegend("\u25a0", "blue", "System prompt", systemPromptTokens, contextLimit);
    RenderLegend("\u25a0", "mediumpurple1", "Tools", toolTokensTotal, contextLimit);
    RenderLegend("\u25a0", "green", "Messages", messageTokensTotal, contextLimit);
    RenderLegend("\u25a0", "grey", "Free space", freeTokens, contextLimit);
    RenderLegend("\u25a0", "darkorange", "Compact buffer", bufferTokens, contextLimit);
    SpectreHelpers.OutputLine();

    // ── System prompt breakdown ───────────────────

    SpectreHelpers.OutputMarkup(string.Format(
        CultureInfo.InvariantCulture,
        "  [blue bold]System prompt[/] [dim]\u00b7[/] {0} tokens",
        SpectreHelpers.FormatCompact(systemPromptTokens)));
    RenderTreeLine(false, "Meta prompt", string.Format(CultureInfo.InvariantCulture, "{0} tokens", SpectreHelpers.FormatCompact(metaPromptTokens)));
    RenderTreeLine(true, "Session prompt", string.Format(CultureInfo.InvariantCulture, "{0} tokens", SpectreHelpers.FormatCompact(sessionPromptTokens)));
    SpectreHelpers.OutputLine();

    // ── Message breakdown ─────────────────────────

    var totalMessageCount = messageBreakdown.UserTextCount
        + messageBreakdown.AssistantTextCount
        + messageBreakdown.ToolCallCount
        + messageBreakdown.ToolResultCount;

    SpectreHelpers.OutputMarkup(string.Format(
        CultureInfo.InvariantCulture,
        "  [green bold]Messages[/] [dim]\u00b7[/] {0} messages, {1} tokens",
        totalMessageCount.ToString(CultureInfo.InvariantCulture),
        SpectreHelpers.FormatCompact(messageTokensTotal)));
    RenderTreeLine(false, "User text", string.Format(
        CultureInfo.InvariantCulture, "{0} messages, {1} tokens",
        messageBreakdown.UserTextCount, SpectreHelpers.FormatCompact(messageBreakdown.UserTextTokens)));
    RenderTreeLine(false, "Assistant text", string.Format(
        CultureInfo.InvariantCulture, "{0} messages, {1} tokens",
        messageBreakdown.AssistantTextCount, SpectreHelpers.FormatCompact(messageBreakdown.AssistantTextTokens)));
    RenderTreeLine(false, "Tool calls", string.Format(
        CultureInfo.InvariantCulture, "{0} calls, {1} tokens",
        messageBreakdown.ToolCallCount, SpectreHelpers.FormatCompact(messageBreakdown.ToolCallTokens)));
    RenderTreeLine(true, "Tool results", string.Format(
        CultureInfo.InvariantCulture, "{0} results, {1} tokens",
        messageBreakdown.ToolResultCount, SpectreHelpers.FormatCompact(messageBreakdown.ToolResultTokens)));
    SpectreHelpers.OutputLine();

    // ── Tool inventory ────────────────────────────

    SpectreHelpers.OutputMarkup(string.Format(
        CultureInfo.InvariantCulture,
        "  [mediumpurple1 bold]Tools[/] [dim]\u00b7[/] 1 tool, {0} tokens",
        SpectreHelpers.FormatCompact(toolTokensTotal)));
    RenderTreeLine(true, shellTool.Name, string.Format(CultureInfo.InvariantCulture, "{0} tokens", SpectreHelpers.FormatCompact(toolTokensTotal)));

    SpectreHelpers.OutputLine();
  }

  // ──────────────────────────────────────────────
  //  RENDERING HELPERS
  // ──────────────────────────────────────────────

  private static void RenderStackedBar(
      int systemTokens, int toolTokens, int messageTokens,
      int freeTokens, int bufferTokens, int contextLimit)
  {
    const int barWidth = 72;
    if (contextLimit <= 0) return;

    var segments = new (int tokens, string color)[]
    {
      (systemTokens, "blue"),
      (toolTokens, "mediumpurple1"),
      (messageTokens, "green"),
      (freeTokens, "grey"),
      (bufferTokens, "darkorange"),
    };

    // Calculate character widths: minimum 1 char for non-zero categories
    var charWidths = new int[segments.Length];
    var nonZeroCount = segments.Count(s => s.tokens > 0);
    var reserved = nonZeroCount; // 1 char minimum each
    var remaining = barWidth - reserved;

    // First pass: assign proportional widths for what's left after minimums
    var totalTokens = segments.Sum(s => s.tokens);
    if (totalTokens > 0 && remaining > 0)
    {
      for (var i = 0; i < segments.Length; i++)
      {
        if (segments[i].tokens > 0)
        {
          charWidths[i] = 1 + (int)((long)segments[i].tokens * remaining / totalTokens);
        }
      }
    }
    else
    {
      for (var i = 0; i < segments.Length; i++)
      {
        charWidths[i] = segments[i].tokens > 0 ? 1 : 0;
      }
    }

    // Adjust to exactly barWidth
    var currentTotal = charWidths.Sum();
    var diff = barWidth - currentTotal;

    // Find the largest segment to absorb the difference (prefer free space index=3)
    if (diff != 0)
    {
      var adjustIdx = 3; // free space
      if (charWidths[adjustIdx] == 0)
      {
        adjustIdx = Array.IndexOf(charWidths, charWidths.Max());
      }
      charWidths[adjustIdx] += diff;
      if (charWidths[adjustIdx] < 0) charWidths[adjustIdx] = 0;
    }

    var bar = new StringBuilder("  ");
    for (var i = 0; i < segments.Length; i++)
    {
      if (charWidths[i] <= 0) continue;
      var ch = i == 3 ? '\u2591' : '\u2588'; // light shade for free space, full block for others
      bar.Append(CultureInfo.InvariantCulture, $"[{segments[i].color}]{new string(ch, charWidths[i])}[/]");
    }

    SpectreHelpers.OutputMarkup(bar.ToString());
  }

  private static void RenderLegend(string indicator, string color, string label, int tokens, int contextLimit)
  {
    var percent = contextLimit > 0 ? (double)tokens / contextLimit * 100 : 0;
    var tokenStr = SpectreHelpers.FormatCompact(tokens);
    var percentStr = SpectreHelpers.FormatPercent(percent);
    SpectreHelpers.OutputMarkup(string.Format(
        CultureInfo.InvariantCulture,
        "    [{0}]{1}[/] {2,-18}{3,8} tokens  ({4})",
        color,
        indicator,
        Markup.Escape(label),
        tokenStr,
        percentStr));
  }

  private static void RenderTreeLine(bool isLast, string label, string value)
  {
    var connector = isLast ? "\u2514\u2500\u2500" : "\u251c\u2500\u2500";
    SpectreHelpers.OutputMarkup(string.Format(
        CultureInfo.InvariantCulture,
        "  [dim]{0}[/] {1,-20}{2}",
        connector,
        Markup.Escape(label),
        Markup.Escape(value)));
  }

  // ──────────────────────────────────────────────
  //  MESSAGE BREAKDOWN
  // ──────────────────────────────────────────────

  internal sealed record MessageBreakdown(
      int UserTextCount, int UserTextTokens,
      int AssistantTextCount, int AssistantTextTokens,
      int ToolCallCount, int ToolCallTokens,
      int ToolResultCount, int ToolResultTokens);

  internal static MessageBreakdown ComputeMessageBreakdown(IReadOnlyList<ConversationMessage> messages)
  {
    int userTextCount = 0, userTextTokens = 0;
    int assistantTextCount = 0, assistantTextTokens = 0;
    int toolCallCount = 0, toolCallTokens = 0;
    int toolResultCount = 0, toolResultTokens = 0;

    foreach (var msg in messages)
    {
      foreach (var block in msg.Content)
      {
        switch (block)
        {
          case TextBlock t:
            if (msg.Role == MessageRole.User)
            {
              userTextCount++;
              userTextTokens += t.Text.Length / 4;
            }
            else
            {
              assistantTextCount++;
              assistantTextTokens += t.Text.Length / 4;
            }
            break;
          case ToolUseBlock tu:
            toolCallCount++;
            toolCallTokens += (tu.Name.Length + tu.ArgumentsJson.Length) / 4;
            break;
          case ToolResultBlock tr:
            toolResultCount++;
            toolResultTokens += tr.Content.Length / 4;
            break;
          case ImageBlock:
            if (msg.Role == MessageRole.User)
            {
              userTextCount++;
              userTextTokens += 250;
            }
            else
            {
              assistantTextCount++;
              assistantTextTokens += 250;
            }
            break;
        }
      }
    }

    return new MessageBreakdown(
        userTextCount, userTextTokens,
        assistantTextCount, assistantTextTokens,
        toolCallCount, toolCallTokens,
        toolResultCount, toolResultTokens);
  }

  // ──────────────────────────────────────────────
  //  TOKEN ESTIMATION
  // ──────────────────────────────────────────────

  internal static int EstimateToolDefinitionTokens(ToolDefinition tool)
  {
    var chars = tool.Name.Length + tool.Description.Length;
    foreach (var param in tool.Parameters)
    {
      chars += param.Name.Length + param.Type.Length + param.Description.Length;
    }
    return chars / 4;
  }

  private static int EstimateStringTokens(string? text) => (text?.Length ?? 0) / 4;

  // ──────────────────────────────────────────────
  //  REFRESH
  // ──────────────────────────────────────────────

  private async Task HandleRefreshAsync(CancellationToken ct)
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

    // 6. Build ExecutionConfig (same logic as ChatCommand)
    var executionConfig = project.DockerImage is not null || project.RequireContainer
        ? project.BuildExecutionConfig()
        : _settings.Execution;

    // 7. Create new engine via factory, set on ActiveExecutionEngine
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

    // 8. Rebuild + assign session system prompt (after engine refresh so path mappings are current)
    session.SystemPrompt = ChatCommand.BuildSystemPrompt(project, resolvedDirs, _activeEngine.Engine?.PathMappings);

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

    SpectreHelpers.OutputLine();
    SpectreHelpers.Success("Session context refreshed.");
    _ui.StaleSettingsWarning = null;

    var metaPromptText = MetaPrompt.Build(_activeEngine.Mode, _activeEngine.Engine?.GetAvailableCommands() ?? []);
    await _conversationLogger.LogLlmContextAsync(
        session.SystemPrompt ?? "", metaPromptText,
        ToolNames,
        _activeProvider.Config?.Model ?? "unknown",
        _activeProvider.Config?.ProviderType ?? LlmProviderType.Gemini,
        ct);

    SpectreHelpers.OutputLine();

    // Directories
    var dirLabel = $"{afterDirCount} ({gitDirCount} git)";
    var dirChanged = afterDirCount != beforeDirCount;
    RenderRefreshSummaryLine("Directories", dirLabel, dirChanged);

    // Git branch
    var branchDisplay = afterBranch ?? "none";
    var branchChanged = !string.Equals(beforeBranch, afterBranch, StringComparison.Ordinal);
    if (branchChanged && beforeBranch is not null)
    {
      RenderRefreshSummaryLine("Git branch", $"{branchDisplay}  [dim](was: {Markup.Escape(beforeBranch)})[/]", true);
    }
    else
    {
      RenderRefreshSummaryLine("Git branch", branchDisplay, false);
    }

    // Engine
    var engineLabel = engineRefreshed
        ? $"{afterMode} (refreshed)"
        : $"{afterMode} (kept previous)";
    var engineChanged = engineRefreshed || afterMode != beforeMode;
    RenderRefreshSummaryLine("Engine", engineLabel, engineChanged);

    // System prompt
    var promptChanged = afterPromptLength != beforePromptLength;
    var promptLabel = promptChanged
        ? $"updated ({beforePromptLength:N0} → {afterPromptLength:N0} chars)"
        : $"unchanged ({afterPromptLength:N0} chars)";
    RenderRefreshSummaryLine("System prompt", promptLabel, promptChanged);

    SpectreHelpers.OutputLine();
  }

  private static void RenderRefreshSummaryLine(string label, string value, bool changed)
  {
    var style = changed ? "bold" : "dim";
    SpectreHelpers.OutputMarkup($"    [dim]{Markup.Escape(label),-16}[/][{style}]{value}[/]");
  }

  // ──────────────────────────────────────────────
  //  SUMMARIZE
  // ──────────────────────────────────────────────

  private async Task HandleSummarizeAsync(string? focusTopic, CancellationToken ct)
  {
    var session = _activeSession.Session;
    if (session is null)
    {
      SpectreHelpers.Error("No active session.");
      return;
    }

    if (!_activeProvider.IsConfigured)
    {
      SpectreHelpers.Error("No LLM provider configured.");
      return;
    }

    var conversation = session.Conversation;
    if (conversation.Messages.Count < 4)
    {
      SpectreHelpers.OutputMarkup("Not enough conversation to summarize (need at least 4 messages).");
      return;
    }

    var recentExchange = ExtractRecentExchange(conversation.Messages);
    var messagesToSummarize = conversation.Messages
        .Take(conversation.Messages.Count - recentExchange.Count)
        .ToList();

    var beforeTokens = EstimateContentBlockTokens(messagesToSummarize);
    var originalMessages = conversation.Messages.ToList();
    var availableTokens = CalculateAvailableTokenBudget(session, recentExchange);

    string? revisionFeedback = null;

    while (true)
    {
      var systemPrompt = BuildSummarizeSystemPrompt(focusTopic, revisionFeedback, availableTokens);

      var request = new LlmRequest
      {
        Model = _activeProvider.Config!.Model,
        SystemPrompt = systemPrompt,
        Tools = [],
        ToolChoice = ToolChoiceStrategy.None,
        Stream = false,
        Messages = messagesToSummarize,
      };

      string summaryText;
      try
      {
        TuiLayout.Current?.SetActivity(ActivityState.Thinking);
        try
        {
          var response = await _activeProvider.Provider!.SendAsync(request, ct);
          summaryText = response.TextContent ?? "";
        }
        finally
        {
          TuiLayout.Current?.SetActivity(ActivityState.Idle);
        }
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        conversation.ReplaceMessages(originalMessages);
        SpectreHelpers.Error($"Summarization failed: {ex.Message}");
        return;
      }

      if (string.IsNullOrWhiteSpace(summaryText))
      {
        SpectreHelpers.Error("Summarization produced no output.");
        return;
      }

      // Render preview panel and token savings
      var afterTokens = summaryText.Length / 4;

      if (afterTokens > availableTokens)
      {
        SpectreHelpers.Warning($"Summary exceeds budget ({afterTokens:N0} > {availableTokens:N0} tokens). Applying it may leave limited space for new conversation turns.");
      }

      var previewPanel = new Panel(new Text(summaryText))
          .Header("[bold]Summary Preview[/]")
          .Border(BoxBorder.Rounded)
          .BorderColor(Color.Grey)
          .Padding(2, 1)
          .Expand();

      SpectreHelpers.OutputRenderable(previewPanel);
      SpectreHelpers.OutputMarkup(string.Format(
          CultureInfo.InvariantCulture,
          "  [dim]{0} messages \u2192 1 summary message (estimated {1:N0} \u2192 {2:N0} tokens)[/]",
          messagesToSummarize.Count,
          beforeTokens,
          afterTokens));

      // Non-interactive mode: auto-apply
      if (!_ui.IsInteractive)
      {
        ApplySummary(conversation, summaryText, recentExchange, originalMessages);
        await _conversationLogger.LogContextSummarizeAsync(
            summaryText, focusTopic,
            originalMessages.Count, conversation.Messages.Count,
            beforeTokens, summaryText.Length / 4,
            ct);
        return;
      }

      // Interactive: present choice
      var choice = SpectreHelpers.Select(
          "What would you like to do?",
          SummarizeChoices);

      switch (choice)
      {
        case "Apply":
          ApplySummary(conversation, summaryText, recentExchange, originalMessages);
          await _conversationLogger.LogContextSummarizeAsync(
              summaryText, focusTopic,
              originalMessages.Count, conversation.Messages.Count,
              beforeTokens, summaryText.Length / 4,
              ct);
          return;

        case "Fork conversation":
          await HandleForkAsync(session, summaryText, focusTopic, ct);
          return;

        case "Revise":
          revisionFeedback = SpectreHelpers.PromptNonEmpty("Revision [green]instructions[/]:");
          continue;

        case "Cancel":
          SpectreHelpers.Cancelled();
          return;
      }
    }
  }

  private static string BuildSummarizeSystemPrompt(string? focusTopic, string? revisionFeedback, int availableTokens)
  {
    var prompt = """
        You are a conversation summarizer. Produce a concise summary that captures:
        - Key decisions made
        - Important file paths and code references
        - Pending tasks or open questions
        - Technical context needed to continue the conversation

        If a focus topic is provided, emphasize information related to that topic.
        Respond with only the summary text, no preamble.
        """;

    if (focusTopic is not null)
    {
      prompt += $"\n\nFocus topic: {focusTopic}";
    }

    if (revisionFeedback is not null)
    {
      prompt += $"\n\nRevision feedback: {revisionFeedback}";
    }

    var targetChars = availableTokens * 4;
    prompt += $"\n\nYour summary must not exceed {targetChars} characters (approximately {availableTokens} tokens).\nPrioritize density over completeness. If you cannot fit everything within the budget,\npreserve decisions and file references over discussion context.";

    return prompt;
  }

  private static void ApplySummary(
      Conversation conversation,
      string summaryText,
      List<ConversationMessage> recentExchange,
      List<ConversationMessage> originalMessages)
  {
    var replacementMessages = new List<ConversationMessage>
    {
      new(MessageRole.User, $"[The following is a summary of the earlier conversation.]\n\n{summaryText}"),
    };
    replacementMessages.AddRange(recentExchange);

    conversation.ReplaceMessages(replacementMessages);

    SpectreHelpers.Success($"Summarized {originalMessages.Count} messages into {conversation.Messages.Count}. Estimated tokens: {conversation.EstimateTokenCount():N0}");
  }

  private static int EstimateContentBlockTokens(List<ConversationMessage> messages)
  {
    var tokens = 0;
    foreach (var msg in messages)
    {
      foreach (var block in msg.Content)
      {
        tokens += block switch
        {
          TextBlock t => t.Text.Length / 4,
          ToolUseBlock tu => (tu.Name.Length + tu.ArgumentsJson.Length) / 4,
          ToolResultBlock tr => tr.Content.Length / 4,
          ImageBlock => 250,
          _ => 0,
        };
      }
    }
    return tokens;
  }

  // ──────────────────────────────────────────────
  //  PRUNE
  // ──────────────────────────────────────────────

  private async Task HandlePruneAsync(CancellationToken ct)
  {
    var session = _activeSession.Session;
    if (session is null)
    {
      SpectreHelpers.Error("No active session.");
      return;
    }

    if (!_activeProvider.IsConfigured)
    {
      SpectreHelpers.Error("No LLM provider configured.");
      return;
    }

    var conversation = session.Conversation;
    if (conversation.Messages.Count < 4)
    {
      SpectreHelpers.OutputMarkup("Not enough conversation to prune (need at least 4 messages).");
      return;
    }

    var contextLimit = _activeProvider.Provider?.Capabilities.MaxContextWindowTokens > 0
        ? _activeProvider.Provider!.Capabilities.MaxContextWindowTokens
        : _settings.ContextWindowTokenLimit;
    var targetTokens = contextLimit / 2;

    var beforeCount = conversation.Messages.Count;
    var beforeTokens = conversation.EstimateTokenCount();

    Conversation compacted;
    try
    {
      TuiLayout.Current?.SetActivity(ActivityState.Thinking);
      try
      {
        compacted = await _contextCompactor.CompactAsync(conversation, targetTokens, ct);
      }
      finally
      {
        TuiLayout.Current?.SetActivity(ActivityState.Idle);
      }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      SpectreHelpers.Error($"Pruning failed: {ex.Message}");
      return;
    }

    var afterCount = compacted.Messages.Count;
    if (afterCount >= beforeCount)
    {
      SpectreHelpers.OutputMarkup("[dim]Nothing to prune — conversation is within target size.[/]");
      return;
    }

    var afterTokens = compacted.EstimateTokenCount();
    var prunedCount = beforeCount - afterCount;
    var tokensSaved = beforeTokens - afterTokens;

    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup(string.Format(
        CultureInfo.InvariantCulture,
        "  Will prune [bold]{0}[/] messages (estimated savings: [bold]{1:N0}[/] tokens).",
        prunedCount, tokensSaved));
    SpectreHelpers.OutputMarkup(string.Format(
        CultureInfo.InvariantCulture,
        "  [dim]{0} messages, ~{1:N0} tokens → {2} messages, ~{3:N0} tokens[/]",
        beforeCount, beforeTokens, afterCount, afterTokens));

    if (_ui.IsInteractive)
    {
      if (!SpectreHelpers.Confirm("Prune?", defaultValue: true))
      {
        SpectreHelpers.Cancelled();
        return;
      }
    }

    conversation.ReplaceMessages(compacted.Messages);
    await _conversationLogger.LogContextCompactionAsync(beforeCount, afterCount, beforeTokens, afterTokens, ct);
    SpectreHelpers.Success($"Pruned {prunedCount} messages (freed ~{tokensSaved:N0} tokens).");
  }

  // ──────────────────────────────────────────────
  //  FORK
  // ──────────────────────────────────────────────

  private async Task HandleForkAsync(Session currentSession, string summaryText, string? focusTopic, CancellationToken ct)
  {
    // 1. Save current session
    await _sessionRepository.SaveAsync(currentSession, ct);

    // 2. Create new session — inherit the system prompt from the current session
    //    (already built from project + directories; avoids re-resolving here)
    var newSession = new Session(currentSession.WorkingDirectory);
    newSession.ProjectName = currentSession.ProjectName;
    newSession.SystemPrompt = currentSession.SystemPrompt;

    // 3. Seed with summary
    newSession.Conversation.AddUserMessage(
        $"[Summary of previous conversation {currentSession.Id}]\n\n{summaryText}");

    // 4. Auto-name via LLM
    string autoName;
    try
    {
      TuiLayout.Current?.SetActivity(ActivityState.Thinking);
      try
      {
        var nameRequest = new LlmRequest
        {
          Model = _activeProvider.Config!.Model,
          SystemPrompt = "Name this conversation in 3-5 words based on the following summary. Respond with only the name.",
          Tools = [],
          ToolChoice = ToolChoiceStrategy.None,
          Stream = false,
          Messages = [new ConversationMessage(MessageRole.User, summaryText)],
        };

        var nameResponse = await _activeProvider.Provider!.SendAsync(nameRequest, ct);
        autoName = nameResponse.TextContent?.Trim() ?? $"Fork of {currentSession.Id}";
        if (autoName.Length > 50)
        {
          autoName = autoName[..50];
        }
      }
      finally
      {
        TuiLayout.Current?.SetActivity(ActivityState.Idle);
      }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      autoName = $"Fork of {currentSession.Id}";
    }

    newSession.Name = autoName;

    // 5. Switch active session
    _activeSession.Set(newSession);

    // 6. Log fork to old session (before re-init)
    await _conversationLogger.LogContextForkAsync(
        currentSession.Id, newSession.Id, summaryText, autoName, ct);

    // 7. Re-initialize logger for new session
    await _conversationLogger.InitializeAsync(newSession.Id, ct);

    // 8. Log fork to new session
    await _conversationLogger.LogContextForkAsync(
        currentSession.Id, newSession.Id, summaryText, autoName, ct);

    // 9. Log session start
    await _conversationLogger.LogSessionStartAsync(
        _activeProvider.Config!.ProviderType,
        _activeProvider.Config.Model,
        newSession.ProjectName ?? "_default",
        _activeEngine.Mode,
        newSession.WorkingDirectory,
        ct);

    // 10. Save new session
    await _sessionRepository.SaveAsync(newSession, ct);

    // 11. Render confirmation
    SpectreHelpers.Success($"Forked to new conversation \"{autoName}\" ({newSession.Id}). Original session preserved.");
  }

  // ──────────────────────────────────────────────
  //  HELPERS
  // ──────────────────────────────────────────────

  internal static List<ConversationMessage> ExtractRecentExchange(IReadOnlyList<ConversationMessage> messages)
  {
    if (messages.Count < 2)
    {
      return [];
    }

    var secondToLast = messages[^2];
    var last = messages[^1];

    if (secondToLast.Role == MessageRole.User
        && !secondToLast.Content.OfType<ToolResultBlock>().Any())
    {
      return [secondToLast, last];
    }

    return [];
  }

  private int CalculateAvailableTokenBudget(Session session, List<ConversationMessage> recentExchange)
  {
    var contextLimit = _activeProvider.Provider?.Capabilities.MaxContextWindowTokens > 0
        ? _activeProvider.Provider!.Capabilities.MaxContextWindowTokens
        : _settings.ContextWindowTokenLimit;

    var metaPromptTokens = EstimateStringTokens(MetaPrompt.Build(_activeEngine.Mode, _activeEngine.Engine?.GetAvailableCommands() ?? []));
    var sessionPromptTokens = EstimateStringTokens(session.SystemPrompt);
    var toolTokens = EstimateToolDefinitionTokens(AgentOrchestrator.ShellToolDefinition);
    var recentExchangeTokens = EstimateContentBlockTokens(recentExchange);
    var safetyMargin = contextLimit * 10 / 100;
    var available = contextLimit - metaPromptTokens - sessionPromptTokens - toolTokens - recentExchangeTokens - safetyMargin;
    return Math.Max(500, available);
  }
}
