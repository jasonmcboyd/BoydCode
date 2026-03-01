using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Tools;

namespace BoydCode.Infrastructure.Tools.Tools;

public sealed class WebSearchTool : ITool
{
  public ToolDefinition Definition { get; } = new(
      "WebSearch",
      "Search the web for information. Returns search results matching the query.",
      ToolCategory.Web,
      [
          new ToolParameter("query", "string", "The search query", Required: true),
      ]);

  public Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, string workingDirectory, CancellationToken ct)
  {
    try
    {
      using var doc = JsonDocument.Parse(argumentsJson);
      var root = doc.RootElement;

      _ = root.GetProperty("query").GetString()
          ?? throw new ArgumentException("query is required");

      return Task.FromResult(new ToolExecutionResult(
          "Web search not configured. This feature will be available in a future release."));
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      return Task.FromResult(
          new ToolExecutionResult($"Error: {ex.Message}", IsError: true));
    }
  }
}
