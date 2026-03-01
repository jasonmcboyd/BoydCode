using System.Collections.Concurrent;
using System.Globalization;
using BoydCode.Presentation.Console.Terminal;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace BoydCode.Presentation.Console;

internal static class SpectreHelpers
{
  // ──────────────────────────────────────────────
  //  VIM-STYLE KEY REMAPPING (j/k → ↓/↑)
  // ──────────────────────────────────────────────

  private static readonly Lazy<VimAnsiConsole> s_vimConsole =
    new(() => new VimAnsiConsole(AnsiConsole.Console));

  private sealed class VimConsoleInput : IAnsiConsoleInput
  {
    private readonly IAnsiConsoleInput _inner;
    private readonly ConcurrentQueue<ConsoleKeyInfo> _buffer = new();

    public VimConsoleInput(IAnsiConsoleInput inner)
    {
      _inner = inner;
    }

    public void PreloadDownArrows(int count)
    {
      for (var i = 0; i < count; i++)
      {
        _buffer.Enqueue(new ConsoleKeyInfo(
          '\0', ConsoleKey.DownArrow, shift: false, alt: false, control: false));
      }
    }

    public bool IsKeyAvailable()
    {
      return !_buffer.IsEmpty || _inner.IsKeyAvailable();
    }

    public ConsoleKeyInfo? ReadKey(bool intercept)
    {
      if (_buffer.TryDequeue(out var buffered))
      {
        return buffered;
      }

      var key = _inner.ReadKey(intercept);
      return key.HasValue ? RemapVimKey(key.Value) : null;
    }

    public async Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
    {
      if (_buffer.TryDequeue(out var buffered))
      {
        return buffered;
      }

      var key = await _inner.ReadKeyAsync(intercept, cancellationToken);
      return key.HasValue ? RemapVimKey(key.Value) : null;
    }

    private static ConsoleKeyInfo RemapVimKey(ConsoleKeyInfo key)
    {
      if (key.Modifiers != 0)
      {
        return key;
      }

      return key.Key switch
      {
        ConsoleKey.J => new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, shift: false, alt: false, control: false),
        ConsoleKey.K => new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, shift: false, alt: false, control: false),
        _ => key,
      };
    }
  }

  private sealed class VimAnsiConsole : IAnsiConsole
  {
    private readonly IAnsiConsole _inner;

    public VimAnsiConsole(IAnsiConsole inner)
    {
      _inner = inner;
      VimInput = new VimConsoleInput(inner.Input);
    }

    public VimConsoleInput VimInput { get; }

    public Profile Profile => _inner.Profile;
    public IAnsiConsoleCursor Cursor => _inner.Cursor;
    public IAnsiConsoleInput Input => VimInput;
    public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;
    public RenderPipeline Pipeline => _inner.Pipeline;

    public void Clear(bool home) => _inner.Clear(home);
    public void Write(IRenderable renderable) => _inner.Write(renderable);
  }

  // ──────────────────────────────────────────────
  //  LAYOUT SUSPEND/RESUME HELPERS
  // ──────────────────────────────────────────────

  private static void SuspendLayout() => TerminalLayout.Current?.Suspend();

  private static void ResumeLayout() => TerminalLayout.Current?.Resume();

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

  public static string PromptNonEmpty(string label)
  {
    SuspendLayout();
    try
    {
      return AnsiConsole.Prompt(
        new TextPrompt<string>(label)
          .Validate(v => !string.IsNullOrWhiteSpace(v)
            ? ValidationResult.Success()
            : ValidationResult.Error("Value cannot be empty")));
    }
    finally
    {
      ResumeLayout();
    }
  }

  public static string PromptOptional(string label)
  {
    SuspendLayout();
    try
    {
      return AnsiConsole.Prompt(
        new TextPrompt<string>(label)
          .AllowEmpty());
    }
    finally
    {
      ResumeLayout();
    }
  }

  public static string PromptSecret(string label, bool allowEmpty = false)
  {
    SuspendLayout();
    try
    {
      var prompt = new TextPrompt<string>(label).Secret();
      if (allowEmpty) prompt.AllowEmpty();
      return AnsiConsole.Prompt(prompt);
    }
    finally
    {
      ResumeLayout();
    }
  }

  public static string PromptWithDefault(string label, string defaultValue)
  {
    SuspendLayout();
    try
    {
      return AnsiConsole.Prompt(
        new TextPrompt<string>(label)
          .DefaultValue(defaultValue)
          .ShowDefaultValue());
    }
    finally
    {
      ResumeLayout();
    }
  }

  public static T Ask<T>(string label)
  {
    SuspendLayout();
    try
    {
      return AnsiConsole.Ask<T>(label);
    }
    finally
    {
      ResumeLayout();
    }
  }

  public static string Select(string title, IEnumerable<string> choices, int defaultIndex = 0)
  {
    SuspendLayout();
    try
    {
      return SelectCore(
        new SelectionPrompt<string>()
          .Title(title)
          .AddChoices(choices)
          .HighlightStyle(new Style(Color.Green)),
        defaultIndex);
    }
    finally
    {
      ResumeLayout();
    }
  }

  public static T Select<T>(string title, IEnumerable<T> choices, int defaultIndex = 0) where T : notnull
  {
    SuspendLayout();
    try
    {
      return SelectCore(
        new SelectionPrompt<T>()
          .Title(title)
          .AddChoices(choices)
          .HighlightStyle(new Style(Color.Green)),
        defaultIndex);
    }
    finally
    {
      ResumeLayout();
    }
  }

  public static List<T> MultiSelect<T>(string title, IEnumerable<T> choices) where T : notnull
  {
    SuspendLayout();
    try
    {
      return AnsiConsole.Prompt(
        new MultiSelectionPrompt<T>()
          .Title(title)
          .NotRequired()
          .AddChoices(choices)
          .HighlightStyle(new Style(Color.Green)));
    }
    finally
    {
      ResumeLayout();
    }
  }

  private static T SelectCore<T>(SelectionPrompt<T> prompt, int defaultIndex) where T : notnull
  {
    var vim = s_vimConsole.Value;
    if (defaultIndex > 0)
    {
      vim.VimInput.PreloadDownArrows(defaultIndex);
    }

    return prompt.Show(vim);
  }

  public static bool Confirm(string prompt, bool defaultValue = true)
  {
    SuspendLayout();
    try
    {
      return AnsiConsole.Confirm(prompt, defaultValue);
    }
    finally
    {
      ResumeLayout();
    }
  }

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

  internal static Grid InfoGrid()
  {
    var grid = new Grid();
    grid.AddColumn(new GridColumn().PadLeft(2).PadRight(1).NoWrap());
    grid.AddColumn(new GridColumn().PadRight(4));
    grid.AddColumn(new GridColumn().PadRight(1).NoWrap());
    grid.AddColumn(new GridColumn());
    return grid;
  }

  internal static void AddInfoRow(Grid grid, string label, string value)
  {
    grid.AddRow(
        new Markup($"[dim]{Markup.Escape(label)}[/]"),
        new Markup($"[cyan]{Markup.Escape(value)}[/]"),
        new Markup(""),
        new Markup(""));
  }

  internal static void AddInfoRow(Grid grid, string label1, string value1, string label2, string value2)
  {
    grid.AddRow(
        new Markup($"[dim]{Markup.Escape(label1)}[/]"),
        new Markup($"[cyan]{Markup.Escape(value1)}[/]"),
        new Markup($"[dim]{Markup.Escape(label2)}[/]"),
        new Markup($"[cyan]{Markup.Escape(value2)}[/]"));
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
