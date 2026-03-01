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

    // Show a truncated preview of arguments
    var preview = argumentsJson.Length > 200 ? argumentsJson[..200] + "..." : argumentsJson;
    AnsiConsole.Write(new Panel(Markup.Escape(preview))
        .Header($"[yellow]{Markup.Escape(tool.Name)}[/]")
        .BorderColor(Color.Yellow));

    var result = AnsiConsole.Confirm("[yellow]Allow?[/]", defaultValue: true);
    return Task.FromResult(result);
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
