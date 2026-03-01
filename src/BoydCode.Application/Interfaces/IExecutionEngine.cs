namespace BoydCode.Application.Interfaces;

public sealed record ShellExecutionResult(
    string Output, string? ErrorOutput, bool HadErrors, TimeSpan Duration);

public interface IExecutionEngine : IAsyncDisposable
{
  Task InitializeAsync(CancellationToken ct = default);
  Task<ShellExecutionResult> ExecuteAsync(
      string command, string workingDirectory,
      Action<string>? onOutputLine = null,
      CancellationToken ct = default);
  IReadOnlyList<string> GetAvailableCommands();
  IReadOnlyDictionary<string, string> PathMappings { get; }
}
