using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Persistence.Logging;

/// <summary>
/// Append-only JSONL conversation logger that writes structured events to
/// ~/.boydcode/logs/{sessionId}.jsonl. Logging failures are swallowed and
/// reported via <see cref="ILogger{T}"/> so they never crash the application.
/// </summary>
public sealed partial class JsonlConversationLogger : IConversationLogger, IAsyncDisposable
{
  private const int MaxToolOutputChars = 10_000;
  private const int MaxTextContentChars = 5_000;
  private const int MaxSystemPromptChars = 5_000;

  private static readonly string LogDirectory =
    Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      ".boydcode",
      "logs");

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
  };

  private readonly ILogger<JsonlConversationLogger> _logger;
  private readonly SemaphoreSlim _writeLock = new(1, 1);

  private string _sessionId = string.Empty;
  private StreamWriter? _writer;

  public JsonlConversationLogger(ILogger<JsonlConversationLogger> logger)
  {
    _logger = logger;
  }

  public Task InitializeAsync(string sessionId, CancellationToken ct = default)
  {
    _sessionId = sessionId;

    try
    {
      Directory.CreateDirectory(LogDirectory);
      var filePath = GetLogFilePath(sessionId);
      var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
      _writer = new StreamWriter(stream) { AutoFlush = false };
      LogInitialized(sessionId, filePath);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      LogInitializationFailed(sessionId, ex);
    }

    return Task.CompletedTask;
  }

  public Task LogSessionStartAsync(
    LlmProviderType provider, string model, string project,
    ExecutionMode engineMode, string workingDirectory,
    CancellationToken ct = default)
  {
    return WriteEventAsync("session_start", new
    {
      provider = provider.ToString(),
      model,
      project,
      engine_mode = engineMode.ToString(),
      working_directory = workingDirectory,
    }, ct);
  }

  public Task LogSessionResumeAsync(
    int messageCount, DateTimeOffset originalCreatedDate,
    CancellationToken ct = default)
  {
    return WriteEventAsync("session_resume", new
    {
      message_count = messageCount,
      original_created_date = originalCreatedDate,
    }, ct);
  }

  public Task LogSessionEndAsync(string reason, CancellationToken ct = default)
  {
    return WriteEventAsync("session_end", new
    {
      reason,
    }, ct);
  }

  public Task LogUserMessageAsync(string text, CancellationToken ct = default)
  {
    return WriteEventAsync("user_message", new
    {
      text = Truncate(text, MaxTextContentChars),
    }, ct);
  }

  public Task LogSlashCommandAsync(string rawInput, CancellationToken ct = default)
  {
    return WriteEventAsync("slash_command", new
    {
      raw_input = rawInput,
    }, ct);
  }

  public Task LogLlmContextAsync(
    string systemPrompt, string metaPrompt,
    IReadOnlyList<string> toolNames, string model, LlmProviderType provider,
    CancellationToken ct = default)
  {
    return WriteEventAsync("llm_context", new
    {
      system_prompt = Truncate(systemPrompt, MaxSystemPromptChars),
      meta_prompt = Truncate(metaPrompt, MaxSystemPromptChars),
      tool_names = toolNames,
      model,
      provider = provider.ToString(),
    }, ct);
  }

  public Task LogLlmRequestAsync(
    string model, int messageCount, int estimatedTokens,
    CancellationToken ct = default)
  {
    return WriteEventAsync("llm_request", new
    {
      model,
      message_count = messageCount,
      estimated_tokens = estimatedTokens,
    }, ct);
  }

  public Task LogLlmResponseAsync(
    string? textContent, int toolCallCount,
    int inputTokens, int outputTokens,
    CancellationToken ct = default)
  {
    return WriteEventAsync("llm_response", new
    {
      text_content = textContent is not null ? Truncate(textContent, MaxTextContentChars) : null,
      tool_call_count = toolCallCount,
      input_tokens = inputTokens,
      output_tokens = outputTokens,
    }, ct);
  }

  public Task LogToolCallAsync(
    string toolName, string argumentsJson,
    CancellationToken ct = default)
  {
    return WriteEventAsync("tool_call", new
    {
      tool_name = toolName,
      arguments_json = Truncate(argumentsJson, MaxToolOutputChars),
    }, ct);
  }

  public Task LogToolResultAsync(
    string toolName, string output, bool isError, TimeSpan elapsed,
    CancellationToken ct = default)
  {
    return WriteEventAsync("tool_result", new
    {
      tool_name = toolName,
      output = Truncate(output, MaxToolOutputChars),
      is_error = isError,
      elapsed_ms = elapsed.TotalMilliseconds,
    }, ct);
  }

  public Task LogContextCompactionAsync(
    int messagesBefore, int messagesAfter,
    int tokensBefore, int tokensAfter,
    CancellationToken ct = default)
  {
    return WriteEventAsync("context_compaction", new
    {
      messages_before = messagesBefore,
      messages_after = messagesAfter,
      tokens_before = tokensBefore,
      tokens_after = tokensAfter,
    }, ct);
  }

  public Task LogContextSummarizeAsync(
    int messagesBefore, int messagesAfter,
    CancellationToken ct = default)
  {
    return WriteEventAsync("context_summarize", new
    {
      messages_before = messagesBefore,
      messages_after = messagesAfter,
    }, ct);
  }

  public Task LogContextClearAsync(int messagesCleared, CancellationToken ct = default)
  {
    return WriteEventAsync("context_clear", new
    {
      messages_cleared = messagesCleared,
    }, ct);
  }

  public Task LogProviderErrorAsync(
    string errorMessage, string? suggestion,
    CancellationToken ct = default)
  {
    return WriteEventAsync("provider_error", new
    {
      error_message = errorMessage,
      suggestion,
    }, ct);
  }

  public async ValueTask DisposeAsync()
  {
    if (_writer is not null)
    {
      try
      {
        await _writer.FlushAsync().ConfigureAwait(false);
        await _writer.DisposeAsync().ConfigureAwait(false);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        LogDisposeFailed(ex);
      }

      _writer = null;
    }

    _writeLock.Dispose();
  }

  private async Task WriteEventAsync(string eventType, object data, CancellationToken ct)
  {
    if (_writer is null)
    {
      return;
    }

    await _writeLock.WaitAsync(ct).ConfigureAwait(false);
    try
    {
      var envelope = new
      {
        type = eventType,
        timestamp = DateTimeOffset.UtcNow.ToString("O"),
        session_id = _sessionId,
        data,
      };

      var json = JsonSerializer.Serialize(envelope, JsonOptions);
      await _writer.WriteLineAsync(json.AsMemory(), ct).ConfigureAwait(false);
      await _writer.FlushAsync(ct).ConfigureAwait(false);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      LogWriteFailed(eventType, _sessionId, ex);
    }
    finally
    {
      _writeLock.Release();
    }
  }

  private static string Truncate(string value, int maxLength)
  {
    if (value.Length <= maxLength)
    {
      return value;
    }

    return string.Concat(value.AsSpan(0, maxLength), "...[truncated]");
  }

  private static string GetLogFilePath(string sessionId) =>
    Path.Combine(LogDirectory, $"{sessionId}.jsonl");

  // --- LoggerMessage source-generated methods (CA1848 compliance) ---

  [LoggerMessage(Level = LogLevel.Debug, Message = "Conversation logger initialized for session {SessionId} at {FilePath}")]
  private partial void LogInitialized(string sessionId, string filePath);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to initialize conversation logger for session {SessionId}")]
  private partial void LogInitializationFailed(string sessionId, Exception exception);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to write {EventType} event for session {SessionId}")]
  private partial void LogWriteFailed(string eventType, string sessionId, Exception exception);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to dispose conversation logger")]
  private partial void LogDisposeFailed(Exception exception);
}
