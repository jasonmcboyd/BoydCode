namespace BoydCode.Application.Interfaces;

public sealed record HelpCommandGroup(string Prefix, string Description, IReadOnlyList<HelpSubcommand> Subcommands);
public sealed record HelpSubcommand(string Usage, string Description);

public sealed record ContextUsageData(
  string ProviderName, string ModelName,
  int TotalUsed, int ContextLimit,
  int SystemPromptTokens, int MetaPromptTokens, int SessionPromptTokens,
  int ToolTokensTotal, int MessageTokensTotal,
  int FreeTokens, int BufferTokens,
  int UserTextCount, int UserTextTokens,
  int AssistantTextCount, int AssistantTextTokens,
  int ToolCallCount, int ToolCallTokens,
  int ToolResultCount, int ToolResultTokens,
  int TotalMessageCount, string ToolName);

public interface IUserInterface
{
  bool IsInteractive { get; }
  string? StatusLine { get; set; }
  string? StaleSettingsWarning { get; set; }
  Task<string> GetUserInputAsync(CancellationToken ct = default);
  void RenderUserMessage(string message);
  void RenderAssistantText(string text);
  void RenderStreamingToken(string token);
  void RenderStreamingComplete();
  void RenderThinkingStart();
  void RenderThinkingStop();
  void RenderToolExecution(string toolName, string argumentsJson);
  void RenderExecutingStart();
  void RenderExecutingStop();
  void RenderOutputLine(string line);
  void RenderToolResult(string toolName, string result, bool isError);
  void RenderError(string message);
  void RenderHint(string hint);
  void RenderSuccess(string message);
  void RenderWarning(string message);
  void RenderSection(string title);
  void RenderTokenUsage(int inputTokens, int outputTokens);
  void RenderWelcome(string model, string workingDirectory);
  void RenderMarkdown(string markdown);
  void RenderCancelHint();
  void ClearCancelHint();
  void ExpandLastToolOutput();
  void ShowModal(string title, string content);
  void ShowDetailModal(string title, IReadOnlyList<DetailSection> sections);
  void ShowHelpModal(IReadOnlyList<HelpCommandGroup> commandGroups);
  void ShowContextModal(ContextUsageData data);
  void DismissModal();
  bool IsModalActive { get; }
  IDisposable BeginCancellationMonitor(Action onCancelRequested);
  void BeginTurn();
  void EndTurn();
  void ActivateLayout();
  void DeactivateLayout();
  void SuspendLayout();
  void ResumeLayout();
  void SetAgentBusy(bool busy);
}
