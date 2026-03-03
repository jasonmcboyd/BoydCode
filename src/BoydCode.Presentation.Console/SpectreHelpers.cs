using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using BoydCode.Presentation.Console.Terminal;
using Spectre.Console;
using Spectre.Console.Rendering;
using TguiApp = Terminal.Gui.App.Application;

#pragma warning disable CS0618 // Application.Run/RequestStop/Invoke - using legacy static API during Terminal.Gui migration

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

  private static void SuspendLayout() => SpectreUserInterface.Current?.SuspendLayout();

  private static void ResumeLayout() => SpectreUserInterface.Current?.ResumeLayout();

  // ──────────────────────────────────────────────
  //  LAYOUT-AWARE OUTPUT
  // ──────────────────────────────────────────────

  internal static void OutputMarkup(string markup)
  {
    var ui = SpectreUserInterface.Current;
    if (ui is { IsSessionActive: true, Toplevel: not null })
    {
      var text = StripMarkup(markup);
      TguiApp.Invoke(() => ui.Toplevel?.ConversationView.AddBlock(new PlainTextBlock(text)));
    }
    else
    {
      AnsiConsole.MarkupLine(markup);
    }
  }

  internal static void OutputLine(string text = "")
  {
    var ui = SpectreUserInterface.Current;
    if (ui is { IsSessionActive: true, Toplevel: not null })
    {
      TguiApp.Invoke(() => ui.Toplevel?.ConversationView.AddBlock(new SeparatorBlock()));
    }
    else
    {
      if (text.Length > 0)
        AnsiConsole.WriteLine(text);
      else
        AnsiConsole.WriteLine();
    }
  }

  // ──────────────────────────────────────────────
  //  STATUS MESSAGES (escape internally)
  // ──────────────────────────────────────────────

  public static void Success(string message) =>
    OutputMarkup($"  [green]{Theme.Symbols.Check}[/] {Markup.Escape(message)}");

  public static void Error(string message) =>
    OutputMarkup($"[red bold]Error:[/] [red]{Markup.Escape(message)}[/]");

  public static void Warning(string message) =>
    OutputMarkup($"[yellow]Warning:[/] {Markup.Escape(message)}");

  public static void Usage(string message) =>
    OutputMarkup($"[yellow]Usage:[/] {Markup.Escape(message)}");

  public static void Dim(string message) =>
    OutputMarkup($"[dim]{Markup.Escape(message)}[/]");

  public static void Cancelled() =>
    OutputMarkup("[dim]Cancelled.[/]");

  // ──────────────────────────────────────────────
  //  SECTION DIVIDER
  // ──────────────────────────────────────────────

  public static void Section(string title)
  {
    var ui = SpectreUserInterface.Current;
    if (ui is { IsSessionActive: true, Toplevel: not null })
    {
      TguiApp.Invoke(() =>
      {
        ui.Toplevel?.ConversationView.AddBlock(new SeparatorBlock());
        ui.Toplevel?.ConversationView.AddBlock(new SectionBlock(title));
      });
    }
    else
    {
      AnsiConsole.WriteLine();
      AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(title)}[/]").LeftJustified().RuleStyle("dim"));
    }
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
          .HighlightStyle(new Style(Spectre.Console.Color.Green)),
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
          .HighlightStyle(new Style(Spectre.Console.Color.Green)),
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
          .HighlightStyle(new Style(Spectre.Console.Color.Green)));
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
  //  TEXT HELPERS
  // ──────────────────────────────────────────────

  /// <summary>
  /// Strip Spectre.Console markup tags from a string, returning plain text.
  /// </summary>
  private static string StripMarkup(string markup)
  {
    // Simple tag-stripping: remove [color], [/], [bold], etc.
    var result = System.Text.RegularExpressions.Regex.Replace(markup, @"\[/?[^\]]*\]", "");
    return result;
  }

  // ──────────────────────────────────────────────
  //  TERMINAL.GUI SELECTION DIALOG
  // ──────────────────────────────────────────────

  /// <summary>
  /// Shows a modal selection dialog with a ListView. Returns the selected item
  /// or null if cancelled. Requires the TUI to be active.
  /// </summary>
  internal static string? ShowSelectionDialog(string title, IReadOnlyList<string> items)
  {
    if (items.Count == 0)
    {
      return null;
    }

    var selected = (string?)null;
    var dialog = new global::Terminal.Gui.Views.Dialog
    {
      Title = title,
      Width = global::Terminal.Gui.ViewBase.Dim.Percent(50),
      Height = Math.Min(items.Count + 6, 20),
      BorderStyle = global::Terminal.Gui.Drawing.LineStyle.Rounded,
    };
    dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

    var listView = new global::Terminal.Gui.Views.ListView
    {
      X = 2,
      Y = 1,
      Width = global::Terminal.Gui.ViewBase.Dim.Fill(2),
      Height = global::Terminal.Gui.ViewBase.Dim.Fill(2),
    };
    listView.SetSource(new ObservableCollection<string>(items));
    dialog.Add(listView);

    var cancelButton = new global::Terminal.Gui.Views.Button { Text = "Cancel" };
    var okButton = new global::Terminal.Gui.Views.Button { Text = "Ok", IsDefault = true };
    dialog.AddButton(cancelButton);
    dialog.AddButton(okButton);

    okButton.Accepting += (s, e) =>
    {
      e.Handled = true;
      var index = listView.SelectedItem ?? -1;
      if (index >= 0 && index < items.Count)
      {
        selected = items[index];
      }
      TguiApp.RequestStop();
    };

    cancelButton.Accepting += (s, e) =>
    {
      e.Handled = true;
      TguiApp.RequestStop();
    };

    listView.Accepting += (s, e) =>
    {
      e.Handled = true;
      var index = listView.SelectedItem ?? -1;
      if (index >= 0 && index < items.Count)
      {
        selected = items[index];
      }
      TguiApp.RequestStop();
    };

    TguiApp.Run(dialog);
    dialog.Dispose();
    return selected;
  }

}
