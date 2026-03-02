using System.Text;
using BoydCode.Application.Interfaces;
using BoydCode.Presentation.Console.Terminal;
using Spectre.Console;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TguiApp = Terminal.Gui.App.Application;

#pragma warning disable CS0618 // Application.Invoke/Init/Shutdown/RequestStop - using legacy static API during Terminal.Gui migration

namespace BoydCode.Presentation.Console;

public sealed class SpectreUserInterface : IUserInterface, IDisposable
{
  private readonly IAnsiConsole _stderr = AnsiConsole.Create(
      new AnsiConsoleSettings { Out = new AnsiConsoleOutput(System.Console.Error) });

  private readonly ExecutionWindow _executionWindow;

  private bool _streamingStarted;
  private bool _isExecuting;
  private bool _isThinking;
  private bool _sessionActive; // true for entire chat session

  // Track previous activity state for restoring after cancel hint
  private ActivityState _preHintActivityState = ActivityState.Idle;

  // Terminal.Gui views
  private BoydCodeToplevel? _toplevel;

  // Modal overlay
  private Window? _modalWindow;

  // Streaming token batching
  private readonly StringBuilder _pendingTokens = new();
  private readonly object _tokenLock = new();
  private bool _flushScheduled;

  // Suspend/resume coordination for Spectre prompts
  private ManualResetEventSlim? _suspendedSignal;
  private ManualResetEventSlim? _resumeSignal;
  private ManualResetEventSlim? _resumedSignal;
  private volatile bool _suspendRequested;

  public SpectreUserInterface()
  {
    _executionWindow = new ExecutionWindow();
  }

  public static SpectreUserInterface? Current { get; private set; }

  public bool IsInteractive => AnsiConsole.Profile.Capabilities.Interactive;

  public string? StatusLine { get; set; }

  public string? StaleSettingsWarning { get; set; }

  internal BoydCodeToplevel? Toplevel => _toplevel;
  internal bool IsSessionActive => _sessionActive;
  internal bool IsSuspendRequested => _suspendRequested;

  internal void SignalSuspended()
  {
    _suspendedSignal?.Set();
  }

  internal void WaitForResume()
  {
    _resumeSignal?.Wait();
  }

  internal void SignalResumed()
  {
    _suspendRequested = false;
    _resumedSignal?.Set();
  }

  // -----------------------------------------------
  //  Input
  // -----------------------------------------------

  public async Task<string> GetUserInputAsync(CancellationToken ct = default)
  {
    if (!IsInteractive)
    {
      var line = System.Console.ReadLine();
      return line ?? "/quit";
    }

    if (_sessionActive && _toplevel is not null)
    {
      return await _toplevel.InputView.GetUserInputAsync(ct);
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

  // -----------------------------------------------
  //  Tool preview formatting (pure helpers)
  // -----------------------------------------------

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

  // -----------------------------------------------
  //  Render methods — marshal to UI thread
  // -----------------------------------------------

  public void RenderUserMessage(string message)
  {
    InvokeOnUiThread(() => _toplevel!.ConversationView.AddBlock(new UserMessageBlock(message)));
  }

  public void RenderAssistantText(string text)
  {
    InvokeOnUiThread(() =>
    {
      _toplevel!.ConversationView.AddBlock(new AssistantTextBlock(text));
      _toplevel!.ConversationView.AddBlock(new SeparatorBlock());
    });
  }

  public void RenderStreamingToken(string token)
  {
    if (!_streamingStarted)
    {
      _streamingStarted = true;
      _isThinking = false;
      InvokeOnUiThread(() =>
      {
        _toplevel?.ConversationView.BeginStream();
        _toplevel?.ActivityBar.SetState(ActivityState.Streaming);
      });
    }

    lock (_tokenLock)
    {
      _pendingTokens.Append(token);
      if (!_flushScheduled)
      {
        _flushScheduled = true;
        TguiApp.Invoke(() =>
        {
          string text;
          lock (_tokenLock)
          {
            text = _pendingTokens.ToString();
            _pendingTokens.Clear();
            _flushScheduled = false;
          }
          _toplevel?.ConversationView.AppendStreamText(text);
        });
      }
    }
  }

  public void RenderStreamingComplete()
  {
    // Flush any remaining tokens
    lock (_tokenLock)
    {
      if (_pendingTokens.Length > 0)
      {
        var remaining = _pendingTokens.ToString();
        _pendingTokens.Clear();
        InvokeOnUiThread(() => _toplevel?.ConversationView.AppendStreamText(remaining));
      }
      _flushScheduled = false;
    }

    _streamingStarted = false;
    InvokeOnUiThread(() =>
    {
      _toplevel?.ConversationView.EndStream();
      _toplevel?.ConversationView.AddBlock(new SeparatorBlock());
      _toplevel?.ActivityBar.SetState(ActivityState.Idle);
    });
  }

  public void RenderThinkingStart()
  {
    _isThinking = true;
    InvokeOnUiThread(() => _toplevel?.ActivityBar.SetState(ActivityState.Thinking));
  }

  public void RenderThinkingStop()
  {
    _isThinking = false;
    InvokeOnUiThread(() => _toplevel?.ActivityBar.SetState(ActivityState.Idle));
  }

  public void RenderToolExecution(string toolName, string argumentsJson)
  {
    var preview = FormatToolPreview(toolName, argumentsJson);
    InvokeOnUiThread(() =>
        _toplevel?.ConversationView.AddBlock(new ToolCallConversationBlock(toolName, preview)));
  }

  public void RenderExecutingStart()
  {
    _isExecuting = true;
    _executionWindow.Start();
    InvokeOnUiThread(() => _toplevel?.ActivityBar.SetState(ActivityState.Executing));
  }

  public void RenderExecutingStop()
  {
    if (!_isExecuting) return;
    _isExecuting = false;
    _executionWindow.Stop();
    InvokeOnUiThread(() => _toplevel?.ActivityBar.SetState(ActivityState.Idle));
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
      InvokeOnUiThread(() =>
          _toplevel?.ConversationView.AddBlock(new StatusMessageBlock("No tool output to expand.", MessageKind.Hint)));
      return;
    }

    if (_executionWindow.IsLastOutputExpanded)
    {
      InvokeOnUiThread(() =>
          _toplevel?.ConversationView.AddBlock(new StatusMessageBlock("Output already expanded.", MessageKind.Hint)));
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

    if (_sessionActive && _toplevel is not null)
    {
      InvokeOnUiThread(() =>
          _toplevel!.ConversationView.AddBlock(new StatusMessageBlock(message, MessageKind.Error)));
    }
  }

  public void RenderHint(string hint)
  {
    InvokeOnUiThread(() =>
    {
      _toplevel?.ConversationView.AddBlock(new StatusMessageBlock(hint, MessageKind.Hint));
      _toplevel?.ConversationView.AddBlock(new SeparatorBlock());
    });
  }

  public void RenderSuccess(string message)
  {
    InvokeOnUiThread(() =>
        _toplevel?.ConversationView.AddBlock(new StatusMessageBlock(message, MessageKind.Success)));
  }

  public void RenderWarning(string message)
  {
    InvokeOnUiThread(() =>
        _toplevel?.ConversationView.AddBlock(new StatusMessageBlock(message, MessageKind.Warning)));
  }

  public void RenderSection(string title)
  {
    InvokeOnUiThread(() =>
    {
      _toplevel?.ConversationView.AddBlock(new SeparatorBlock());
      _toplevel?.ConversationView.AddBlock(new SectionBlock(title));
    });
  }

  public void RenderTokenUsage(int inputTokens, int outputTokens)
  {
    InvokeOnUiThread(() =>
        _toplevel?.ConversationView.AddBlock(new TokenUsageBlock(inputTokens, outputTokens)));
  }

  public void RenderWelcome(string model, string workingDirectory)
  {
    // Render to stdout BEFORE Terminal.Gui takes over (called before ActivateLayout)
    AnsiConsole.Write(new FigletText("BoydCode").Color(Color.Blue));
    AnsiConsole.MarkupLine("[bold]AI Coding Assistant with JEA-Constrained PowerShell[/]");
    AnsiConsole.MarkupLine($"Model: [cyan]{Markup.Escape(model)}[/]");
    AnsiConsole.MarkupLine($"Working directory: [cyan]{Markup.Escape(workingDirectory)}[/]");
    AnsiConsole.MarkupLine("[dim]Type /quit to exit.[/]");
    AnsiConsole.WriteLine();
  }

  public void RenderMarkdown(string markdown)
  {
    InvokeOnUiThread(() =>
        _toplevel?.ConversationView.AddBlock(new PlainTextBlock(markdown)));
  }

  // -----------------------------------------------
  //  Cancel hint
  // -----------------------------------------------

  public void RenderCancelHint()
  {
    _preHintActivityState = CurrentActivityState();
    InvokeOnUiThread(() => _toplevel?.ActivityBar.SetState(ActivityState.CancelHint));
  }

  public void ClearCancelHint()
  {
    InvokeOnUiThread(() => _toplevel?.ActivityBar.SetState(_preHintActivityState));
  }

  // -----------------------------------------------
  //  Modal windows
  // -----------------------------------------------

  public void ShowModal(string title, string content)
  {
    InvokeOnUiThread(() =>
    {
      if (_toplevel is null) return;
      DismissCurrentModal();

      var window = new Window
      {
        Title = title,
        X = 2,
        Y = 1,
        Width = Dim.Fill(2),
        Height = Dim.Fill(2),
      };

      var textView = new TextView
      {
        Text = content,
        ReadOnly = true,
        X = 0,
        Y = 0,
        Width = Dim.Fill(),
        Height = Dim.Fill(),
      };

      window.Add(textView);
      _modalWindow = window;
      _toplevel.Add(window);
      _toplevel.ActivityBar.SetState(ActivityState.Modal);
      textView.CanFocus = true;
      textView.SetFocus();
    });
  }

  public void DismissModal()
  {
    InvokeOnUiThread(() => DismissCurrentModal());
  }

  public bool IsModalActive => _modalWindow is not null;

  private void DismissCurrentModal()
  {
    if (_modalWindow is not null && _toplevel is not null)
    {
      _toplevel.Remove(_modalWindow);
      _modalWindow.Dispose();
      _modalWindow = null;
      _toplevel.ActivityBar.SetState(ActivityState.Idle);
      _toplevel.InputView.SetFocus();
    }
  }

  // -----------------------------------------------
  //  Turn lifecycle
  // -----------------------------------------------

  public void BeginTurn()
  {
    InvokeOnUiThread(() =>
    {
      if (_toplevel is not null)
      {
        _toplevel.InputView.Enabled = false;
      }
    });

    if (StatusLine is not null && _toplevel is not null)
    {
      InvokeOnUiThread(() => _toplevel!.StatusBar.StatusText = StatusLine!);
    }
  }

  public void EndTurn()
  {
    InvokeOnUiThread(() =>
    {
      if (_toplevel is not null)
      {
        _toplevel.InputView.Enabled = true;
      }
    });
  }

  // -----------------------------------------------
  //  Layout lifecycle
  // -----------------------------------------------

  public void ActivateLayout()
  {
    if (!IsInteractive) return;
    _sessionActive = true;
    Current = this;
    _toplevel = new BoydCodeToplevel();

    // Wire modal dismissal callback
    _toplevel.TryDismissModal = () =>
    {
      if (_modalWindow is null) return false;
      DismissCurrentModal();
      return true;
    };

    // Wire ChatInputView cancel events to activity bar
    _toplevel.InputView.CancelHintRequested += () =>
    {
      _preHintActivityState = CurrentActivityState();
      _toplevel.ActivityBar.SetState(ActivityState.CancelHint);
    };
    _toplevel.InputView.CancelHintCleared += () =>
    {
      _toplevel.ActivityBar.SetState(_preHintActivityState);
    };

    // Wire ExecutionWindow block callback to marshal through UI thread
    _executionWindow.AddBlock = block =>
        InvokeOnUiThread(() => _toplevel?.ConversationView.AddBlock(block));

    // Push any pre-set status text to the status bar
    if (StatusLine is not null)
    {
      _toplevel.StatusBar.StatusText = StatusLine;
    }
  }

  public void DeactivateLayout()
  {
    _sessionActive = false;
    _toplevel = null;
    if (Current == this)
    {
      Current = null;
    }
  }

  // -----------------------------------------------
  //  Suspend/resume for Spectre prompts
  // -----------------------------------------------

  public void SuspendLayout()
  {
    if (!_sessionActive || _toplevel is null) return;

    _suspendedSignal = new ManualResetEventSlim(false);
    _resumeSignal = new ManualResetEventSlim(false);
    _resumedSignal = new ManualResetEventSlim(false);
    _suspendRequested = true;

    TguiApp.Invoke(() => TguiApp.RequestStop());
    _suspendedSignal.Wait(); // Block until Terminal.Gui actually stopped
  }

  public void ResumeLayout()
  {
    if (!_sessionActive) return;
    _resumeSignal?.Set(); // Tell main thread to restart
    _resumedSignal?.Wait(); // Wait for Terminal.Gui to be running again
  }

  // -----------------------------------------------
  //  Agent busy state
  // -----------------------------------------------

  public void SetAgentBusy(bool busy)
  {
    // State tracking for potential future UI indicator
  }

  // -----------------------------------------------
  //  Cancellation monitoring
  // -----------------------------------------------

  public IDisposable BeginCancellationMonitor(Action onCancelRequested)
  {
    if (_sessionActive && _toplevel is not null)
    {
      _toplevel.InputView.SetCancelCallback(onCancelRequested);
      return new CancelCallbackDisposer(_toplevel.InputView);
    }

    // Fallback: use standalone monitor for non-TUI mode
    return new CancellationMonitor(this, onCancelRequested);
  }

  // -----------------------------------------------
  //  Helpers
  // -----------------------------------------------

  private void InvokeOnUiThread(Action action)
  {
    if (_toplevel is null) return;
    TguiApp.Invoke(action);
  }

  private ActivityState CurrentActivityState()
  {
    if (_isExecuting) return ActivityState.Executing;
    if (_streamingStarted) return ActivityState.Streaming;
    if (_isThinking) return ActivityState.Thinking;
    return ActivityState.Idle;
  }

  // -----------------------------------------------
  //  Dispose
  // -----------------------------------------------

  public void Dispose()
  {
    _toplevel = null;
  }

  // -----------------------------------------------
  //  CancelCallbackDisposer — clears the cancel callback on the ChatInputView
  // -----------------------------------------------

  private sealed class CancelCallbackDisposer : IDisposable
  {
    private readonly ChatInputView _inputView;
    private bool _disposed;

    internal CancelCallbackDisposer(ChatInputView inputView)
    {
      _inputView = inputView;
    }

    public void Dispose()
    {
      if (_disposed) return;
      _disposed = true;
      _inputView.SetCancelCallback(null);
    }
  }

  // -----------------------------------------------
  //  CancellationMonitor — fallback for non-interactive mode
  // -----------------------------------------------

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
