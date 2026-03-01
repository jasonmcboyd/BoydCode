using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.LlmResponses;
using BoydCode.Domain.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoydCode.Application.Services;

public sealed partial class AgentOrchestrator
{
  public static readonly ToolDefinition ShellToolDefinition = new(
      "Shell",
      "Execute a command in the current execution environment.",
      [
          new ToolParameter("command", "string", "The command to execute", Required: true),
      ]);

  private readonly ActiveProvider _activeProvider;
  private readonly ActiveExecutionEngine _activeEngine;
  private readonly IUserInterface _ui;
  private readonly IContextCompactor _contextCompactor;
  private readonly ISessionRepository _sessionRepository;
  private readonly IConversationLogger _conversationLogger;
  private readonly AppSettings _settings;
  private readonly ISlashCommandRegistry _slashCommandRegistry;
  private readonly ILogger<AgentOrchestrator> _logger;
  private int _totalInputTokens;
  private int _totalOutputTokens;

  public AgentOrchestrator(
      ActiveProvider activeProvider,
      ActiveExecutionEngine activeEngine,
      IUserInterface ui,
      IContextCompactor contextCompactor,
      ISessionRepository sessionRepository,
      IConversationLogger conversationLogger,
      IOptions<AppSettings> settings,
      ISlashCommandRegistry slashCommandRegistry,
      ILogger<AgentOrchestrator> logger)
  {
    _activeProvider = activeProvider;
    _activeEngine = activeEngine;
    _ui = ui;
    _contextCompactor = contextCompactor;
    _sessionRepository = sessionRepository;
    _conversationLogger = conversationLogger;
    _settings = settings.Value;
    _slashCommandRegistry = slashCommandRegistry;
    _logger = logger;
  }

  public async Task RunSessionAsync(Session session, CancellationToken ct = default)
  {
    while (!ct.IsCancellationRequested)
    {
      string? input;
      try
      {
        input = await _ui.GetUserInputAsync(ct);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        LogSessionLoopError(_logger, ex);
        _ui.RenderError($"Input error: {ex.Message}");
        continue;
      }

      if (string.IsNullOrWhiteSpace(input)) continue;
      var trimmed = input.Trim();
      if (trimmed.Equals("/quit", StringComparison.OrdinalIgnoreCase) ||
          trimmed.Equals("/exit", StringComparison.OrdinalIgnoreCase) ||
          trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
          trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

      // Slash command dispatch
      if (input.StartsWith('/'))
      {
        await _conversationLogger.LogSlashCommandAsync(input, ct);
        try
        {
          var handled = await _slashCommandRegistry.TryHandleAsync(input, ct);
          if (handled) continue;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
          LogSessionLoopError(_logger, ex);
          _ui.RenderError($"Command error: {ex.Message}");
          continue;
        }

        // Unrecognized slash command — never send to LLM
        var suggestion = _slashCommandRegistry.SuggestCommand(input);
        if (suggestion is not null)
        {
          _ui.RenderError($"Unknown command. Did you mean '{suggestion}'?");
        }
        else
        {
          _ui.RenderError("Unknown command. Type /help for available commands.");
        }
        continue;
      }

      await _conversationLogger.LogUserMessageAsync(input, ct);
      session.Conversation.AddUserMessage(input);

      try
      {
        await RunAgentTurnAsync(session, ct);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        // Remove the dangling user message to keep conversation state consistent
        // (prevents cascading failures from two consecutive user messages)
        session.Conversation.RemoveLastMessage();
        LogSessionLoopError(_logger, ex);
        var errorMessage = FormatProviderError(ex);
        var suggestion = ClassifyAndSuggest(ex.Message, ex);
        await _conversationLogger.LogProviderErrorAsync(errorMessage, suggestion, ct);
        _ui.RenderError(errorMessage);
      }
    }

    await AutoSaveSessionAsync(session, ct);
  }

  public async Task RunAgentTurnAsync(Session session, CancellationToken ct = default)
  {
    if (!_activeProvider.IsConfigured)
    {
      _ui.RenderError("No LLM provider configured. Use /provider setup to configure one.");
      session.Conversation.RemoveLastMessage();
      return;
    }

    _ui.SetAgentBusy(true);

    // Create a turn-level CTS so Esc/Ctrl+C cancels streaming and thinking, not just tool execution
    using var turnCts = new CancellationTokenSource();
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, turnCts.Token);
    using var turnMonitor = _ui.BeginCancellationMonitor(() => turnCts.Cancel());
    var turnToken = linkedCts.Token;

    const int maxToolRounds = 50;

    // Build the base request from session state — tier-1/2 fields are stable across rounds
    var metaPrompt = MetaPrompt.Build(_activeEngine.Mode, _activeEngine.Engine?.GetAvailableCommands() ?? []);
    var systemPrompt = session.SystemPrompt is not null
        ? $"{metaPrompt}\n\n---\n\n{session.SystemPrompt}"
        : metaPrompt;

    var baseRequest = new LlmRequest
    {
      Model = _activeProvider.Config!.Model,
      SystemPrompt = systemPrompt,
      Tools = [ShellToolDefinition],
      ToolChoice = ToolChoiceStrategy.Auto,
    };

    try
    {
      for (var round = 0; round < maxToolRounds; round++)
      {
        // Check if context compaction is needed
        await CompactIfNeededAsync(session, turnToken);

        // Build per-turn request — only Messages changes each round
        var request = baseRequest with
        {
          Messages = session.Conversation.Messages,
          Stream = _activeProvider.Provider!.Capabilities.SupportsStreaming,
        };

        await _conversationLogger.LogLlmRequestAsync(
            baseRequest.Model, session.Conversation.Messages.Count,
            session.Conversation.EstimateTokenCount(), turnToken);

        _ui.RenderThinkingStart();

        LlmResponse response;
        if (request.Stream)
        {
          response = await StreamResponseAsync(request, turnToken);
        }
        else
        {
          response = await _activeProvider.Provider!.SendAsync(request, turnToken);
          _ui.RenderThinkingStop();
          var textContent = response.TextContent;
          if (!string.IsNullOrEmpty(textContent))
          {
            _ui.RenderAssistantText(textContent);
          }
        }

        _totalInputTokens += response.Usage.InputTokens;
        _totalOutputTokens += response.Usage.OutputTokens;
        await _conversationLogger.LogLlmResponseAsync(
            response.TextContent, response.ToolUseCalls.Count(),
            response.Usage.InputTokens, response.Usage.OutputTokens, turnToken);
        _ui.RenderTokenUsage(_totalInputTokens, _totalOutputTokens);

        // Add assistant response to conversation
        session.Conversation.AddAssistantMessage(response.Content);

        // Process tool calls or stop
        if (!response.HasToolUse)
        {
          _ui.SetAgentBusy(false);
          await AutoSaveSessionAsync(session, ct);
          return;
        }

        await ProcessToolCallsAsync(session, response.ToolUseCalls, turnToken);
      }

      _ui.SetAgentBusy(false);
      LogMaxToolRoundsReached(_logger, maxToolRounds);
      _ui.RenderError($"Reached maximum tool call rounds ({maxToolRounds}). Stopping to prevent runaway execution.");
      await AutoSaveSessionAsync(session, ct);
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
    {
      // Turn was cancelled by the user (Esc/Ctrl+C), not by the outer session token.
      // Clean up gracefully — the conversation may have a partial turn.
      _ui.RenderThinkingStop();
      _ui.RenderStreamingComplete();
      _ui.RenderExecutingStop();
      _ui.SetAgentBusy(false);
      _ui.RenderHint("Cancelled.");
      await AutoSaveSessionAsync(session, ct);
    }
  }

  private async Task ProcessToolCallsAsync(Session session, IEnumerable<ToolUseBlock> toolCalls, CancellationToken ct)
  {
    foreach (var toolCall in toolCalls)
    {
      if (!toolCall.Name.Equals("Shell", StringComparison.OrdinalIgnoreCase))
      {
        session.Conversation.AddToolResult(toolCall.Id, $"Error: Unknown tool '{toolCall.Name}'. Use the Shell tool.", isError: true);
        continue;
      }

      if (!_activeEngine.IsInitialized)
      {
        session.Conversation.AddToolResult(toolCall.Id, "Error: Execution engine not initialized.", isError: true);
        continue;
      }

      await _conversationLogger.LogToolCallAsync(toolCall.Name, toolCall.ArgumentsJson, ct);
      _ui.RenderToolExecution(toolCall.Name, toolCall.ArgumentsJson);
      _ui.RenderExecutingStart();

      try
      {
        using var doc = JsonDocument.Parse(toolCall.ArgumentsJson);
        var root = doc.RootElement;
        var command = root.GetProperty("command").GetString()
            ?? throw new ArgumentException("command is required");

        var outputStreamed = false;
        var result = await _activeEngine.Engine!.ExecuteAsync(
            command, session.WorkingDirectory,
            onOutputLine: line =>
            {
              _ui.RenderOutputLine(line);
              outputStreamed = true;
            },
            ct);
        _ui.RenderExecutingStop();

        // Format output for LLM
        var output = result.Output;
        if (result.HadErrors && result.ErrorOutput is not null)
        {
          output = string.IsNullOrEmpty(output)
              ? $"Error: {result.ErrorOutput}"
              : $"{output}\n\nErrors:\n{result.ErrorOutput}";
        }

        if (result.HadErrors && output.Contains("is not recognized", StringComparison.OrdinalIgnoreCase))
        {
          var commands = _activeEngine.Engine?.GetAvailableCommands();
          if (commands is { Count: > 0 })
          {
            output += $"\n\nAvailable commands: {string.Join(", ", commands)}";
          }
        }

        if (output.Length > 30_000)
        {
          output = string.Concat(output.AsSpan(0, 29_997), "...");
        }

        // Brief summary when output was already streamed; full output otherwise
        var displayOutput = outputStreamed
            ? $"{result.Duration.TotalSeconds:F1}s"
            : output;
        _ui.RenderToolResult(toolCall.Name, displayOutput, result.HadErrors);
        await _conversationLogger.LogToolResultAsync(toolCall.Name, output, result.HadErrors, result.Duration, ct);
        session.Conversation.AddToolResult(toolCall.Id, output, result.HadErrors);
      }
      catch (OperationCanceledException) when (!ct.IsCancellationRequested)
      {
        _ui.RenderExecutingStop();
        _ui.RenderToolResult(toolCall.Name, "Command cancelled.", isError: false);
        await _conversationLogger.LogToolResultAsync(toolCall.Name, "Command was cancelled by the user.", false, TimeSpan.Zero, CancellationToken.None);
        session.Conversation.AddToolResult(toolCall.Id, "Command was cancelled by the user.", isError: false);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        _ui.RenderExecutingStop();
        var errorMsg = $"Error executing command: {ex.Message}";
        _ui.RenderToolResult(toolCall.Name, errorMsg, isError: true);
        await _conversationLogger.LogToolResultAsync(toolCall.Name, errorMsg, true, TimeSpan.Zero, CancellationToken.None);
        session.Conversation.AddToolResult(toolCall.Id, errorMsg, isError: true);
      }
    }
  }

  private async Task CompactIfNeededAsync(Session session, CancellationToken ct)
  {
    var messageTokens = session.Conversation.EstimateTokenCount();
    var metaPromptText = MetaPrompt.Build(_activeEngine.Mode, _activeEngine.Engine?.GetAvailableCommands() ?? []);
    var systemPromptTokens = (metaPromptText.Length + (session.SystemPrompt?.Length ?? 0)) / 4;
    var estimated = messageTokens + systemPromptTokens;

    var contextLimit = _activeProvider.Provider!.Capabilities.MaxContextWindowTokens > 0
        ? _activeProvider.Provider!.Capabilities.MaxContextWindowTokens
        : _settings.ContextWindowTokenLimit;
    var threshold = contextLimit * _settings.CompactionThresholdPercent / 100;
    if (estimated > threshold)
    {
      LogContextCompactionTriggered(_logger, estimated, threshold);
      var previousCount = session.Conversation.Messages.Count;
      var targetTokens = contextLimit / 2;
      var compacted = await _contextCompactor.CompactAsync(session.Conversation, targetTokens, ct);
      session.Conversation.ReplaceMessages(compacted.Messages);

      // The compactor may prepend a notice message, so count only non-notice messages
      var newCount = session.Conversation.Messages.Count;
      var hasNotice = newCount > 0
          && newCount != previousCount
          && session.Conversation.Messages[0].Content
              .OfType<TextBlock>()
              .Any(t => t.Text.Contains("compacted to manage context window size"));
      var keptCount = hasNotice ? newCount - 1 : newCount;
      var droppedCount = previousCount - keptCount;
      if (droppedCount > 0)
      {
        var newEstimate = session.Conversation.EstimateTokenCount() + systemPromptTokens;
        _ui.RenderWarning(
          $"Context compacted: {droppedCount} message(s) removed to fit context window. " +
          $"Estimated tokens: {newEstimate:N0} (target: {targetTokens:N0}).");
        await _conversationLogger.LogContextCompactionAsync(previousCount, newCount, estimated, newEstimate, ct);
      }
    }
  }

  private async Task<LlmResponse> StreamResponseAsync(LlmRequest request, CancellationToken ct)
  {
    var accumulator = new StreamAccumulator();
    var textStarted = false;
    var thinkingStopped = false;

    try
    {
      await foreach (var chunk in _activeProvider.Provider!.StreamAsync(request, ct))
      {
        if (!thinkingStopped)
        {
          _ui.RenderThinkingStop();
          thinkingStopped = true;
        }

        accumulator.Process(chunk);

        if (chunk is TextChunk textChunk)
        {
          _ui.RenderStreamingToken(textChunk.Text);
          textStarted = true;
        }
      }
    }
    finally
    {
      if (!thinkingStopped)
      {
        _ui.RenderThinkingStop();
      }

      if (textStarted)
      {
        _ui.RenderStreamingComplete();
      }
    }

    return accumulator.ToResponse();
  }

  private async Task AutoSaveSessionAsync(Session session, CancellationToken ct)
  {
    try
    {
      session.LastAccessedAt = DateTimeOffset.UtcNow;
      await _sessionRepository.SaveAsync(session, ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      LogAutoSaveFailed(_logger, ex);
    }
  }

  /// <summary>
  /// Extracts a user-friendly error message from provider exceptions.
  /// Provider SDKs often embed JSON error bodies in their exception messages
  /// (e.g. Anthropic: "Status Code: BadRequest\n{\"error\":{\"message\":\"...\"}}").
  /// This method tries to pull out the human-readable message instead of
  /// showing the raw JSON + stack trace.
  /// </summary>
  private static string FormatProviderError(Exception ex)
  {
    var message = ex.Message;

    // Try to extract a message from an embedded JSON error body
    var jsonStart = message.IndexOf('{');
    if (jsonStart >= 0)
    {
      try
      {
        using var doc = JsonDocument.Parse(message[jsonStart..]);
        var root = doc.RootElement;

        // Nested style: { "error": { "message": "..." } }
        // Covers Anthropic, OpenAI, and Google error responses.
        if (root.TryGetProperty("error", out var errorObj) &&
            errorObj.ValueKind == JsonValueKind.Object &&
            errorObj.TryGetProperty("message", out var nestedMsg))
        {
          return nestedMsg.GetString() ?? message;
        }

        // Flat style: { "message": "..." }
        if (root.TryGetProperty("message", out var flatMsg))
        {
          return flatMsg.GetString() ?? message;
        }
      }
      catch (JsonException)
      {
        // Not valid JSON — fall through
      }
    }

    // If the exception wraps another with a cleaner message, prefer it
    if (ex.InnerException is not null && ex.InnerException.Message != message)
    {
      message = ex.InnerException.Message;
    }

    var suggestion = ClassifyAndSuggest(message, ex);
    return suggestion is not null
        ? $"{message}\n  Suggestion: {suggestion}"
        : message;
  }

  internal static string? ClassifyAndSuggest(string message, Exception ex)
  {
    var lower = message.ToUpperInvariant();

    if (lower.Contains("API KEY") || lower.Contains("UNAUTHORIZED") ||
        lower.Contains("AUTHENTICATION") || lower.Contains("PERMISSION_DENIED") ||
        lower.Contains("FORBIDDEN"))
    {
      return "Check your API key with /provider setup or pass --api-key.";
    }

    if (lower.Contains("RATE LIMIT") || lower.Contains("TOO MANY REQUESTS") ||
        lower.Contains("429") || lower.Contains("QUOTA"))
    {
      return "Wait a moment and retry, or switch providers with /provider setup.";
    }

    if (lower.Contains("CONTEXT") || lower.Contains("TOKEN LIMIT") ||
        lower.Contains("TOO LONG") || lower.Contains("MAX.*TOKEN"))
    {
      return "Start a new session or switch to a model with a larger context window.";
    }

    if (ex is HttpRequestException || lower.Contains("CONNECTION") ||
        lower.Contains("TIMEOUT") || lower.Contains("NETWORK"))
    {
      return "Check your internet connection and try again.";
    }

    if (lower.Contains("500") || lower.Contains("SERVER ERROR") ||
        lower.Contains("OVERLOADED") || lower.Contains("503"))
    {
      return "The provider may be experiencing issues. Try again in a few moments.";
    }

    return null;
  }

  [LoggerMessage(Level = LogLevel.Information, Message = "Context compaction triggered: {Estimated} tokens exceeds {Threshold} threshold")]
  private static partial void LogContextCompactionTriggered(ILogger logger, int estimated, int threshold);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Error during agent turn")]
  private static partial void LogSessionLoopError(ILogger logger, Exception exception);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Reached maximum tool call rounds: {MaxRounds}")]
  private static partial void LogMaxToolRoundsReached(ILogger logger, int maxRounds);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to auto-save session")]
  private static partial void LogAutoSaveFailed(ILogger logger, Exception exception);
}
