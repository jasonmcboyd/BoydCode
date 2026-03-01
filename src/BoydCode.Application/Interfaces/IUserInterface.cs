using BoydCode.Domain.Tools;

namespace BoydCode.Application.Interfaces;

public interface IUserInterface
{
  string? StatusLine { get; set; }
  Task<string> GetUserInputAsync(CancellationToken ct = default);
  Task<bool> RequestPermissionAsync(ToolDefinition tool, string argumentsJson, CancellationToken ct = default);
  void RenderAssistantText(string text);
  void RenderStreamingToken(string token);
  void RenderStreamingComplete();
  void RenderToolExecution(string toolName, string argumentsSummary);
  void RenderToolResult(string toolName, string result, bool isError);
  void RenderError(string message);
  void RenderTokenUsage(int inputTokens, int outputTokens);
  void RenderWelcome(string model, string workingDirectory);
  void RenderMarkdown(string markdown);
}
