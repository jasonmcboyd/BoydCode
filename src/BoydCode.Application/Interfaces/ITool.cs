using BoydCode.Domain.Tools;

namespace BoydCode.Application.Interfaces;

public interface ITool
{
  ToolDefinition Definition { get; }
  Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, string workingDirectory, CancellationToken ct = default);
}
