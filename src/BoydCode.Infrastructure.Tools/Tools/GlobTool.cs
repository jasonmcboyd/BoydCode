using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Tools;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace BoydCode.Infrastructure.Tools.Tools;

public sealed class GlobTool : ITool
{
  private readonly IDirectoryGuard _directoryGuard;

  public GlobTool(IDirectoryGuard directoryGuard)
  {
    _directoryGuard = directoryGuard;
  }

  public ToolDefinition Definition { get; } = new(
      "Glob",
      "Find files matching a glob pattern. Returns matching file paths sorted by modification time.",
      ToolCategory.Search,
      [
          new ToolParameter("pattern", "string", "The glob pattern to match files against (e.g. \"**/*.cs\")", Required: true),
            new ToolParameter("path", "string", "The directory to search in. Defaults to working directory.", Required: false),
      ]);

  public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, string workingDirectory, CancellationToken ct)
  {
    var sw = Stopwatch.StartNew();
    try
    {
      using var doc = JsonDocument.Parse(argumentsJson);
      var root = doc.RootElement;

      var pattern = root.GetProperty("pattern").GetString()
          ?? throw new ArgumentException("pattern is required");

      var searchPath = root.TryGetProperty("path", out var pathProp)
          ? pathProp.GetString() ?? workingDirectory
          : workingDirectory;

      if (!Path.IsPathRooted(searchPath))
      {
        searchPath = Path.GetFullPath(searchPath, workingDirectory);
      }

      var accessLevel = _directoryGuard.GetAccessLevel(searchPath);
      if (accessLevel == DirectoryAccessLevel.None)
      {
        sw.Stop();
        return Task.FromResult(
            new ToolExecutionResult($"Access denied: '{searchPath}' is outside project scope.", IsError: true, sw.Elapsed));
      }

      if (!Directory.Exists(searchPath))
      {
        sw.Stop();
        return Task.FromResult(
            new ToolExecutionResult($"Directory not found: {searchPath}", IsError: true, sw.Elapsed));
      }

      var matcher = new Matcher();
      matcher.AddInclude(pattern);

      var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(searchPath));
      var result = matcher.Execute(directoryInfo);

      if (!result.HasMatches)
      {
        sw.Stop();
        return Task.FromResult(
            new ToolExecutionResult("No files found", Duration: sw.Elapsed));
      }

      var files = result.Files
          .Select(f => Path.Combine(searchPath, f.Path))
          .Where(File.Exists)
          .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
          .ToList();

      var sb = new StringBuilder();
      foreach (var file in files)
      {
        ct.ThrowIfCancellationRequested();
        sb.AppendLine(file);
      }

      sw.Stop();
      return Task.FromResult(
          new ToolExecutionResult(sb.ToString(), Duration: sw.Elapsed));
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      sw.Stop();
      return Task.FromResult(
          new ToolExecutionResult($"Error searching files: {ex.Message}", IsError: true, Duration: sw.Elapsed));
    }
  }
}
