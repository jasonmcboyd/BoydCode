using BoydCode.Application.Interfaces;
using BoydCode.Presentation.Console.Terminal;
using Spectre.Console;

namespace BoydCode.Presentation.Console;

public sealed class SpectreUserInterface : IUserInterface, IDisposable
{
  private readonly IAnsiConsole _stderr = AnsiConsole.Create(
      new AnsiConsoleSettings { Out = new AnsiConsoleOutput(System.Console.Error) });

  private readonly TerminalLayout _layout = new();
  private readonly ExecutionWindow _executionWindow;
  private AsyncInputReader? _inputReader;

  private bool _streamingStarted;
  private bool _isThinking;
  private bool _isExecuting;
  private bool _cancelHintShowing;
  private bool _layoutActive;

  public SpectreUserInterface()
  {
    _executionWindow = new ExecutionWindow(_layout.ConsoleLock);
  }

  public bool IsInteractive => AnsiConsole.Profile.Capabilities.Interactive;

  public string? StatusLine { get; set; }

  public string? StaleSettingsWarning { get; set; }

  public async Task<string> GetUserInputAsync(CancellationToken ct = default)
  {
    if (!IsInteractive)
    {
      var line = System.Console.ReadLine();
      return line ?? "/quit";
    }

    if (_layoutActive && _inputReader is not null)
    {
      // Async input: read from Channel (user can type while agent works)
      _layout.UpdateQueueCount(_inputReader.PendingCount);
      return await _inputReader.ReadLineAsync(ct);
    }

    // Fallback: blocking Spectre.Console prompt
    if (StatusLine is not null)
    {
      AnsiConsole.MarkupLine($"[dim]{Markup.Escape(StatusLine)}[/]");
    }

    if (StaleSettingsWarning is not null)
    {
      AnsiConsole.MarkupLine($"  [yellow]{Markup.Escape(StaleSettingsWarning)}[/]");
    }

    var input = AnsiConsole.Prompt(
        new TextPrompt<string>("[bold blue]>[/]")
            .AllowEmpty());
    return input;
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

  public void RenderToolExecution(string toolName, string argumentsJson)
  {
    var preview = FormatToolPreview(toolName, argumentsJson);
    AnsiConsole.Write(new Panel(Markup.Escape(preview))
        .Header($"[dim]{Markup.Escape(toolName)}[/]")
        .BorderColor(Color.Grey));
  }

  public void RenderExecutingStart()
  {
    lock (_layout.ConsoleLock)
    {
      _isExecuting = true;
      _cancelHintShowing = false;
      var useContainedOutput = IsInteractive && AnsiConsole.Profile.Capabilities.Ansi;
      _executionWindow.Start(useContainedOutput);
    }
  }

  public void RenderExecutingStop()
  {
    lock (_layout.ConsoleLock)
    {
      if (!_isExecuting) return;
      _isExecuting = false;
      _cancelHintShowing = false;
      _executionWindow.Stop();
    }
  }

  public void RenderOutputLine(string line)
  {
    lock (_layout.ConsoleLock)
    {
      if (_cancelHintShowing)
      {
        System.Console.Write("\r                                                  \r");
        _cancelHintShowing = false;
      }
      _executionWindow.AddOutputLine(line);
    }
  }

  public void RenderToolResult(string toolName, string result, bool isError)
  {
    lock (_layout.ConsoleLock)
    {
      _executionWindow.RenderToolResult(toolName, result, isError, outputStreamed: _executionWindow.OutputLineCount > 0 || _isExecuting);
    }
  }

  public void ExpandLastToolOutput() => _executionWindow.ExpandLastToolOutput();

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
    AnsiConsole.Write(new Panel(Markup.Escape(markdown)).Border(BoxBorder.Rounded));
  }

  public void RenderCancelHint()
  {
    lock (_layout.ConsoleLock)
    {
      if (_isExecuting)
      {
        _executionWindow.ClearForCancelHint();
      }
      AnsiConsole.Markup("[dim italic yellow]  Press Esc or Ctrl+C again to cancel[/]");
      _cancelHintShowing = true;
    }
  }

  public void ClearCancelHint()
  {
    lock (_layout.ConsoleLock)
    {
      if (!_cancelHintShowing) return;
      System.Console.Write("\r                                                  \r");
      _cancelHintShowing = false;
      if (_isExecuting)
      {
        _executionWindow.RestoreAfterCancelHint();
      }
    }
  }

  public void ActivateLayout()
  {
    if (!IsInteractive) return;

    _layout.Activate();
    _layoutActive = _layout.IsActive;

    if (_layoutActive)
    {
      if (StatusLine is not null)
      {
        _layout.UpdateStatusLine(StatusLine);
      }

      _inputReader = new AsyncInputReader(_layout)
      {
        OnCancelHintRequested = RenderCancelHint,
        OnCancelHintCleared = ClearCancelHint,
      };
      _inputReader.Start();
    }
  }

  public void DeactivateLayout()
  {
    _layoutActive = false;
    _inputReader?.Dispose();
    _inputReader = null;
    _layout.Deactivate();
  }

  public void SuspendLayout() => _layout.Suspend();

  public void ResumeLayout() => _layout.Resume();

  public void SetAgentBusy(bool busy)
  {
    _layout.SetAgentBusy(busy);
    if (_inputReader is not null)
    {
      _layout.UpdateQueueCount(_inputReader.PendingCount);
    }
  }

  public void Dispose()
  {
    _inputReader?.Dispose();
    _executionWindow.Dispose();
    _layout.Dispose();
  }

  public IDisposable BeginCancellationMonitor(Action onCancelRequested)
  {
    if (_layoutActive && _inputReader is not null)
    {
      // Delegate to AsyncInputReader's integrated cancellation
      return _inputReader.BeginCancellationMonitor(onCancelRequested);
    }

    // Fallback: use standalone monitor
    return new CancellationMonitor(this, onCancelRequested);
  }

  private sealed class CancellationMonitor : IDisposable
  {
    private readonly SpectreUserInterface _ui;
    private readonly Action _onCancelRequested;
    private readonly object _pressLock = new();
    private readonly CancellationTokenSource _pollCts = new();
    private readonly Task _pollTask;
    private Timer? _resetTimer;
    private DateTimeOffset _lastPressTime = DateTimeOffset.MinValue;
    private bool _disposed;

    internal CancellationMonitor(SpectreUserInterface ui, Action onCancelRequested)
    {
      _ui = ui;
      _onCancelRequested = onCancelRequested;

      System.Console.CancelKeyPress += OnCancelKeyPress;
      _pollTask = Task.Run(PollEscapeKeyAsync);
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
      e.Cancel = true;
      HandlePress();
    }

    private async Task PollEscapeKeyAsync()
    {
      try
      {
        while (!_pollCts.Token.IsCancellationRequested)
        {
          if (System.Console.KeyAvailable)
          {
            var key = System.Console.ReadKey(true);
            if (key.Key == ConsoleKey.Escape)
            {
              HandlePress();
            }
          }
          await Task.Delay(50, _pollCts.Token).ConfigureAwait(false);
        }
      }
      catch (OperationCanceledException)
      {
        // Expected during dispose
      }
    }

    private void HandlePress()
    {
      var shouldCancel = false;
      var shouldShowHint = false;

      lock (_pressLock)
      {
        if (_disposed) return;

        var now = DateTimeOffset.UtcNow;
        if ((now - _lastPressTime).TotalMilliseconds <= 1000)
        {
          _resetTimer?.Dispose();
          _resetTimer = null;
          shouldCancel = true;
        }
        else
        {
          _lastPressTime = now;
          shouldShowHint = true;
          _resetTimer?.Dispose();
          _resetTimer = new Timer(_ =>
          {
            lock (_pressLock)
            {
              if (_disposed) return;
              _lastPressTime = DateTimeOffset.MinValue;
            }
            _ui.ClearCancelHint();
          }, null, 1000, Timeout.Infinite);
        }
      }

      if (shouldShowHint)
      {
        _ui.RenderCancelHint();
      }

      if (shouldCancel)
      {
        _onCancelRequested();
      }
    }

    public void Dispose()
    {
      lock (_pressLock)
      {
        if (_disposed) return;
        _disposed = true;
        _resetTimer?.Dispose();
        _resetTimer = null;
      }

      System.Console.CancelKeyPress -= OnCancelKeyPress;
      _pollCts.Cancel();

      try
      {
        _pollTask.Wait(TimeSpan.FromMilliseconds(200));
      }
      catch
      {
        // Best effort
      }

      _pollCts.Dispose();
    }
  }
}
