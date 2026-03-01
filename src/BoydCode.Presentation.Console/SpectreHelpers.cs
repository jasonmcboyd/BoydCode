using System.Globalization;
using Spectre.Console;

namespace BoydCode.Presentation.Console;

internal static class SpectreHelpers
{
  // ──────────────────────────────────────────────
  //  STATUS MESSAGES (escape internally)
  // ──────────────────────────────────────────────

  public static void Success(string message) =>
    AnsiConsole.MarkupLine($"  [green]v[/] {Markup.Escape(message)}");

  public static void Error(string message) =>
    AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");

  public static void Warning(string message) =>
    AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(message)}");

  public static void Usage(string message) =>
    AnsiConsole.MarkupLine($"[yellow]Usage:[/] {Markup.Escape(message)}");

  public static void Dim(string message) =>
    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(message)}[/]");

  public static void Cancelled() =>
    AnsiConsole.MarkupLine("[dim]Cancelled.[/]");

  // ──────────────────────────────────────────────
  //  SECTION DIVIDER
  // ──────────────────────────────────────────────

  public static void Section(string title)
  {
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(title)}[/]").LeftJustified().RuleStyle("dim"));
  }

  // ──────────────────────────────────────────────
  //  PROMPTS (labels are developer-authored markup)
  // ──────────────────────────────────────────────

  public static string PromptNonEmpty(string label) =>
    AnsiConsole.Prompt(
      new TextPrompt<string>(label)
        .Validate(v => !string.IsNullOrWhiteSpace(v)
          ? ValidationResult.Success()
          : ValidationResult.Error("Value cannot be empty")));

  public static string PromptOptional(string label) =>
    AnsiConsole.Prompt(
      new TextPrompt<string>(label)
        .AllowEmpty());

  public static string Select(string title, IEnumerable<string> choices) =>
    AnsiConsole.Prompt(
      new SelectionPrompt<string>()
        .Title(title)
        .AddChoices(choices)
        .HighlightStyle(new Style(Color.Green)));

  public static T Select<T>(string title, IEnumerable<T> choices) where T : notnull =>
    AnsiConsole.Prompt(
      new SelectionPrompt<T>()
        .Title(title)
        .AddChoices(choices)
        .HighlightStyle(new Style(Color.Green)));

  public static bool Confirm(string prompt, bool defaultValue = true) =>
    AnsiConsole.Confirm(prompt, defaultValue);

  // ──────────────────────────────────────────────
  //  TABLE FACTORY
  // ──────────────────────────────────────────────

  public static Table SimpleTable(params string[] headers)
  {
    var table = new Table().Border(TableBorder.Simple);
    foreach (var header in headers)
    {
      table.AddColumn(new TableColumn($"[bold]{Markup.Escape(header)}[/]"));
    }

    return table;
  }

  // ──────────────────────────────────────────────
  //  COMPACT FORMATTING
  // ──────────────────────────────────────────────

  internal static string FormatCompact(int value)
  {
    return value switch
    {
      >= 1_000_000 => $"{value / 1_000_000.0:F1}M",
      >= 1_000 => $"{value / 1_000.0:F1}k",
      _ => value.ToString(CultureInfo.InvariantCulture),
    };
  }

  internal static string FormatPercent(double value) =>
    $"{value:F1}%";
}
