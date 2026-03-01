using BoydCode.Domain.Enums;

namespace BoydCode.Application.Interfaces;

public interface IConversationLogger : IAsyncDisposable
{
  Task InitializeAsync(string sessionId, CancellationToken ct = default);

  Task LogSessionStartAsync(
      LlmProviderType provider, string model, string project,
      ExecutionMode engineMode, string workingDirectory,
      CancellationToken ct = default);

  Task LogSessionResumeAsync(
      int messageCount, DateTimeOffset originalCreatedDate,
      CancellationToken ct = default);

  Task LogSessionEndAsync(string reason, CancellationToken ct = default);

  Task LogUserMessageAsync(string text, CancellationToken ct = default);

  Task LogSlashCommandAsync(string rawInput, CancellationToken ct = default);

  Task LogLlmContextAsync(
      string systemPrompt, string metaPrompt,
      IReadOnlyList<string> toolNames, string model, LlmProviderType provider,
      CancellationToken ct = default);

  Task LogLlmRequestAsync(
      string model, int messageCount, int estimatedTokens,
      CancellationToken ct = default);

  Task LogLlmResponseAsync(
      string? textContent, int toolCallCount,
      int inputTokens, int outputTokens,
      CancellationToken ct = default);

  Task LogToolCallAsync(
      string toolName, string argumentsJson,
      CancellationToken ct = default);

  Task LogToolResultAsync(
      string toolName, string output, bool isError, TimeSpan elapsed,
      CancellationToken ct = default);

  Task LogContextCompactionAsync(
      int messagesBefore, int messagesAfter,
      int tokensBefore, int tokensAfter,
      CancellationToken ct = default);

  Task LogContextSummarizeAsync(
      string summaryText, string? instructions,
      int messagesBefore, int messagesAfter,
      int tokensBefore, int tokensAfter,
      CancellationToken ct = default);

  Task LogContextForkAsync(
      string oldSessionId, string newSessionId,
      string summaryText, string? autoName,
      CancellationToken ct = default);

  Task LogContextClearAsync(int messagesCleared, CancellationToken ct = default);

  Task LogAgentDelegationAsync(
      string agentName, string task, string? modelOverride,
      CancellationToken ct = default);

  Task LogAgentResultAsync(
      string agentName, string result, int turnsTaken, bool isError,
      CancellationToken ct = default);

  Task LogProviderErrorAsync(
      string errorMessage, string? suggestion,
      CancellationToken ct = default);
}
