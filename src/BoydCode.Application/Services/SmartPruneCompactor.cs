using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BoydCode.Application.Services;

public sealed partial class SmartPruneCompactor : IContextCompactor
{
  private const int MinMessagesForBoundaryDetection = 6;
  private const int MinGapBetweenBoundaries = 3;

  private readonly ActiveProvider _activeProvider;
  private readonly IContextCompactor _fallbackCompactor;
  private readonly ILogger<SmartPruneCompactor> _logger;

  public SmartPruneCompactor(
    ActiveProvider activeProvider,
    [FromKeyedServices("eviction")] IContextCompactor fallbackCompactor,
    ILogger<SmartPruneCompactor> logger)
  {
    _activeProvider = activeProvider;
    _fallbackCompactor = fallbackCompactor;
    _logger = logger;
  }

  public async Task<Conversation> CompactAsync(
    Conversation conversation,
    int targetTokenCount,
    CancellationToken ct = default)
  {
    if (!_activeProvider.IsConfigured || conversation.Messages.Count < MinMessagesForBoundaryDetection)
    {
      LogFallbackToEviction(_logger, "provider not configured or too few messages");
      return await _fallbackCompactor.CompactAsync(conversation, targetTokenCount, ct);
    }

    try
    {
      var boundaries = await DetectBoundariesAsync(conversation, ct);
      if (boundaries.Count == 0)
      {
        LogFallbackToEviction(_logger, "no valid boundaries detected");
        return await _fallbackCompactor.CompactAsync(conversation, targetTokenCount, ct);
      }

      return PruneAtBestBoundary(conversation, targetTokenCount, boundaries);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      LogBoundaryDetectionFailed(_logger, ex);
      return await _fallbackCompactor.CompactAsync(conversation, targetTokenCount, ct);
    }
  }

  private async Task<List<int>> DetectBoundariesAsync(Conversation conversation, CancellationToken ct)
  {
    var summaryBuilder = new StringBuilder();
    var messages = conversation.Messages;
    for (var i = 0; i < messages.Count; i++)
    {
      var msg = messages[i];
      var roleLabel = msg.Role == MessageRole.User ? "user" : "assistant";
      var preview = GetMessagePreview(msg, 100);
      summaryBuilder.AppendLine(CultureInfo.InvariantCulture, $"[{i}] {roleLabel}: {preview}");
    }

    var systemPrompt =
      "You are analyzing a conversation to find topic transition points. Identify the message " +
      "indices where the conversation shifts to a new topic or task. Return only the indices " +
      "as a comma-separated list, ordered from oldest to newest. A good transition point is " +
      "where the user starts a new request, shifts focus to a different part of the codebase, " +
      "or begins a new line of inquiry.";

    var request = new LlmRequest
    {
      Model = _activeProvider.Config!.Model,
      SystemPrompt = systemPrompt,
      Tools = [],
      ToolChoice = ToolChoiceStrategy.None,
      Stream = false,
      Messages = [new ConversationMessage(MessageRole.User, summaryBuilder.ToString())],
    };

    var response = await _activeProvider.Provider!.SendAsync(request, ct);
    var responseText = response.TextContent ?? "";

    return ParseBoundaryIndices(responseText, messages.Count);
  }

  internal static List<int> ParseBoundaryIndices(string text, int messageCount)
  {
    var result = new List<int>();
    var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    foreach (var part in parts)
    {
      var match = Regex.Match(part, @"\d+");
      if (!match.Success || !int.TryParse(match.Value, out var index))
      {
        continue;
      }

      if (index <= 0 || index >= messageCount - 1)
      {
        continue;
      }

      if (result.Count > 0 && index - result[^1] < MinGapBetweenBoundaries)
      {
        continue;
      }

      result.Add(index);
    }

    return result;
  }

  internal static Conversation PruneAtBestBoundary(
    Conversation conversation,
    int targetTokenCount,
    List<int> boundaries)
  {
    var messages = conversation.Messages;

    var bestBoundary = boundaries[0];
    var bestDiff = int.MaxValue;

    foreach (var boundary in boundaries)
    {
      var keptTokens = 0;
      for (var i = boundary; i < messages.Count; i++)
      {
        keptTokens += EstimateMessageTokens(messages[i]);
      }

      var diff = Math.Abs(keptTokens - targetTokenCount);
      if (diff < bestDiff)
      {
        bestDiff = diff;
        bestBoundary = boundary;
      }
    }

    var result = new Conversation();
    if (bestBoundary > 0)
    {
      result.AddUserMessage(CompactionNotice);
    }

    for (var i = bestBoundary; i < messages.Count; i++)
    {
      result.AddMessage(messages[i]);
    }

    return result;
  }

  internal const string CompactionNotice =
    "[Earlier conversation context was pruned at a topic boundary to manage context window size. " +
    "Some previous messages, tool calls, and results are no longer visible.]";

  private static string GetMessagePreview(ConversationMessage message, int maxChars)
  {
    foreach (var block in message.Content)
    {
      var text = block switch
      {
        TextBlock t => t.Text,
        ToolUseBlock tu => $"[tool:{tu.Name}]",
        ToolResultBlock tr => tr.Content,
        _ => null,
      };

      if (text is not null)
      {
        return text.Length <= maxChars ? text : string.Concat(text.AsSpan(0, maxChars), "...");
      }
    }

    return "[empty]";
  }

  private static int EstimateMessageTokens(ConversationMessage message)
  {
    var chars = 0;
    foreach (var block in message.Content)
    {
      chars += block switch
      {
        TextBlock t => t.Text.Length,
        ToolUseBlock tu => tu.Name.Length + tu.ArgumentsJson.Length,
        ToolResultBlock tr => tr.Content.Length,
        ImageBlock => 1000,
        _ => 0,
      };
    }

    return chars / 4;
  }

  [LoggerMessage(Level = LogLevel.Information, Message = "Falling back to eviction compactor: {Reason}")]
  private static partial void LogFallbackToEviction(ILogger logger, string reason);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Boundary detection failed, falling back to eviction")]
  private static partial void LogBoundaryDetectionFailed(ILogger logger, Exception exception);
}
