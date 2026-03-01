using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Tools;

namespace BoydCode.Infrastructure.Tools.Tools;

public sealed class ReadTool : ITool
{
  private const int MaxLineLength = 2000;
  private readonly IDirectoryGuard _directoryGuard;

  public ReadTool(IDirectoryGuard directoryGuard)
  {
    _directoryGuard = directoryGuard;
  }

  public ToolDefinition Definition { get; } = new(
      "Read",
      "Read the contents of a file. Returns the file content with line numbers.",
      ToolCategory.FileRead,
      [
          new ToolParameter("file_path", "string", "Absolute path to the file to read", Required: true),
            new ToolParameter("offset", "integer", "Line number to start reading from (1-based)", Required: false),
            new ToolParameter("limit", "integer", "Maximum number of lines to read", Required: false),
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

      if (!Path.IsPathRooted(filePath))
      {
        filePath = Path.GetFullPath(filePath, workingDirectory);
      }

      var accessLevel = _directoryGuard.GetAccessLevel(filePath);
      if (accessLevel == DirectoryAccessLevel.None)
      {
        return new ToolExecutionResult($"Access denied: '{filePath}' is outside project scope.", IsError: true, Duration: sw.Elapsed);
      }

      if (!File.Exists(filePath))
      {
        return new ToolExecutionResult($"File not found: {filePath}", IsError: true, sw.Elapsed);
      }

      var lines = await File.ReadAllLinesAsync(filePath, ct);

      var offset = root.TryGetProperty("offset", out var offProp) ? offProp.GetInt32() : 1;
      var limit = root.TryGetProperty("limit", out var limProp) ? limProp.GetInt32() : lines.Length;

      var startIndex = Math.Max(0, offset - 1);
      var endIndex = Math.Min(lines.Length, startIndex + limit);

      var sb = new StringBuilder();
      for (var i = startIndex; i < endIndex; i++)
      {
        var lineNum = i + 1;
        var line = lines[i].Length > MaxLineLength
            ? string.Concat(lines[i].AsSpan(0, MaxLineLength), "... (truncated)")
            : lines[i];
        sb.AppendLine(CultureInfo.InvariantCulture, $"{lineNum,6}\t{line}");
      }

      sw.Stop();
      return new ToolExecutionResult(sb.ToString(), Duration: sw.Elapsed);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      sw.Stop();
      return new ToolExecutionResult($"Error reading file: {ex.Message}", IsError: true, Duration: sw.Elapsed);
    }
  }
}
