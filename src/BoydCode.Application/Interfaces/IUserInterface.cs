namespace BoydCode.Application.Interfaces;

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
