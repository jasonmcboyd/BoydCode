namespace BoydCode.Application.Interfaces;

public sealed record ShellExecutionResult(
    string Output, string? ErrorOutput, bool HadErrors, TimeSpan Duration);

public interface IExecutionEngine : IAsyncDisposable
{
  Task InitializeAsync(CancellationToken ct = default);
  Task<ShellExecutionResult> ExecuteAsync(
      string command, string workingDirectory, int timeoutMs = 120_000,
      CancellationToken ct = default);
  IReadOnlyList<string> GetAvailableCommands();
}
