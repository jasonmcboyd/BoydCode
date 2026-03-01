using System.Diagnostics;
using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace BoydCode.Presentation.Console.Terminal;

internal enum ActivityState
{
  Idle,
  Thinking,
  Streaming,
  Executing,
  CancelHint,
  Modal,
}

internal sealed class TuiLayout : IDisposable
{
  private const int MinTerminalHeight = 10;
  private const int MaxContentBlocks = 2000;
  private const int MaxInputLines = 10;
  private const int RenderIntervalMs = 16; // ~60fps
  private static readonly string[] SpinnerFrames = ["\u283F", "\u283B", "\u283D", "\u283E", "\u2837", "\u282F", "\u281F", "\u283E"];

  // Shared state -- guarded by _contentLock for the list, volatile/Interlocked for scalars
  private readonly object _contentLock = new();
  private readonly List<IRenderable> _contentBlocks = [];
  private volatile bool _isActive;
  private volatile bool _disposed;

  // Scroll state
  private int _viewportOffset; // 0 = pinned to bottom, >0 = scrolled up N blocks

  // Activity state
  private volatile ActivityState _activityState = ActivityState.Idle;
  private readonly Stopwatch _executingStopwatch = new();
  private readonly Stopwatch _masterStopwatch = new();

  // Status bar
  private volatile string _statusText = "";

  // Streaming state
  private readonly object _streamLock = new();
  private StringBuilder? _streamBuffer;
  private volatile bool _streamActive;

  // Modal overlay
  private volatile IRenderable? _modalContent;

  // Input region state
  private volatile string _inputText = "";
  private volatile int _inputCursorPosition;
  private volatile bool _agentBusy;
  private volatile int _queueCount;
  private volatile int _currentInputHeight = 1;

  // Suspend/resume coordination
  private readonly ManualResetEventSlim _suspendRequested = new(false);
  private readonly ManualResetEventSlim _suspendAcknowledged = new(false);
  private readonly ManualResetEventSlim _resumeSignal = new(false);
  private int _suspendDepth;

  // Render loop
  private Task? _renderTask;
  private CancellationTokenSource? _renderCts;

  public static TuiLayout? Current { get; private set; }

  public bool IsActive => _isActive;

  // -----------------------------------------------
  //  Lifecycle — persistent for entire session
  // -----------------------------------------------

  public void Activate()
  {
    if (!CanUseLayout())
    {
      return;
    }

    if (_isActive) return;

    Current = this;
    _isActive = true;
    _activityState = ActivityState.Idle;
    _masterStopwatch.Start();

    _renderCts = new CancellationTokenSource();
    _renderTask = Task.Run(() => RenderLoopAsync(_renderCts.Token));
  }

  public void Deactivate()
  {
    if (!_isActive) return;

    _isActive = false;
    _suspendDepth = 0;

    // Signal render loop to stop
    _renderCts?.Cancel();

    // If suspended, release the render loop so it can exit
    _resumeSignal.Set();
    _suspendRequested.Reset();

    try
    {
      _renderTask?.Wait(TimeSpan.FromSeconds(2));
    }
    catch
    {
      // Best effort -- render loop is non-critical
    }

    _renderCts?.Dispose();
    _renderCts = null;
    _renderTask = null;

    if (Current == this)
    {
      Current = null;
    }
  }

  public void BeginTurn()
  {
    _activityState = ActivityState.Thinking;
    _masterStopwatch.Restart();
  }

  public void EndTurn()
  {
    _activityState = ActivityState.Idle;
  }

  public void Suspend()
  {
    if (!_isActive) return;

    if (Interlocked.Increment(ref _suspendDepth) > 1)
    {
      // Already suspended -- no need to wait for acknowledgement again
      return;
    }

    _suspendAcknowledged.Reset();
    _suspendRequested.Set();

    // Wait for the render loop to acknowledge the suspend (exit Live context)
    _suspendAcknowledged.Wait(TimeSpan.FromSeconds(2));
  }

  public void Resume()
  {
    if (!_isActive) return;

    if (Interlocked.Decrement(ref _suspendDepth) > 0)
    {
      // Still nested -- don't actually resume yet
      return;
    }

    Interlocked.Exchange(ref _suspendDepth, 0); // Clamp to zero in case of mismatched calls
    _suspendRequested.Reset();
    _resumeSignal.Set();
  }

  // -----------------------------------------------
  //  Content model
  // -----------------------------------------------

  public void AddContent(IRenderable renderable)
  {
    lock (_contentLock)
    {
      _contentBlocks.Add(renderable);
      TrimContentBlocks();
      if (_viewportOffset > 0) _viewportOffset++;
    }
  }

  public void AddContentLine(string text)
  {
    lock (_contentLock)
    {
      _contentBlocks.Add(new Text(text));
      TrimContentBlocks();
      if (_viewportOffset > 0) _viewportOffset++;
    }
  }

  public void AddContentMarkup(string markup)
  {
    lock (_contentLock)
    {
      _contentBlocks.Add(new Markup(markup));
      TrimContentBlocks();
      if (_viewportOffset > 0) _viewportOffset++;
    }
  }

  public void BeginStream()
  {
    lock (_streamLock)
    {
      _streamBuffer = new StringBuilder();
      _streamActive = true;
    }
  }

  public void AppendStreamText(string text)
  {
    lock (_streamLock)
    {
      _streamBuffer?.Append(text);
    }
  }

  public void EndStream()
  {
    lock (_streamLock)
    {
      if (_streamBuffer is not null && _streamBuffer.Length > 0)
      {
        var finalText = _streamBuffer.ToString();
        lock (_contentLock)
        {
          _contentBlocks.Add(new Text(finalText));
          TrimContentBlocks();
          if (_viewportOffset > 0) _viewportOffset++;
        }
      }
      _streamBuffer = null;
      _streamActive = false;
    }
  }

  // -----------------------------------------------
  //  Scroll
  // -----------------------------------------------

  public bool IsScrolledToBottom
  {
    get
    {
      lock (_contentLock)
      {
        return _viewportOffset == 0;
      }
    }
  }

  public void ScrollUp(int blocks = 5)
  {
    lock (_contentLock)
    {
      _viewportOffset = Math.Min(_viewportOffset + blocks, Math.Max(_contentBlocks.Count - 1, 0));
    }
  }

  public void ScrollDown(int blocks = 5)
  {
    lock (_contentLock)
    {
      _viewportOffset = Math.Max(_viewportOffset - blocks, 0);
    }
  }

  public void ScrollToTop()
  {
    lock (_contentLock)
    {
      _viewportOffset = Math.Max(_contentBlocks.Count - 1, 0);
    }
  }

  public void ScrollToBottom()
  {
    lock (_contentLock)
    {
      _viewportOffset = 0;
    }
  }

  // -----------------------------------------------
  //  Activity bar
  // -----------------------------------------------

  public void SetActivity(ActivityState state, string? text = null)
  {
    _activityState = state;
    if (state == ActivityState.Executing)
    {
      _executingStopwatch.Restart();
    }
  }

  // -----------------------------------------------
  //  Status bar
  // -----------------------------------------------

  public void UpdateStatus(string status)
  {
    _statusText = status;
  }

  // -----------------------------------------------
  //  Input region
  // -----------------------------------------------

  public void UpdateInput(string text, int cursorPosition)
  {
    _inputText = text;
    _inputCursorPosition = cursorPosition;
  }

  public void SetAgentBusy(bool busy)
  {
    _agentBusy = busy;
  }

  public void UpdateQueueCount(int count)
  {
    _queueCount = count;
  }

  // -----------------------------------------------
  //  Modal overlay
  // -----------------------------------------------

  public void ShowModal(IRenderable content)
  {
    _modalContent = content;
    _activityState = ActivityState.Modal;
  }

  public void DismissModal()
  {
    _modalContent = null;
    _activityState = ActivityState.Idle;
  }

  public bool IsModalActive => _modalContent is not null;

  // -----------------------------------------------
  //  Dispose
  // -----------------------------------------------

  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;

    Deactivate();
    _suspendRequested.Dispose();
    _suspendAcknowledged.Dispose();
    _resumeSignal.Dispose();
  }

  // -----------------------------------------------
  //  Render loop
  // -----------------------------------------------

  private async Task RenderLoopAsync(CancellationToken ct)
  {
    while (!ct.IsCancellationRequested)
    {
      try
      {
        await AnsiConsole.Live(new Text(""))
          .AutoClear(false)
          .Overflow(VerticalOverflow.Ellipsis)
          .StartAsync(async ctx =>
          {
            while (!ct.IsCancellationRequested)
            {
              // Check for suspend request
              if (_suspendRequested.IsSet)
              {
                // Acknowledge and exit Live context
                _suspendAcknowledged.Set();

                // Wait for resume signal
                while (!ct.IsCancellationRequested)
                {
                  if (_resumeSignal.Wait(100))
                  {
                    _resumeSignal.Reset();
                    break;
                  }
                }

                if (ct.IsCancellationRequested) return;

                // Re-enter the Live context by breaking out so the outer loop restarts it
                return;
              }

              // Build and render the layout
              var layout = BuildLayout();
              ctx.UpdateTarget(layout);
              ctx.Refresh();

              var interval = _streamActive ? RenderIntervalMs : 100;
              await Task.Delay(interval, ct).ConfigureAwait(false);
            }
          }).ConfigureAwait(false);
      }
      catch (OperationCanceledException)
      {
        // Expected during shutdown
        return;
      }
      catch
      {
        // If Live throws for any reason, wait a bit and retry
        if (ct.IsCancellationRequested) return;
        await Task.Delay(100, ct).ConfigureAwait(false);
      }
    }
  }

  private Layout BuildLayout()
  {
    int termHeight;
    int termWidth;
    try
    {
      termHeight = System.Console.WindowHeight;
    }
    catch
    {
      termHeight = 24;
    }
    try
    {
      termWidth = System.Console.WindowWidth;
    }
    catch
    {
      termWidth = 120;
    }

    // Calculate dynamic input height
    _currentInputHeight = CalculateInputLineCount(_inputText, termWidth);
    var maxInputHeight = Math.Min(MaxInputLines, termHeight / 4);
    _currentInputHeight = Math.Clamp(_currentInputHeight, 1, maxInputHeight);

    // Fixed overhead: Activity(1) + Rule1(1) + Rule2(1) + StatusBar(1) = 4
    var contentHeight = Math.Max(termHeight - 4 - _currentInputHeight, 3);

    var layout = new Layout("Root")
      .SplitRows(
        new Layout("Content").MinimumSize(3).Ratio(1),
        new Layout("Activity").Size(1),
        new Layout("Rule1").Size(1),
        new Layout("Input").Size(_currentInputHeight),
        new Layout("Rule2").Size(1),
        new Layout("StatusBar").Size(1));

    layout["Rule1"].Update(new Rule().RuleStyle("dim"));
    layout["Rule2"].Update(new Rule().RuleStyle("dim"));

    // Content region
    var contentRenderable = BuildContentRegion(contentHeight);
    layout["Content"].Update(contentRenderable);

    // Modal overlay replaces content when active
    var modal = _modalContent;
    if (modal is not null)
    {
      layout["Content"].Update(modal);
    }

    // Activity bar
    layout["Activity"].Update(BuildActivity());

    // Input region
    layout["Input"].Update(BuildInputRegion(termWidth));

    // Status bar
    layout["StatusBar"].Update(BuildStatusBar());

    return layout;
  }

  private IRenderable BuildContentRegion(int contentHeight)
  {
    var blocks = new List<IRenderable>();
    var showMoreIndicator = false;

    lock (_contentLock)
    {
      // Viewport-aware rendering
      var endIndex = _contentBlocks.Count - _viewportOffset;
      if (endIndex < 0) endIndex = 0;
      var startIndex = Math.Max(endIndex - contentHeight, 0);

      for (var i = startIndex; i < endIndex && i < _contentBlocks.Count; i++)
      {
        blocks.Add(_contentBlocks[i]);
      }

      showMoreIndicator = _viewportOffset > 0;
    }

    // Show streaming content only when pinned to bottom
    if (_viewportOffset == 0)
    {
      lock (_streamLock)
      {
        if (_streamActive && _streamBuffer is not null && _streamBuffer.Length > 0)
        {
          blocks.Add(new Markup($"  {Markup.Escape(_streamBuffer.ToString())}"));
        }
      }
    }

    if (showMoreIndicator)
    {
      blocks.Add(new Markup("[dim]\u2193 More content below[/]"));
    }

    if (blocks.Count == 0)
    {
      return new Text("");
    }

    return new Rows(blocks);
  }

  private IRenderable BuildActivity()
  {
    var frameIndex = (int)(_masterStopwatch.ElapsedMilliseconds / 100) % SpinnerFrames.Length;
    var spinner = SpinnerFrames[frameIndex];

    if (AccessibilityConfig.Accessible)
    {
      return _activityState switch
      {
        ActivityState.Thinking => new Text("[Thinking...]"),
        ActivityState.Streaming => new Text("[Streaming...]"),
        ActivityState.Executing => new Text($"[Executing... ({FormatDuration(_executingStopwatch.Elapsed)})]"),
        ActivityState.CancelHint => new Text("Press Esc again to cancel"),
        ActivityState.Modal => new Text("Esc to dismiss"),
        _ => new Rule().RuleStyle("dim"),
      };
    }

    return _activityState switch
    {
      ActivityState.Thinking => new Markup($"[yellow]{spinner} Thinking...[/]"),
      ActivityState.Streaming => new Markup($"[cyan]{spinner} Streaming...[/]"),
      ActivityState.Executing => BuildExecutingActivity(spinner),
      ActivityState.CancelHint => new Markup("[yellow]Press Esc again to cancel[/]"),
      ActivityState.Modal => new Markup("[dim]Esc to dismiss[/]"),
      _ => new Rule().RuleStyle("dim"),
    };
  }

  private Markup BuildExecutingActivity(string spinner)
  {
    var duration = Markup.Escape(FormatDuration(_executingStopwatch.Elapsed));
    return new Markup($"[cyan]{spinner} Executing... ({duration})[/]");
  }

  private IRenderable BuildStatusBar()
  {
    var text = _statusText;
    if (string.IsNullOrEmpty(text))
    {
      return new Text("");
    }

    int termWidth;
    try { termWidth = System.Console.WindowWidth; } catch { termWidth = 120; }

    var hints = "Esc: cancel  /help: commands";
    var available = termWidth - text.Length - hints.Length - 2;

    if (available >= 4)
    {
      var padding = new string(' ', available);
      return new Markup($"[dim]{Markup.Escape(text)}{padding}{Markup.Escape(hints)}[/]");
    }

    return new Markup($"[dim]{Markup.Escape(text)}[/]");
  }

  private Markup BuildInputRegion(int termWidth)
  {
    var text = _inputText;
    var cursorPos = _inputCursorPosition;

    if (_agentBusy)
    {
      var prompt = $"> {text}";
      if (_queueCount > 0)
      {
        var badge = $" [{_queueCount} queued]";
        var available = termWidth - prompt.Length - badge.Length;
        if (available >= 0)
        {
          return new Markup($"[dim]{Markup.Escape(prompt)}[/][yellow]{Markup.Escape(badge)}[/]");
        }
      }

      return new Markup($"[dim]{Markup.Escape(prompt)}[/]");
    }

    // Render with cursor indicator
    var before = text[..Math.Min(cursorPos, text.Length)];
    var cursor = cursorPos < text.Length ? text[cursorPos].ToString() : " ";
    var after = cursorPos < text.Length - 1 ? text[(cursorPos + 1)..] : "";

    return new Markup($"[bold blue]>[/] {Markup.Escape(before)}[underline]{Markup.Escape(cursor)}[/]{Markup.Escape(after)}");
  }

  // -----------------------------------------------
  //  Helpers
  // -----------------------------------------------

  private void TrimContentBlocks()
  {
    // Must be called under _contentLock
    while (_contentBlocks.Count > MaxContentBlocks)
    {
      _contentBlocks.RemoveAt(0);
      if (_viewportOffset > 0) _viewportOffset--;
    }
  }

  private static int CalculateInputLineCount(string text, int termWidth)
  {
    if (string.IsNullOrEmpty(text)) return 1;

    // Account for "> " prefix (2 chars)
    var availableWidth = Math.Max(termWidth - 2, 1);
    var lines = 1 + (text.Length / availableWidth);
    return lines;
  }

  private static bool CanUseLayout()
  {
    try
    {
      if (System.Console.IsOutputRedirected || System.Console.IsInputRedirected)
      {
        return false;
      }

      return System.Console.WindowHeight >= MinTerminalHeight;
    }
    catch
    {
      return false;
    }
  }

  private static string FormatDuration(TimeSpan elapsed)
  {
    if (elapsed.TotalMinutes >= 1)
    {
      return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
    }
    return elapsed.TotalSeconds >= 10
      ? $"{elapsed.TotalSeconds:F0}s"
      : $"{elapsed.TotalSeconds:F1}s";
  }
}
