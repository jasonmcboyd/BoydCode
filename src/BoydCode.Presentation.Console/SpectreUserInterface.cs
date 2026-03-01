using BoydCode.Application.Interfaces;
using BoydCode.Presentation.Console.Renderables;
using BoydCode.Presentation.Console.Terminal;
using Spectre.Console;

namespace BoydCode.Presentation.Console;

public sealed class SpectreUserInterface : IUserInterface, IDisposable
{
  private readonly IAnsiConsole _stderr = AnsiConsole.Create(
      new AnsiConsoleSettings { Out = new AnsiConsoleOutput(System.Console.Error) });

  private readonly TuiLayout _layout = new();
  private readonly ExecutionWindow _executionWindow;
  private AsyncInputReader? _inputReader;

  private bool _streamingStarted;
  private bool _isExecuting;
  private bool _sessionActive; // true for entire chat session

  // Track previous activity state for restoring after cancel hint
  private ActivityState _preHintActivityState = ActivityState.Idle;

  public SpectreUserInterface()
  {
    _executionWindow = new ExecutionWindow(_layout);
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

    if (_sessionActive && _inputReader is not null)
    {
      _inputReader.ShowPrompt();
      // Async input: read from Channel (user can type while agent works)
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
        "Shell" => FormatShellCommand(root),
        _ => FormatGeneric(root),
      };
    }
    catch
    {
      return argumentsJson.Length > 200 ? argumentsJson[..200] + "..." : argumentsJson;
    }
  }

  private static string FormatShellCommand(System.Text.Json.JsonElement root)
  {
    var command = GetStringProperty(root, "command");
    if (command is null) return FormatGeneric(root);

    var segments = command.Split("; ");
    return string.Join(Environment.NewLine, segments);
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

  public void RenderUserMessage(string message)
  {
    _layout.AddContent(ConversationRenderables.UserMessage(message));
  }

  public void RenderAssistantText(string text)
  {
    _layout.AddContent(ConversationRenderables.AssistantText(text));
    _layout.AddContent(ConversationRenderables.TurnSeparator());
  }

  public void RenderStreamingToken(string token)
  {
    if (!_streamingStarted)
    {
      _layout.BeginStream();
      _layout.AppendStreamText("  " + token);
      _layout.SetActivity(ActivityState.Streaming);
      _streamingStarted = true;
    }
    else
    {
      _layout.AppendStreamText(token);
    }
  }

  public void RenderStreamingComplete()
  {
    _streamingStarted = false;
    _layout.EndStream();
    _layout.SetActivity(ActivityState.Idle);
    _layout.AddContentLine("");
  }

  public void RenderThinkingStart()
  {
    _layout.SetActivity(ActivityState.Thinking);
  }

  public void RenderThinkingStop()
  {
    _layout.SetActivity(ActivityState.Idle);
  }

  public void RenderToolExecution(string toolName, string argumentsJson)
  {
    var preview = FormatToolPreview(toolName, argumentsJson);
    var badge = ConversationRenderables.ToolCallBadge(toolName, preview);
    _layout.AddContent(badge);
  }

  public void RenderExecutingStart()
  {
    _isExecuting = true;
    _executionWindow.Start();
  }

  public void RenderExecutingStop()
  {
    if (!_isExecuting) return;
    _isExecuting = false;
    _executionWindow.Stop();
  }

  public void RenderOutputLine(string line)
  {
    _executionWindow.AddOutputLine(line);
  }

  public void RenderToolResult(string toolName, string result, bool isError)
  {
    _executionWindow.RenderToolResult(toolName, result, isError);
  }

  public void ExpandLastToolOutput()
  {
    var output = _executionWindow.GetLastOutput();
    if (output is null)
    {
      _layout.AddContentMarkup("[dim]No tool output to expand.[/]");
      return;
    }

    if (_executionWindow.IsLastOutputExpanded)
    {
      _layout.AddContentMarkup("[dim]Output already expanded.[/]");
      return;
    }

    _executionWindow.MarkLastOutputExpanded();
    var content = string.Join(Environment.NewLine, output);
    ShowModal($"Shell Output ({output.Count} lines)", content);
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
    _layout.AddContentMarkup($"  [dim italic]{Markup.Escape(hint)}[/]");
    _layout.AddContentLine("");
  }

  public void RenderSuccess(string message)
  {
    _layout.AddContentMarkup($"  [green]\u2713[/] {Markup.Escape(message)}");
  }

  public void RenderWarning(string message)
  {
    _layout.AddContentMarkup($"[yellow]Warning:[/] {Markup.Escape(message)}");
  }

  public void RenderSection(string title)
  {
    _layout.AddContentLine("");
    _layout.AddContent(new Rule($"[bold]{Markup.Escape(title)}[/]").LeftJustified().RuleStyle("dim"));
  }

  public void RenderTokenUsage(int inputTokens, int outputTokens)
  {
    var renderable = ConversationRenderables.TokenUsage(inputTokens, outputTokens);
    _layout.AddContent(renderable);
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
    var panel = new Panel(Markup.Escape(markdown)).Border(BoxBorder.Rounded);
    _layout.AddContent(panel);
  }

  public void RenderCancelHint()
  {
    // Track what the activity was before the hint so we can restore it
    _preHintActivityState = _isExecuting
      ? ActivityState.Executing
      : _streamingStarted
        ? ActivityState.Streaming
        : ActivityState.Idle;

    _layout.SetActivity(ActivityState.CancelHint);
  }

  public void ClearCancelHint()
  {
    // Restore the previous activity state
    _layout.SetActivity(_preHintActivityState);
  }

  public void ShowModal(string title, string content)
  {
    var panel = new Panel(Markup.Escape(content))
      .Header($"[bold]{Markup.Escape(title)}[/]")
      .Border(BoxBorder.Rounded)
      .BorderColor(Color.Blue)
      .Expand();

    _layout.ShowModal(panel);
  }

  public void DismissModal()
  {
    _layout.DismissModal();
  }

  public bool IsModalActive => _layout.IsModalActive;

  public void BeginTurn()
  {
    _layout.BeginTurn();
    if (StatusLine is not null)
    {
      _layout.UpdateStatus(StatusLine);
    }
  }

  public void EndTurn()
  {
    _layout.EndTurn();
  }

  public void ActivateLayout()
  {
    if (!IsInteractive) return;
    _sessionActive = true;

    _layout.Activate();

    _inputReader = new AsyncInputReader(_layout)
    {
      OnCancelHintRequested = RenderCancelHint,
      OnCancelHintCleared = ClearCancelHint,
      IsModalActive = () => _layout.IsModalActive,
      OnModalDismissRequested = () => _layout.DismissModal(),
    };
    _inputReader.Start();
  }

  public void DeactivateLayout()
  {
    _sessionActive = false;
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
    _layout.Dispose();
  }

  public IDisposable BeginCancellationMonitor(Action onCancelRequested)
  {
    if (_sessionActive && _inputReader is not null)
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
