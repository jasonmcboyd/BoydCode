using System.Diagnostics;
using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Tools;

namespace BoydCode.Infrastructure.Tools.Tools;

public sealed class WriteTool : ITool
{
  private readonly IDirectoryGuard _directoryGuard;

  public WriteTool(IDirectoryGuard directoryGuard)
  {
    _directoryGuard = directoryGuard;
  }

  public ToolDefinition Definition { get; } = new(
      "Write",
      "Write content to a file. Creates parent directories if needed and overwrites existing files.",
      ToolCategory.FileWrite,
      [
          new ToolParameter("file_path", "string", "Absolute path to the file to write", Required: true),
            new ToolParameter("content", "string", "The content to write to the file", Required: true),
      ]);

  public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, string workingDirectory, CancellationToken ct)
  {
    var sw = Stopwatch.StartNew();
    try
    {
      using var doc = JsonDocument.Parse(argumentsJson);
      var root = doc.RootElement;

      var filePath = root.GetProperty("file_path").GetString()
          ?? throw new ArgumentException("file_path is required");
      var content = root.GetProperty("content").GetString()
          ?? throw new ArgumentException("content is required");

      if (!Path.IsPathRooted(filePath))
      {
        filePath = Path.GetFullPath(filePath, workingDirectory);
      }

      var accessLevel = _directoryGuard.GetAccessLevel(filePath);
      if (accessLevel == DirectoryAccessLevel.None)
      {
        return new ToolExecutionResult($"Access denied: '{filePath}' is outside project scope.", IsError: true, Duration: sw.Elapsed);
      }
      if (accessLevel == DirectoryAccessLevel.ReadOnly)
      {
        return new ToolExecutionResult($"Access denied: '{filePath}' is in a read-only project directory.", IsError: true, Duration: sw.Elapsed);
      }

      var directory = Path.GetDirectoryName(filePath);
      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
      }

      await File.WriteAllTextAsync(filePath, content, ct);

      sw.Stop();
      return new ToolExecutionResult($"Successfully wrote {content.Length} characters to {filePath}", Duration: sw.Elapsed);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      sw.Stop();
      return new ToolExecutionResult($"Error writing file: {ex.Message}", IsError: true, Duration: sw.Elapsed);
    }
  }
}
