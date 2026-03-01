using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Tools;

namespace BoydCode.Infrastructure.Tools.Tools;

public sealed class PowerShellTool : ITool
{
  private readonly ActiveExecutionEngine _activeEngine;

  public PowerShellTool(ActiveExecutionEngine activeEngine)
  {
    _activeEngine = activeEngine;
  }

  public ToolDefinition Definition { get; } = new(
      "PowerShell",
      "Execute a PowerShell command in a constrained execution engine. Only whitelisted commands are available.",
      ToolCategory.Shell,
      [
          new ToolParameter("command", "string", "The PowerShell command to execute", Required: true),
            new ToolParameter("timeout", "integer", "Timeout in milliseconds (default 120000)", Required: false),
      ]);

  public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, string workingDirectory, CancellationToken ct)
  {
    if (!_activeEngine.IsInitialized)
    {
      return new ToolExecutionResult("Error: Execution engine not initialized.", IsError: true);
    }

    try
    {
      using var doc = JsonDocument.Parse(argumentsJson);
      var root = doc.RootElement;

      var command = root.GetProperty("command").GetString()
          ?? throw new ArgumentException("command is required");
      var timeout = root.TryGetProperty("timeout", out var toProp) ? toProp.GetInt32() : 120_000;

      var result = await _activeEngine.Engine!.ExecuteAsync(command, workingDirectory, timeout, ct);

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

      return new ToolExecutionResult(output, result.HadErrors, result.Duration);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      return new ToolExecutionResult($"Error executing PowerShell: {ex.Message}", IsError: true);
    }
  }
}
