using System.Text;
using System.Threading.Channels;

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class AsyncInputReader : IDisposable
{
  private const int MaxHistorySize = 100;
  private const int PollingIntervalMs = 16;
  private const int CancelWindowMs = 1000;

  private readonly TuiLayout? _layout;
  private readonly Channel<string> _completedLines = Channel.CreateUnbounded<string>();
  private readonly StringBuilder _lineBuffer = new();
  private readonly List<string> _history = [];
  private readonly object _cancelLock = new();

  private CancellationTokenSource? _readerCts;
  private Task? _readerTask;
  private int _cursorPosition;
  private int _historyIndex = -1;
  private string? _savedCurrentLine;

  // Cancellation monitoring state (guarded by _cancelLock)
  private Action? _onCancelHintRequested;
  private Action? _onCancelHintCleared;
  private Action? _onCancelRequested;
  private DateTimeOffset _lastCancelPressTime = DateTimeOffset.MinValue;
  private Timer? _cancelResetTimer;
  private bool _disposed;

  public AsyncInputReader(TuiLayout? layout)
  {
    _layout = layout;
  }

  public int PendingCount => _completedLines.Reader.Count;

  public void Start()
  {
    if (_readerCts is not null) return;

    _readerCts = new CancellationTokenSource();
    var ct = _readerCts.Token;

    System.Console.CancelKeyPress += OnCancelKeyPress;
    _readerTask = Task.Run(() => ReadKeyLoopAsync(ct));
  }

  public ValueTask<string> ReadLineAsync(CancellationToken ct = default)
  {
    return _completedLines.Reader.ReadAsync(ct);
  }

  public IDisposable BeginCancellationMonitor(Action onCancelRequested)
  {
    return new CancellationScope(this, onCancelRequested);
  }

  public void Dispose()
  {
    lock (_cancelLock)
    {
      if (_disposed) return;
      _disposed = true;
      _cancelResetTimer?.Dispose();
      _cancelResetTimer = null;
      _onCancelRequested = null;
      _onCancelHintRequested = null;
      _onCancelHintCleared = null;
    }

    System.Console.CancelKeyPress -= OnCancelKeyPress;

    if (_readerCts is not null)
    {
      _readerCts.Cancel();
      try
      {
        _readerTask?.Wait(TimeSpan.FromMilliseconds(200));
      }
      catch
      {
        // Best effort — reader is non-critical
      }
      _readerCts.Dispose();
      _readerCts = null;
      _readerTask = null;
    }
  }

  private async Task ReadKeyLoopAsync(CancellationToken ct)
  {
    try
    {
      while (!ct.IsCancellationRequested)
      {
        if (System.Console.KeyAvailable)
        {
          var key = System.Console.ReadKey(intercept: true);
          ProcessKey(key);
        }
        else
        {
          await Task.Delay(PollingIntervalMs, ct).ConfigureAwait(false);
        }
      }
    }
    catch (OperationCanceledException)
    {
      // Expected during dispose
    }
  }

  private void ProcessKey(ConsoleKeyInfo key)
  {
    switch (key.Key)
    {
      case ConsoleKey.Enter:
        HandleEnter();
        break;

      case ConsoleKey.Backspace:
        HandleBackspace();
        break;

      case ConsoleKey.Delete:
        HandleDelete();
        break;

      case ConsoleKey.LeftArrow:
        if (_cursorPosition > 0)
        {
          _cursorPosition--;
          UpdateDisplay();
        }
        break;

      case ConsoleKey.RightArrow:
        if (_cursorPosition < _lineBuffer.Length)
        {
          _cursorPosition++;
          UpdateDisplay();
        }
        break;

      case ConsoleKey.Home:
        _cursorPosition = 0;
        UpdateDisplay();
        break;

      case ConsoleKey.End:
        _cursorPosition = _lineBuffer.Length;
        UpdateDisplay();
        break;

      case ConsoleKey.UpArrow:
        HandleHistoryUp();
        break;

      case ConsoleKey.DownArrow:
        HandleHistoryDown();
        break;

      case ConsoleKey.Escape:
        if (IsModalActive?.Invoke() == true)
        {
          OnModalDismissRequested?.Invoke();
        }
        else
        {
          HandleCancelPress();
        }
        break;

      case ConsoleKey.Tab:
        // No tab completion — ignore
        break;

      default:
        if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
        {
          _lineBuffer.Insert(_cursorPosition, key.KeyChar);
          _cursorPosition++;
          UpdateDisplay();
        }
        break;
    }
  }

  private void HandleEnter()
  {
    var line = _lineBuffer.ToString();

    if (line.Length > 0)
    {
      AddToHistory(line);
      _completedLines.Writer.TryWrite(line);
    }

    _lineBuffer.Clear();
    _cursorPosition = 0;
    _historyIndex = -1;
    _savedCurrentLine = null;
    UpdateDisplay();
  }

  private void HandleBackspace()
  {
    if (_cursorPosition <= 0) return;

    _lineBuffer.Remove(_cursorPosition - 1, 1);
    _cursorPosition--;
    UpdateDisplay();
  }

  private void HandleDelete()
  {
    if (_cursorPosition >= _lineBuffer.Length) return;

    _lineBuffer.Remove(_cursorPosition, 1);
    UpdateDisplay();
  }

  private void HandleHistoryUp()
  {
    if (_history.Count == 0) return;

    if (_historyIndex == -1)
    {
      _savedCurrentLine = _lineBuffer.ToString();
      _historyIndex = _history.Count - 1;
    }
    else if (_historyIndex > 0)
    {
      _historyIndex--;
    }
    else
    {
      return;
    }

    LoadHistoryEntry(_history[_historyIndex]);
  }

  private void HandleHistoryDown()
  {
    if (_historyIndex == -1) return;

    if (_historyIndex < _history.Count - 1)
    {
      _historyIndex++;
      LoadHistoryEntry(_history[_historyIndex]);
    }
    else
    {
      _historyIndex = -1;
      LoadHistoryEntry(_savedCurrentLine ?? string.Empty);
      _savedCurrentLine = null;
    }
  }

  private void LoadHistoryEntry(string entry)
  {
    _lineBuffer.Clear();
    _lineBuffer.Append(entry);
    _cursorPosition = _lineBuffer.Length;
    UpdateDisplay();
  }

  private void AddToHistory(string line)
  {
    // Avoid duplicate consecutive entries
    if (_history.Count > 0 && _history[^1] == line) return;

    _history.Add(line);

    if (_history.Count > MaxHistorySize)
    {
      _history.RemoveAt(0);
    }
  }

  private void UpdateDisplay()
  {
    if (_layout is not null)
    {
      _layout.UpdateInput(_lineBuffer.ToString(), _cursorPosition);
    }
    else
    {
      UpdateDisplayFallback();
    }
  }

  private void UpdateDisplayFallback()
  {
    var text = _lineBuffer.ToString();
    var rendered = $"> {text}";

    int termWidth;
    try { termWidth = System.Console.WindowWidth; }
    catch { termWidth = 120; }

    var padding = Math.Max(termWidth - rendered.Length - 1, 0);
    System.Console.Write($"\r{rendered}{new string(' ', padding)}");

    // Position the cursor correctly
    var cursorScreenPos = _cursorPosition + 2; // 2 = "> " prefix length
    System.Console.Write($"\r\x1b[{cursorScreenPos + 1}G");
  }

  private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
  {
    e.Cancel = true;
    HandleCancelPress();
  }

  private void HandleCancelPress()
  {
    var shouldCancel = false;
    var shouldShowHint = false;

    lock (_cancelLock)
    {
      if (_disposed || _onCancelRequested is null) return;

      var now = DateTimeOffset.UtcNow;
      if ((now - _lastCancelPressTime).TotalMilliseconds <= CancelWindowMs)
      {
        // Second press within window — cancel
        _cancelResetTimer?.Dispose();
        _cancelResetTimer = null;
        _lastCancelPressTime = DateTimeOffset.MinValue;
        shouldCancel = true;
      }
      else
      {
        // First press — start reset timer
        _lastCancelPressTime = now;
        shouldShowHint = true;
        _cancelResetTimer?.Dispose();
        _cancelResetTimer = new Timer(_ =>
        {
          Action? clearCallback;
          lock (_cancelLock)
          {
            if (_disposed) return;
            _lastCancelPressTime = DateTimeOffset.MinValue;
            clearCallback = _onCancelHintCleared;
          }
          clearCallback?.Invoke();
        }, null, CancelWindowMs, Timeout.Infinite);
      }
    }

    // Invoke callbacks outside the lock to avoid deadlock
    if (shouldShowHint)
    {
      Action? hintCallback;
      lock (_cancelLock)
      {
        hintCallback = _onCancelHintRequested;
      }
      hintCallback?.Invoke();
    }

    if (shouldCancel)
    {
      Action? cancelCallback;
      lock (_cancelLock)
      {
        cancelCallback = _onCancelRequested;
      }
      cancelCallback?.Invoke();
    }
  }

  /// <summary>
  /// Sets the cancel hint callbacks. Called by <see cref="CancellationScope"/> on construction.
  /// </summary>
  private void AttachCancellation(
    Action onCancelRequested,
    Action? onCancelHintRequested,
    Action? onCancelHintCleared)
  {
    lock (_cancelLock)
    {
      _onCancelRequested = onCancelRequested;
      _onCancelHintRequested = onCancelHintRequested;
      _onCancelHintCleared = onCancelHintCleared;
      _lastCancelPressTime = DateTimeOffset.MinValue;
    }
  }

  /// <summary>
  /// Clears the cancel callbacks. Called by <see cref="CancellationScope"/> on dispose.
  /// </summary>
  private void DetachCancellation()
  {
    lock (_cancelLock)
    {
      _cancelResetTimer?.Dispose();
      _cancelResetTimer = null;
      _onCancelRequested = null;
      _onCancelHintRequested = null;
      _onCancelHintCleared = null;
      _lastCancelPressTime = DateTimeOffset.MinValue;
    }
  }

  /// <summary>
  /// Action callbacks for cancel hint rendering. Set by the parent UI after construction.
  /// </summary>
  public Action? OnCancelHintRequested { get; set; }

  /// <summary>
  /// Action callback for clearing the cancel hint. Set by the parent UI after construction.
  /// </summary>
  public Action? OnCancelHintCleared { get; set; }

  /// <summary>
  /// Checks whether a modal overlay is currently active. When true, Esc dismisses the modal
  /// instead of triggering cancellation.
  /// </summary>
  public Func<bool>? IsModalActive { get; set; }

  /// <summary>
  /// Invoked when Esc is pressed while a modal is active.
  /// </summary>
  public Action? OnModalDismissRequested { get; set; }

  private sealed class CancellationScope : IDisposable
  {
    private readonly AsyncInputReader _reader;
    private bool _disposed;

    internal CancellationScope(AsyncInputReader reader, Action onCancelRequested)
    {
      _reader = reader;
      _reader.AttachCancellation(
        onCancelRequested,
        reader.OnCancelHintRequested,
        reader.OnCancelHintCleared);
    }

    public void Dispose()
    {
      if (_disposed) return;
      _disposed = true;
      _reader.DetachCancellation();
    }
  }
}
