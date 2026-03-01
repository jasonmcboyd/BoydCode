using BoydCode.Application.Interfaces;
using BoydCode.Domain.Tools;
using Spectre.Console;

namespace BoydCode.Presentation.Console;

public sealed class SpectreUserInterface : IUserInterface
{
  private readonly IAnsiConsole _stderr = AnsiConsole.Create(
      new AnsiConsoleSettings { Out = new AnsiConsoleOutput(System.Console.Error) });

  private bool _streamingStarted;
  private bool _isThinking;

  public bool IsInteractive => AnsiConsole.Profile.Capabilities.Interactive;

  public string? StatusLine { get; set; }

  public Task<string> GetUserInputAsync(CancellationToken ct = default)
  {
    if (!IsInteractive)
    {
      var line = System.Console.ReadLine();
      return Task.FromResult(line ?? "/quit");
    }

    if (StatusLine is not null)
    {
      AnsiConsole.MarkupLine($"[dim]{Markup.Escape(StatusLine)}[/]");
    }

    var input = AnsiConsole.Prompt(
        new TextPrompt<string>("[bold blue]>[/]")
            .AllowEmpty());
    return Task.FromResult(input);
  }

  public Task<bool> RequestPermissionAsync(ToolDefinition tool, string argumentsJson, CancellationToken ct = default)
  {
    AnsiConsole.MarkupLine($"[yellow]Tool [bold]{Markup.Escape(tool.Name)}[/] wants to execute:[/]");

    var preview = FormatToolPreview(tool.Name, argumentsJson);

    AnsiConsole.Write(new Panel(Markup.Escape(preview))
        .Header($"[yellow]{Markup.Escape(tool.Name)}[/]")
        .BorderColor(Color.Yellow));

    var result = AnsiConsole.Confirm("[yellow]Allow?[/]", defaultValue: true);
    return Task.FromResult(result);
  }

  private static string FormatToolPreview(string toolName, string argumentsJson)
  {
    try
    {
      using var doc = System.Text.Json.JsonDocument.Parse(argumentsJson);
      var root = doc.RootElement;

      return toolName switch
      {
        "PowerShell" => FormatPowerShell(root),
        "Read" => FormatRead(root),
        "Write" => FormatWrite(root),
        "Edit" => FormatEdit(root),
        "Glob" => FormatGlob(root),
        "Grep" => FormatGrep(root),
        "WebFetch" => GetStringProperty(root, "url") ?? argumentsJson,
        "WebSearch" => GetStringProperty(root, "query") ?? argumentsJson,
        _ => FormatGeneric(root),
      };
    }
    catch
    {
      return argumentsJson.Length > 200 ? argumentsJson[..200] + "..." : argumentsJson;
    }
  }

  private static string FormatPowerShell(System.Text.Json.JsonElement root)
  {
    var command = GetStringProperty(root, "command");
    if (command is null) return FormatGeneric(root);

    var segments = command.Split("; ");
    return string.Join(Environment.NewLine, segments);
  }

  private static string FormatRead(System.Text.Json.JsonElement root)
  {
    var path = GetStringProperty(root, "file_path") ?? GetStringProperty(root, "path");
    if (path is null) return FormatGeneric(root);

    var suffix = "";
    var hasOffset = root.TryGetProperty("offset", out var offsetEl);
    var hasLimit = root.TryGetProperty("limit", out var limitEl);

    if (hasOffset || hasLimit)
    {
      var offset = hasOffset ? offsetEl.ToString() : "0";
      if (hasLimit)
      {
        suffix = $" (lines {offset}-{limitEl})";
      }
      else
      {
        suffix = $" (lines {offset}-)";
      }
    }

    return $"{path}{suffix}";
  }

  private static string FormatWrite(System.Text.Json.JsonElement root)
  {
    var path = GetStringProperty(root, "file_path") ?? GetStringProperty(root, "path");
    if (path is null) return FormatGeneric(root);

    var content = GetStringProperty(root, "content");
    var charCount = content?.Length ?? 0;
    return $"{path} ({charCount} chars)";
  }

  private static string FormatEdit(System.Text.Json.JsonElement root)
  {
    var path = GetStringProperty(root, "file_path") ?? GetStringProperty(root, "path");
    if (path is null) return FormatGeneric(root);

    var oldStr = GetStringProperty(root, "old_string") ?? "";
    var newStr = GetStringProperty(root, "new_string") ?? "";

    if (oldStr.Length > 60) oldStr = oldStr[..57] + "...";
    if (newStr.Length > 60) newStr = newStr[..57] + "...";

    return $"{path}{Environment.NewLine}{Environment.NewLine}- {oldStr}{Environment.NewLine}+ {newStr}";
  }

  private static string FormatGlob(System.Text.Json.JsonElement root)
  {
    var pattern = GetStringProperty(root, "pattern");
    if (pattern is null) return FormatGeneric(root);

    var path = GetStringProperty(root, "path");
    return path is not null
        ? $"{pattern}{Environment.NewLine}Path: {path}"
        : pattern;
  }

  private static string FormatGrep(System.Text.Json.JsonElement root)
  {
    var pattern = GetStringProperty(root, "pattern");
    if (pattern is null) return FormatGeneric(root);

    var lines = new List<string> { $"Pattern: {pattern}" };

    var path = GetStringProperty(root, "path");
    if (path is not null) lines.Add($"Path: {path}");

    var glob = GetStringProperty(root, "glob");
    if (glob is not null) lines.Add($"Glob: {glob}");

    return string.Join(Environment.NewLine, lines);
  }

  private static string FormatGeneric(System.Text.Json.JsonElement root)
  {
    var lines = new List<string>();
    foreach (var property in root.EnumerateObject())
    {
      var value = property.Value.ValueKind == System.Text.Json.JsonValueKind.String
          ? property.Value.GetString() ?? ""
          : property.Value.ToString();

      if (value.Length > 100) value = value[..97] + "...";
      lines.Add($"{property.Name}: {value}");
    }

    return string.Join(Environment.NewLine, lines);
  }

  private static string? GetStringProperty(System.Text.Json.JsonElement root, string name)
  {
    return root.TryGetProperty(name, out var prop) ? prop.GetString() : null;
  }

  public void RenderAssistantText(string text)
  {
    // Render as markdown-ish content with a left border
    AnsiConsole.Write(new Panel(Markup.Escape(text))
        .Border(BoxBorder.None)
        .PadLeft(1));
    AnsiConsole.WriteLine();
  }

  public void RenderStreamingToken(string token)
  {
    if (!_streamingStarted)
    {
      System.Console.Write("  ");
      _streamingStarted = true;
    }

    System.Console.Write(token);
  }

  public void RenderStreamingComplete()
  {
    _streamingStarted = false;
    System.Console.WriteLine();
    System.Console.WriteLine();
  }

  public void RenderThinkingStart()
  {
    _isThinking = true;
    AnsiConsole.Markup("[dim italic]  Thinking...[/]");
  }

  public void RenderThinkingStop()
  {
    if (!_isThinking) return;
    _isThinking = false;
    System.Console.Write("\r                    \r");
  }

  public void RenderToolExecution(string toolName, string argumentsSummary)
  {
    AnsiConsole.MarkupLine($"  [dim][[{Markup.Escape(toolName)}]][/] [dim]{Markup.Escape(argumentsSummary)}[/]");
  }

  public void RenderToolResult(string toolName, string result, bool isError)
  {
    if (isError)
    {
      AnsiConsole.MarkupLine($"  [red][[{Markup.Escape(toolName)} error]][/] {Markup.Escape(Truncate(result, 500))}");
    }
    else
    {
      // Show a brief summary of successful tool results
      var summary = Truncate(result, 200);
      AnsiConsole.MarkupLine($"  [green][[{Markup.Escape(toolName)}]][/] [dim]{Markup.Escape(summary)}[/]");
    }
  }

  public void RenderError(string message)
  {
    var suggestionMarker = "\n  Suggestion: ";
    var markerIndex = message.IndexOf(suggestionMarker, StringComparison.Ordinal);

    if (markerIndex >= 0)
    {
      var errorPart = message[..markerIndex];
      var suggestion = message[(markerIndex + suggestionMarker.Length)..];
      _stderr.MarkupLine($"[red bold]Error:[/] [red]{Markup.Escape(errorPart)}[/]");
      _stderr.MarkupLine($"  [yellow]Suggestion:[/] [dim]{Markup.Escape(suggestion)}[/]");
    }
    else
    {
      _stderr.MarkupLine($"[red bold]Error:[/] [red]{Markup.Escape(message)}[/]");
    }
  }

  public void RenderHint(string hint)
  {
    AnsiConsole.MarkupLine($"  [dim italic]{Markup.Escape(hint)}[/]");
    AnsiConsole.WriteLine();
  }

  public void RenderSuccess(string message) => SpectreHelpers.Success(message);

  public void RenderWarning(string message) => SpectreHelpers.Warning(message);

  public void RenderSection(string title) => SpectreHelpers.Section(title);

  public void RenderTokenUsage(int inputTokens, int outputTokens)
  {
    AnsiConsole.MarkupLine(
        $"[dim]Tokens: {inputTokens.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)} in / " +
        $"{outputTokens.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)} out / " +
        $"{(inputTokens + outputTokens).ToString("N0", System.Globalization.CultureInfo.CurrentCulture)} total[/]");
  }

  public void RenderWelcome(string model, string workingDirectory)
  {
    AnsiConsole.Write(new FigletText("BoydCode").Color(Color.Blue));
    AnsiConsole.MarkupLine("[bold]AI Coding Assistant with JEA-Constrained PowerShell[/]");
    AnsiConsole.MarkupLine($"Model: [cyan]{Markup.Escape(model)}[/]");
    AnsiConsole.MarkupLine($"Working directory: [cyan]{Markup.Escape(workingDirectory)}[/]");
    AnsiConsole.MarkupLine("[dim]Type /quit to exit. Commands execute in a constrained PowerShell runspace.[/]");
    AnsiConsole.WriteLine();
  }

  public void RenderMarkdown(string markdown)
  {
    // For now, render as escaped text. Phase 3 will add proper markdown rendering.
    AnsiConsole.Write(new Panel(Markup.Escape(markdown)).Border(BoxBorder.Rounded));
  }

  private static string Truncate(string text, int maxLength)
  {
    if (text.Length <= maxLength)
    {
      return text;
    }

    return text[..maxLength] + "...";
  }
}
