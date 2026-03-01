using System.Diagnostics;
using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Tools;

namespace BoydCode.Infrastructure.Tools.Tools;

public sealed class EditTool : ITool
{
  private readonly IDirectoryGuard _directoryGuard;

  public EditTool(IDirectoryGuard directoryGuard)
  {
    _directoryGuard = directoryGuard;
  }

  public ToolDefinition Definition { get; } = new(
      "Edit",
      "Perform exact string replacement in a file. The old_string must be unique unless replace_all is true.",
      ToolCategory.FileWrite,
      [
          new ToolParameter("file_path", "string", "Absolute path to the file to edit", Required: true),
            new ToolParameter("old_string", "string", "The exact text to find and replace", Required: true),
            new ToolParameter("new_string", "string", "The replacement text", Required: true),
            new ToolParameter("replace_all", "boolean", "Replace all occurrences (default false)", Required: false),
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
      var oldString = root.GetProperty("old_string").GetString()
          ?? throw new ArgumentException("old_string is required");
      var newString = root.GetProperty("new_string").GetString()
          ?? throw new ArgumentException("new_string is required");
      var replaceAll = root.TryGetProperty("replace_all", out var raProp) && raProp.GetBoolean();

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

      if (!File.Exists(filePath))
      {
        return new ToolExecutionResult($"File not found: {filePath}", IsError: true, sw.Elapsed);
      }

      var content = await File.ReadAllTextAsync(filePath, ct);

      var occurrences = CountOccurrences(content, oldString);

      if (occurrences == 0)
      {
        sw.Stop();
        return new ToolExecutionResult(
            "old_string not found in file. Make sure it matches exactly, including whitespace and indentation.",
            IsError: true,
            Duration: sw.Elapsed);
      }

      if (!replaceAll && occurrences > 1)
      {
        sw.Stop();
        return new ToolExecutionResult(
            $"old_string found {occurrences} times. Provide more context to make it unique, or set replace_all to true.",
            IsError: true,
            Duration: sw.Elapsed);
      }

      var updatedContent = replaceAll
          ? content.Replace(oldString, newString, StringComparison.Ordinal)
          : ReplaceFirst(content, oldString, newString);

      await File.WriteAllTextAsync(filePath, updatedContent, ct);

      sw.Stop();
      var replacementCount = replaceAll ? occurrences : 1;
      return new ToolExecutionResult(
          $"Successfully replaced {replacementCount} occurrence(s) in {filePath}",
          Duration: sw.Elapsed);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      sw.Stop();
      return new ToolExecutionResult($"Error editing file: {ex.Message}", IsError: true, Duration: sw.Elapsed);
    }
  }

  private static int CountOccurrences(string text, string search)
  {
    var count = 0;
    var index = 0;
    while ((index = text.IndexOf(search, index, StringComparison.Ordinal)) != -1)
    {
      count++;
      index += search.Length;
    }

    return count;
  }

  private static string ReplaceFirst(string text, string search, string replacement)
  {
    var index = text.IndexOf(search, StringComparison.Ordinal);
    if (index < 0)
    {
      return text;
    }

    return string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + search.Length));
  }
}
