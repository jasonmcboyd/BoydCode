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
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed class ContextSlashCommand : ISlashCommand
{
  private readonly ActiveSession _activeSession;
  private readonly ActiveProvider _activeProvider;
  private readonly IContextCompactor _contextCompactor;
  private readonly IToolRegistry _toolRegistry;
  private readonly AppSettings _settings;
  private readonly ActiveExecutionEngine _activeEngine;

  public ContextSlashCommand(
      ActiveSession activeSession,
      ActiveProvider activeProvider,
      IContextCompactor contextCompactor,
      IToolRegistry toolRegistry,
      IOptions<AppSettings> settings,
      ActiveExecutionEngine activeEngine)
  {
    _activeSession = activeSession;
    _activeProvider = activeProvider;
    _contextCompactor = contextCompactor;
    _toolRegistry = toolRegistry;
    _settings = settings.Value;
    _activeEngine = activeEngine;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/context",
      "View and manage conversation context",
      [
          new("show", "Show detailed context breakdown with chart"),
          new("compact", "Manually trigger context compaction"),
          new("summarize [topic]", "Summarize conversation using LLM"),
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
      case "compact":
        await HandleCompactAsync(ct);
        break;
      case "summarize":
        var focusTopic = tokens.Length > 2
            ? string.Join(' ', tokens.Skip(2))
            : null;
        await HandleSummarizeAsync(focusTopic, ct);
        break;
      case "":
      default:
        SpectreHelpers.Usage("/context show|compact|summarize [topic]");
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
    var toolDefinitions = _toolRegistry.GetAllDefinitions();

    // ── Compute token categories ──────────────────

    var metaPromptTokens = EstimateStringTokens(MetaPrompt.Build(_activeEngine.Mode, _activeEngine.Engine?.GetAvailableCommands() ?? []));
    var sessionPromptTokens = EstimateStringTokens(session.SystemPrompt);
    var systemPromptTokens = metaPromptTokens + sessionPromptTokens;

    var toolTokensList = toolDefinitions
        .Select(t => (Tool: t, Tokens: EstimateToolDefinitionTokens(t)))
        .OrderByDescending(t => t.Tokens)
        .ToList();
    var toolTokensTotal = toolTokensList.Sum(t => t.Tokens);

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

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine(string.Format(
        CultureInfo.InvariantCulture,
        "  [bold]{0}[/] [dim]\u00b7[/] [bold]{1}[/] [dim]\u00b7[/] [{2}]{3}/{4} tokens ({5})[/]",
        Markup.Escape(providerName),
        Markup.Escape(modelName),
        usageColor,
        SpectreHelpers.FormatCompact(totalUsed),
        SpectreHelpers.FormatCompact(contextLimit),
        SpectreHelpers.FormatPercent(usagePercent)));
    AnsiConsole.WriteLine();

    // ── Stacked bar ───────────────────────────────

    RenderStackedBar(systemPromptTokens, toolTokensTotal, messageTokensTotal, freeTokens, bufferTokens, contextLimit);
    AnsiConsole.WriteLine();

    // ── Legend ─────────────────────────────────────

    AnsiConsole.MarkupLine("  [bold]Estimated usage by category[/]");
    RenderLegend("\u25a0", "blue", "System prompt", systemPromptTokens, contextLimit);
    RenderLegend("\u25a0", "mediumpurple1", "Tools", toolTokensTotal, contextLimit);
    RenderLegend("\u25a0", "green", "Messages", messageTokensTotal, contextLimit);
    RenderLegend("\u25a0", "grey", "Free space", freeTokens, contextLimit);
    RenderLegend("\u25a0", "darkorange", "Compact buffer", bufferTokens, contextLimit);
    AnsiConsole.WriteLine();

    // ── System prompt breakdown ───────────────────

    AnsiConsole.MarkupLine(string.Format(
        CultureInfo.InvariantCulture,
        "  [blue bold]System prompt[/] [dim]\u00b7[/] {0} tokens",
        SpectreHelpers.FormatCompact(systemPromptTokens)));
    RenderTreeLine(false, "Meta prompt", string.Format(CultureInfo.InvariantCulture, "{0} tokens", SpectreHelpers.FormatCompact(metaPromptTokens)));
    RenderTreeLine(true, "Session prompt", string.Format(CultureInfo.InvariantCulture, "{0} tokens", SpectreHelpers.FormatCompact(sessionPromptTokens)));
    AnsiConsole.WriteLine();

    // ── Message breakdown ─────────────────────────

    var totalMessageCount = messageBreakdown.UserTextCount
        + messageBreakdown.AssistantTextCount
        + messageBreakdown.ToolCallCount
        + messageBreakdown.ToolResultCount;

    AnsiConsole.MarkupLine(string.Format(
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
    AnsiConsole.WriteLine();

    // ── Tool inventory ────────────────────────────

    AnsiConsole.MarkupLine(string.Format(
        CultureInfo.InvariantCulture,
        "  [mediumpurple1 bold]Tools[/] [dim]\u00b7[/] {0} tools, {1} tokens",
        toolDefinitions.Count.ToString(CultureInfo.InvariantCulture),
        SpectreHelpers.FormatCompact(toolTokensTotal)));

    for (var i = 0; i < toolTokensList.Count; i++)
    {
      var (tool, tokens) = toolTokensList[i];
      var isLast = i == toolTokensList.Count - 1;
      RenderTreeLine(isLast, tool.Name, string.Format(CultureInfo.InvariantCulture, "{0} tokens", SpectreHelpers.FormatCompact(tokens)));
    }

    AnsiConsole.WriteLine();
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

    AnsiConsole.MarkupLine(bar.ToString());
  }

  private static void RenderLegend(string indicator, string color, string label, int tokens, int contextLimit)
  {
    var percent = contextLimit > 0 ? (double)tokens / contextLimit * 100 : 0;
    var tokenStr = SpectreHelpers.FormatCompact(tokens);
    var percentStr = SpectreHelpers.FormatPercent(percent);
    AnsiConsole.MarkupLine(string.Format(
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
    AnsiConsole.MarkupLine(string.Format(
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
  //  COMPACT
  // ──────────────────────────────────────────────

  private async Task HandleCompactAsync(CancellationToken ct)
  {
    var session = _activeSession.Session;
    if (session is null)
    {
      SpectreHelpers.Error("No active session.");
      return;
    }

    var conversation = session.Conversation;
    if (conversation.Messages.Count == 0)
    {
      AnsiConsole.MarkupLine("Nothing to compact.");
      return;
    }

    var contextLimit = _activeProvider.Provider?.Capabilities.MaxContextWindowTokens > 0
        ? _activeProvider.Provider!.Capabilities.MaxContextWindowTokens
        : _settings.ContextWindowTokenLimit;
    var targetTokens = contextLimit / 2;

    var previousCount = conversation.Messages.Count;
    var compacted = await _contextCompactor.CompactAsync(conversation, targetTokens, ct);
    conversation.ReplaceMessages(compacted.Messages);

    var newTokens = conversation.EstimateTokenCount();
    SpectreHelpers.Success($"Compacted: {previousCount - conversation.Messages.Count} message(s) removed. Estimated tokens: {newTokens:N0}");
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
      AnsiConsole.MarkupLine("Not enough conversation to summarize (need at least 4 messages).");
      return;
    }

    var recentExchange = ExtractRecentExchange(conversation.Messages);
    var messagesToSummarize = conversation.Messages
        .Take(conversation.Messages.Count - recentExchange.Count)
        .ToList();

    var summarizationSystemPrompt = """
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
      summarizationSystemPrompt += $"\n\nFocus topic: {focusTopic}";
    }

    var request = new LlmRequest
    {
      Model = _activeProvider.Config!.Model,
      SystemPrompt = summarizationSystemPrompt,
      Tools = [],
      ToolChoice = ToolChoiceStrategy.None,
      Stream = false,
      Messages = messagesToSummarize,
    };

    var originalMessages = conversation.Messages.ToList();

    try
    {
      var response = await _activeProvider.Provider!.SendAsync(request, ct);
      var summaryText = response.TextContent ?? "";

      if (string.IsNullOrWhiteSpace(summaryText))
      {
        SpectreHelpers.Error("Summarization produced no output.");
        return;
      }

      var replacementMessages = new List<ConversationMessage>
      {
        new(MessageRole.User, $"[The following is a summary of the earlier conversation.]\n\n{summaryText}"),
      };
      replacementMessages.AddRange(recentExchange);

      conversation.ReplaceMessages(replacementMessages);

      SpectreHelpers.Success($"Summarized {originalMessages.Count} messages into {conversation.Messages.Count}. Estimated tokens: {conversation.EstimateTokenCount():N0}");
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      conversation.ReplaceMessages(originalMessages);
      SpectreHelpers.Error($"Summarization failed: {ex.Message}");
    }
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
}
