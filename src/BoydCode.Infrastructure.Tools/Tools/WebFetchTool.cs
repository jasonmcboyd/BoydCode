using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Tools;

namespace BoydCode.Infrastructure.Tools.Tools;

public sealed partial class WebFetchTool : ITool
{
  private readonly IHttpClientFactory _httpClientFactory;

  public WebFetchTool(IHttpClientFactory httpClientFactory)
  {
    _httpClientFactory = httpClientFactory;
  }

  public ToolDefinition Definition { get; } = new(
      "WebFetch",
      "Fetch the contents of a URL. HTML tags are stripped and text content is returned.",
      ToolCategory.Web,
      [
          new ToolParameter("url", "string", "The URL to fetch", Required: true),
            new ToolParameter("prompt", "string", "Description of what to extract from the page", Required: false),
      ]);

  public async Task<ToolExecutionResult> ExecuteAsync(string argumentsJson, string workingDirectory, CancellationToken ct)
  {
    var sw = Stopwatch.StartNew();
    try
    {
      using var doc = JsonDocument.Parse(argumentsJson);
      var root = doc.RootElement;

      var url = root.GetProperty("url").GetString()
          ?? throw new ArgumentException("url is required");

      if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
          || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
      {
        sw.Stop();
        return new ToolExecutionResult(
            $"Invalid URL: {url}. Only http and https URLs are supported.",
            IsError: true,
            Duration: sw.Elapsed);
      }

      using var client = _httpClientFactory.CreateClient("WebFetch");
      client.Timeout = TimeSpan.FromSeconds(30);
      client.DefaultRequestHeaders.UserAgent.ParseAdd("BoydCode/1.0");

      var response = await client.GetAsync(uri, ct);
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync(ct);

      var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
      if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
      {
        content = StripHtmlTags(content);
      }

      if (content.Length > 50_000)
      {
        content = string.Concat(content.AsSpan(0, 49_997), "...");
      }

      sw.Stop();
      return new ToolExecutionResult(content, Duration: sw.Elapsed);
    }
    catch (HttpRequestException ex)
    {
      sw.Stop();
      return new ToolExecutionResult($"HTTP error: {ex.Message}", IsError: true, Duration: sw.Elapsed);
    }
    catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
    {
      sw.Stop();
      return new ToolExecutionResult($"Request timed out: {ex.Message}", IsError: true, Duration: sw.Elapsed);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      sw.Stop();
      return new ToolExecutionResult($"Error fetching URL: {ex.Message}", IsError: true, Duration: sw.Elapsed);
    }
  }

  private static string StripHtmlTags(string html)
  {
    // Remove script and style blocks entirely
    var cleaned = ScriptPattern().Replace(html, " ");
    cleaned = StylePattern().Replace(cleaned, " ");

    // Remove all HTML tags
    cleaned = TagPattern().Replace(cleaned, " ");

    // Decode common HTML entities
    cleaned = cleaned
        .Replace("&amp;", "&")
        .Replace("&lt;", "<")
        .Replace("&gt;", ">")
        .Replace("&quot;", "\"")
        .Replace("&apos;", "'")
        .Replace("&#39;", "'")
        .Replace("&nbsp;", " ");

    // Collapse multiple whitespace
    cleaned = WhitespacePattern().Replace(cleaned, " ");

    // Collapse multiple newlines
    cleaned = MultipleNewlinePattern().Replace(cleaned.Trim(), "\n\n");

    return cleaned;
  }

  [GeneratedRegex(@"<script[\s\S]*?</script>", RegexOptions.IgnoreCase)]
  private static partial Regex ScriptPattern();

  [GeneratedRegex(@"<style[\s\S]*?</style>", RegexOptions.IgnoreCase)]
  private static partial Regex StylePattern();

  [GeneratedRegex(@"<[^>]+>")]
  private static partial Regex TagPattern();

  [GeneratedRegex(@"[ \t]+")]
  private static partial Regex WhitespacePattern();

  [GeneratedRegex(@"\n{3,}")]
  private static partial Regex MultipleNewlinePattern();
}
