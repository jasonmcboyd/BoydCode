using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Tools;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace BoydCode.Infrastructure.Tools.Tools;

public sealed class GrepTool : ITool
{
  private const string OutputModeContent = "content";
  private const string OutputModeFilesWithMatches = "files_with_matches";
  private const string OutputModeCount = "count";
  private readonly IDirectoryGuard _directoryGuard;

  public GrepTool(IDirectoryGuard directoryGuard)
  {
    _directoryGuard = directoryGuard;
  }

  public ToolDefinition Definition { get; } = new(
      "Grep",
      "Search file contents using regex. Recursively searches files and returns matching lines.",
      ToolCategory.Search,
      [
          new ToolParameter("pattern", "string", "The regular expression pattern to search for", Required: true),
            new ToolParameter("path", "string", "File or directory to search in. Defaults to working directory.", Required: false),
            new ToolParameter("glob", "string", "Glob pattern to filter files (e.g. \"*.cs\", \"**/*.json\")", Required: false),
            new ToolParameter("output_mode", "string", "Output mode: content, files_with_matches, or count",
                Required: false, EnumValues: [OutputModeContent, OutputModeFilesWithMatches, OutputModeCount]),
            new ToolParameter("context", "integer", "Number of context lines to show before and after each match", Required: false),
      ]);

  public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, string workingDirectory, CancellationToken ct)
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

      var globPattern = root.TryGetProperty("glob", out var globProp)
          ? globProp.GetString()
          : null;

      var outputMode = root.TryGetProperty("output_mode", out var modeProp)
          ? modeProp.GetString() ?? OutputModeFilesWithMatches
          : OutputModeFilesWithMatches;

      var contextLines = root.TryGetProperty("context", out var ctxProp) ? ctxProp.GetInt32() : 0;

      var accessLevel = _directoryGuard.GetAccessLevel(searchPath);
      if (accessLevel == DirectoryAccessLevel.None)
      {
        sw.Stop();
        return new ToolExecutionResult($"Access denied: '{searchPath}' is outside project scope.", IsError: true, Duration: sw.Elapsed);
      }

      var regex = new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(5));

      var files = GetFilesToSearch(searchPath, globPattern);

      var sb = new StringBuilder();
      var totalMatches = 0;

      foreach (var file in files)
      {
        ct.ThrowIfCancellationRequested();

        string[] lines;
        try
        {
          lines = await File.ReadAllLinesAsync(file, ct);
        }
        catch (IOException)
        {
          continue;
        }
        catch (UnauthorizedAccessException)
        {
          continue;
        }

        var matchingLineIndices = new List<int>();
        for (var i = 0; i < lines.Length; i++)
        {
          if (regex.IsMatch(lines[i]))
          {
            matchingLineIndices.Add(i);
          }
        }

        if (matchingLineIndices.Count == 0)
        {
          continue;
        }

        totalMatches += matchingLineIndices.Count;

        switch (outputMode)
        {
          case OutputModeFilesWithMatches:
            sb.AppendLine(file);
            break;

          case OutputModeCount:
            sb.AppendLine(CultureInfo.InvariantCulture, $"{file}:{matchingLineIndices.Count}");
            break;

          case OutputModeContent:
            AppendContentOutput(sb, file, lines, matchingLineIndices, contextLines);
            break;
        }
      }

      if (totalMatches == 0)
      {
        sw.Stop();
        return new ToolExecutionResult("No matches found", Duration: sw.Elapsed);
      }

      sw.Stop();
      return new ToolExecutionResult(sb.ToString(), Duration: sw.Elapsed);
    }
    catch (RegexParseException ex)
    {
      sw.Stop();
      return new ToolExecutionResult($"Invalid regex pattern: {ex.Message}", IsError: true, Duration: sw.Elapsed);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      sw.Stop();
      return new ToolExecutionResult($"Error searching: {ex.Message}", IsError: true, Duration: sw.Elapsed);
    }
  }

  private static List<string> GetFilesToSearch(string searchPath, string? globPattern)
  {
    if (File.Exists(searchPath))
    {
      return [searchPath];
    }

    if (!Directory.Exists(searchPath))
    {
      return [];
    }

    if (!string.IsNullOrEmpty(globPattern))
    {
      var matcher = new Matcher();
      matcher.AddInclude(globPattern);
      var dirInfo = new DirectoryInfoWrapper(new DirectoryInfo(searchPath));
      var result = matcher.Execute(dirInfo);
      return result.Files
          .Select(f => Path.Combine(searchPath, f.Path))
          .Where(File.Exists)
          .ToList();
    }

    return Directory.EnumerateFiles(searchPath, "*", SearchOption.AllDirectories)
        .Where(IsTextFile)
        .ToList();
  }

  private static bool IsTextFile(string filePath)
  {
    var extension = Path.GetExtension(filePath).ToUpperInvariant();
    return extension switch
    {
      ".EXE" or ".DLL" or ".PDB" or ".OBJ" or ".BIN" or ".ZIP" or ".GZ"
          or ".TAR" or ".RAR" or ".7Z" or ".PNG" or ".JPG" or ".JPEG"
          or ".GIF" or ".BMP" or ".ICO" or ".PDF" or ".WOFF" or ".WOFF2"
          or ".TTF" or ".EOT" or ".MP3" or ".MP4" or ".WAV" or ".AVI"
          or ".MOV" or ".NUPKG" or ".SNK" => false,
      _ => true,
    };
  }

  private static void AppendContentOutput(
      StringBuilder sb,
      string file,
      string[] lines,
      List<int> matchingLineIndices,
      int contextLines)
  {
    sb.AppendLine(file);

    var printedLines = new HashSet<int>();
    var previousEnd = -1;

    foreach (var matchIndex in matchingLineIndices)
    {
      var rangeStart = Math.Max(0, matchIndex - contextLines);
      var rangeEnd = Math.Min(lines.Length - 1, matchIndex + contextLines);

      if (previousEnd >= 0 && rangeStart > previousEnd + 1)
      {
        sb.AppendLine("--");
      }

      for (var i = rangeStart; i <= rangeEnd; i++)
      {
        if (printedLines.Add(i))
        {
          var lineNum = i + 1;
          var marker = i == matchIndex ? ":" : "-";
          sb.AppendLine(CultureInfo.InvariantCulture, $"{lineNum,6}{marker}{lines[i]}");
        }
      }

      previousEnd = rangeEnd;
    }

    sb.AppendLine();
  }
}
