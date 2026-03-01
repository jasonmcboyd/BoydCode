using System.Diagnostics;
using System.Globalization;
using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace BoydCode.Presentation.Console.Terminal;

internal enum IndicatorState
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
  private const int MaxContentBlocks = 200;
  private const int RenderIntervalMs = 16; // ~60fps

  // Shared state — guarded by _contentLock for the list, volatile/Interlocked for scalars
  private readonly object _contentLock = new();
  private readonly List<IRenderable> _contentBlocks = [];
  private volatile bool _isActive;
  private volatile bool _disposed;

  // Indicator state
  private volatile IndicatorState _indicatorState = IndicatorState.Idle;
  private readonly Stopwatch _executingStopwatch = new();

  // Input line state
  private volatile string _inputText = "";
  private volatile int _inputCursorPosition;

  // Status bar
  private volatile string _statusText = "";

  // Agent state
  private volatile bool _agentBusy;
  private volatile int _queueCount;

  // Streaming state
  private readonly object _streamLock = new();
  private StringBuilder? _streamBuffer;
  private bool _streamActive;

  // Modal overlay (plumbing for Phase 4)
  private volatile IRenderable? _modalContent;

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

  // ─────────────────────────────────────────────
  //  Lifecycle
  // ─────────────────────────────────────────────

  public void Activate()
  {
    if (!CanUseLayout())
    {
      return;
    }

    if (_isActive) return;

    Current = this;
    _isActive = true;

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
      // Best effort — render loop is non-critical
    }

    _renderCts?.Dispose();
    _renderCts = null;
    _renderTask = null;

    if (Current == this)
    {
      Current = null;
    }
  }

  public void Suspend()
  {
    if (!_isActive) return;

    if (Interlocked.Increment(ref _suspendDepth) > 1)
    {
      // Already suspended — no need to wait for acknowledgement again
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
      // Still nested — don't actually resume yet
      return;
    }

    Interlocked.Exchange(ref _suspendDepth, 0); // Clamp to zero in case of mismatched calls
    _suspendRequested.Reset();
    _resumeSignal.Set();
  }

  // ─────────────────────────────────────────────
  //  Content model
  // ─────────────────────────────────────────────

  public void AddContent(IRenderable renderable)
  {
    lock (_contentLock)
    {
      _contentBlocks.Add(renderable);
      TrimContentBlocks();
    }
  }

  public void AddContentLine(string text)
  {
    lock (_contentLock)
    {
      _contentBlocks.Add(new Text(text));
      TrimContentBlocks();
    }
  }

  public void AddContentMarkup(string markup)
  {
    lock (_contentLock)
    {
      _contentBlocks.Add(new Markup(markup));
      TrimContentBlocks();
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
        }
      }
      _streamBuffer = null;
      _streamActive = false;
    }
  }

  // ─────────────────────────────────────────────
  //  Indicator bar
  // ─────────────────────────────────────────────

  public void SetIndicator(IndicatorState state, string? text = null)
  {
    _indicatorState = state;
    if (state == IndicatorState.Executing)
    {
      _executingStopwatch.Restart();
    }
  }

  // ─────────────────────────────────────────────
  //  Input line
  // ─────────────────────────────────────────────

  public void UpdateInput(string text, int cursorPosition)
  {
    _inputText = text;
    _inputCursorPosition = cursorPosition;
  }

  // ─────────────────────────────────────────────
  //  Status bar
  // ─────────────────────────────────────────────

  public void UpdateStatus(string status)
  {
    _statusText = status;
  }

  // ─────────────────────────────────────────────
  //  Agent state
  // ─────────────────────────────────────────────

  public void SetAgentBusy(bool busy)
  {
    _agentBusy = busy;
  }

  public void UpdateQueueCount(int count)
  {
    _queueCount = count;
  }

  // ─────────────────────────────────────────────
  //  Modal overlay (plumbing for Phase 4)
  // ─────────────────────────────────────────────

  public void ShowModal(IRenderable content)
  {
    _modalContent = content;
    _indicatorState = IndicatorState.Modal;
  }

  public void DismissModal()
  {
    _modalContent = null;
    _indicatorState = IndicatorState.Idle;
  }

  public bool IsModalActive => _modalContent is not null;

  // ─────────────────────────────────────────────
  //  Dispose
  // ─────────────────────────────────────────────

  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;

    Deactivate();
    _suspendRequested.Dispose();
    _suspendAcknowledged.Dispose();
    _resumeSignal.Dispose();
  }

  // ─────────────────────────────────────────────
  //  Render loop
  // ─────────────────────────────────────────────

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

              await Task.Delay(RenderIntervalMs, ct).ConfigureAwait(false);
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
    try
    {
      termHeight = System.Console.WindowHeight;
    }
    catch
    {
      termHeight = 24;
    }

    // Content region gets all available space minus 3 fixed rows
    var contentHeight = Math.Max(termHeight - 3, 5);

    var layout = new Layout("Root")
      .SplitRows(
        new Layout("Content").MinimumSize(5).Ratio(1),
        new Layout("Indicator").Size(1),
        new Layout("Input").Size(1),
        new Layout("StatusBar").Size(1));

    // Content region
    var contentRenderable = BuildContentRegion(contentHeight);
    layout["Content"].Update(contentRenderable);

    // Modal overlay replaces content when active
    var modal = _modalContent;
    if (modal is not null)
    {
      layout["Content"].Update(modal);
    }

    // Indicator bar
    layout["Indicator"].Update(BuildIndicator());

    // Input line
    layout["Input"].Update(BuildInputLine());

    // Status bar
    layout["StatusBar"].Update(BuildStatusBar());

    return layout;
  }

  private IRenderable BuildContentRegion(int contentHeight)
  {
    var blocks = new List<IRenderable>();

    lock (_contentLock)
    {
      // Take the tail of the content blocks that fits the available height
      var maxBlocks = Math.Max(contentHeight / 2, 1);
      var startIndex = Math.Max(_contentBlocks.Count - maxBlocks, 0);
      for (var i = startIndex; i < _contentBlocks.Count; i++)
      {
        blocks.Add(_contentBlocks[i]);
      }
    }

    // Add streaming content if active
    lock (_streamLock)
    {
      if (_streamActive && _streamBuffer is not null && _streamBuffer.Length > 0)
      {
        blocks.Add(new Text(_streamBuffer.ToString()));
      }
    }

    if (blocks.Count == 0)
    {
      return new Text("");
    }

    return new Rows(blocks);
  }

  private Rule BuildIndicator()
  {
    string? title = _indicatorState switch
    {
      IndicatorState.Thinking => AccessibilityConfig.Accessible
        ? "Thinking..."
        : "[yellow]Thinking...[/]",
      IndicatorState.Streaming => AccessibilityConfig.Accessible
        ? "Streaming..."
        : "[cyan]Streaming...[/]",
      IndicatorState.Executing => AccessibilityConfig.Accessible
        ? $"Executing... ({FormatDuration(_executingStopwatch.Elapsed)})"
        : $"[blue]Executing... ({Markup.Escape(FormatDuration(_executingStopwatch.Elapsed))})[/]",
      IndicatorState.CancelHint => AccessibilityConfig.Accessible
        ? "Press Esc again to cancel"
        : "[yellow]Press Esc again to cancel[/]",
      IndicatorState.Modal => AccessibilityConfig.Accessible
        ? "Esc to dismiss"
        : "[dim]Esc to dismiss[/]",
      _ => null,
    };

    return title is not null
      ? new Rule(title).RuleStyle("dim")
      : new Rule().RuleStyle("dim");
  }

  private Markup BuildInputLine()
  {
    var text = _inputText;
    var cursorPos = _inputCursorPosition;

    var sb = new StringBuilder();
    sb.Append("[bold blue]>[/] ");

    if (text.Length == 0)
    {
      sb.Append("[invert] [/]");
    }
    else if (cursorPos >= text.Length)
    {
      sb.Append(Markup.Escape(text));
      sb.Append("[invert] [/]");
    }
    else
    {
      if (cursorPos > 0)
      {
        sb.Append(Markup.Escape(text[..cursorPos]));
      }
      sb.Append("[invert]");
      sb.Append(Markup.Escape(text[cursorPos].ToString()));
      sb.Append("[/]");
      if (cursorPos + 1 < text.Length)
      {
        sb.Append(Markup.Escape(text[(cursorPos + 1)..]));
      }
    }

    // Queue count on the right when agent is busy
    if (_agentBusy && _queueCount > 0)
    {
      var queueLabel = _queueCount == 1
        ? "[1 queued]"
        : $"[{_queueCount} queued]";
      sb.Append(CultureInfo.InvariantCulture, $"  [dim]{Markup.Escape(queueLabel)}[/]");
    }

    return new Markup(sb.ToString());
  }

  private IRenderable BuildStatusBar()
  {
    var text = _statusText;
    if (string.IsNullOrEmpty(text))
    {
      return new Text("");
    }
    return new Markup($"[dim]{Markup.Escape(text)}[/]");
  }

  // ─────────────────────────────────────────────
  //  Helpers
  // ─────────────────────────────────────────────

  private void TrimContentBlocks()
  {
    // Must be called under _contentLock
    while (_contentBlocks.Count > MaxContentBlocks)
    {
      _contentBlocks.RemoveAt(0);
    }
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
