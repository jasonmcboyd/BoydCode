using System.Diagnostics;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class ExecutionWindow : IDisposable
{
  private const int WindowSize = 5;
  private const int MaxBufferLines = 10_000;

  private static readonly char[] s_spinnerFrames =
    ['⠿', '⠻', '⠽', '⠾', '⠷', '⠯', '⠟', '⠾'];

  private readonly object _consoleLock;
  private readonly Stopwatch _stopwatch = new();
  private readonly Queue<string> _outputBuffer = new();

  // Spinner state
  private CancellationTokenSource? _spinnerCts;
  private Task? _spinnerTask;

  // Window rendering state
  private int _windowDisplayLines;
  private bool _windowActive;
  private bool _useContainedOutput;
  private DateTime _lastRedraw = DateTime.MinValue;
  private bool _redrawPending;

  // Post-execution state for /expand
  private List<string>? _lastOutputBuffer;
  private bool _lastOutputExpanded;

  // State tracking
  private ExecutionState _state = ExecutionState.Inactive;

  public ExecutionWindow(object consoleLock)
  {
    _consoleLock = consoleLock;
  }

  public int OutputLineCount => _outputBuffer.Count;

  public void Start(bool useContainedOutput)
  {
    lock (_consoleLock)
    {
      _state = ExecutionState.Waiting;
      _useContainedOutput = useContainedOutput;
      _outputBuffer.Clear();
      _windowDisplayLines = 0;
      _windowActive = false;
      _lastRedraw = DateTime.MinValue;
      _redrawPending = false;
      _stopwatch.Restart();

      _spinnerCts = new CancellationTokenSource();
      _spinnerTask = Task.Run(() => RunSpinnerAsync(_spinnerCts.Token));
    }
  }

  public void AddOutputLine(string line)
  {
    lock (_consoleLock)
    {
      if (_state == ExecutionState.Waiting)
      {
        // Transition from Waiting to Streaming: kill the spinner
        StopSpinner();
        _state = ExecutionState.Streaming;
      }

      if (_useContainedOutput)
      {
        if (_outputBuffer.Count >= MaxBufferLines)
        {
          _outputBuffer.Dequeue();
        }
        _outputBuffer.Enqueue(line);
        RedrawWindow(bypassThrottle: false);
      }
      else
      {
        System.Console.Write("  ");
        System.Console.WriteLine(line);
      }
    }
  }

  public TimeSpan Stop()
  {
    lock (_consoleLock)
    {
      StopSpinner();
      _stopwatch.Stop();

      if (_useContainedOutput && _redrawPending)
      {
        RedrawWindow(bypassThrottle: true);
      }

      // Clear any residual spinner or "Executing..." text
      System.Console.Write("\r                                                  \r");

      var elapsed = _stopwatch.Elapsed;
      _state = ExecutionState.Inactive;
      return elapsed;
    }
  }

  public void RenderToolResult(string toolName, string result, bool isError, bool outputStreamed)
  {
    lock (_consoleLock)
    {
      var duration = FormatDuration(_stopwatch.Elapsed);
      var lineCount = _outputBuffer.Count;

      if (_useContainedOutput)
      {
        // Save output for /expand before clearing
        _lastOutputBuffer = new List<string>(_outputBuffer);
        _lastOutputExpanded = false;

        if (lineCount > WindowSize)
        {
          // Collapse the window: move cursor up and erase the visible lines
          System.Console.Write($"\x1b[{_windowDisplayLines}A");
          System.Console.Write("\x1b[0J");

          if (isError)
          {
            AnsiConsole.MarkupLine(
              $"  [red][[{Markup.Escape(toolName)} error]][/] [dim]{lineCount} lines | {duration}[/]" +
              $"  [dim italic](/expand to show full output)[/]");
          }
          else
          {
            AnsiConsole.MarkupLine(
              $"  [green][[{Markup.Escape(toolName)}]][/] [dim]{lineCount} lines | {duration}[/]" +
              $"  [dim italic](/expand to show full output)[/]");
          }
        }
        else if (lineCount > 0)
        {
          // Lines are already visible, just render the summary below
          if (isError)
          {
            AnsiConsole.MarkupLine(
              $"  [red][[{Markup.Escape(toolName)} error]][/] [dim]{lineCount} lines | {duration}[/]");
          }
          else
          {
            AnsiConsole.MarkupLine(
              $"  [green][[{Markup.Escape(toolName)}]][/] [dim]{lineCount} lines | {duration}[/]");
          }
        }
        else
        {
          // No output at all
          if (isError)
          {
            AnsiConsole.MarkupLine(
              $"  [red][[{Markup.Escape(toolName)} error]][/] {Markup.Escape(Truncate(result, 500))}");
          }
          else
          {
            var summary = Truncate(result, 200);
            AnsiConsole.MarkupLine(
              $"  [green][[{Markup.Escape(toolName)}]][/] [dim]{Markup.Escape(summary)}[/]");
          }
        }

        _outputBuffer.Clear();
        _windowDisplayLines = 0;
        _windowActive = false;
      }
      else
      {
        // Non-ANSI fallback: original behavior
        _lastOutputBuffer = null;
        if (isError)
        {
          AnsiConsole.MarkupLine(
            $"  [red][[{Markup.Escape(toolName)} error]][/] {Markup.Escape(Truncate(result, 500))}");
        }
        else
        {
          var summary = Truncate(result, 200);
          AnsiConsole.MarkupLine(
            $"  [green][[{Markup.Escape(toolName)}]][/] [dim]{Markup.Escape(summary)}[/]");
        }
      }
    }
  }

  public void ExpandLastToolOutput()
  {
    if (_lastOutputBuffer is null || _lastOutputBuffer.Count == 0)
    {
      AnsiConsole.MarkupLine("[dim]No tool output to expand.[/]");
      return;
    }

    if (_lastOutputExpanded)
    {
      AnsiConsole.MarkupLine("[dim]Output already expanded.[/]");
      return;
    }

    _lastOutputExpanded = true;
    foreach (var line in _lastOutputBuffer)
    {
      System.Console.Write("  ");
      System.Console.WriteLine(line);
    }
  }

  public void Dispose()
  {
    StopSpinner();
  }

  /// <summary>
  /// Clears the current spinner or "Executing..." text so a cancel hint can be shown.
  /// Returns true if the window was in Waiting state (caller should restore after clearing hint).
  /// </summary>
  internal bool ClearForCancelHint()
  {
    // Must be called under _consoleLock by the parent
    if (_state == ExecutionState.Waiting)
    {
      // The spinner is writing "Executing..." — stop it and clear the line
      StopSpinner();
      System.Console.Write("\r                                                  \r");
      return true;
    }
    return false;
  }

  /// <summary>
  /// Restores the spinner after a cancel hint was cleared, if still in a pre-output state.
  /// </summary>
  internal void RestoreAfterCancelHint()
  {
    // Must be called under _consoleLock by the parent
    if (_state == ExecutionState.Waiting)
    {
      // Restart spinner since we stopped it for the cancel hint
      _spinnerCts = new CancellationTokenSource();
      _spinnerTask = Task.Run(() => RunSpinnerAsync(_spinnerCts.Token));
    }
  }

  private async Task RunSpinnerAsync(CancellationToken ct)
  {
    var frameIndex = 0;
    try
    {
      while (!ct.IsCancellationRequested)
      {
        var frame = s_spinnerFrames[frameIndex % s_spinnerFrames.Length];
        var elapsed = FormatDuration(_stopwatch.Elapsed);

        lock (_consoleLock)
        {
          if (ct.IsCancellationRequested) break;
          System.Console.Write($"\r  {frame} Executing... ({elapsed})");
        }

        frameIndex++;
        await Task.Delay(100, ct).ConfigureAwait(false);
      }
    }
    catch (OperationCanceledException)
    {
      // Expected when spinner is cancelled
    }
  }

  private void StopSpinner()
  {
    // Must be called under _consoleLock or when safe to do so
    if (_spinnerCts is not null)
    {
      _spinnerCts.Cancel();
      try
      {
        _spinnerTask?.Wait(TimeSpan.FromMilliseconds(200));
      }
      catch
      {
        // Best effort — spinner is non-critical
      }
      _spinnerCts.Dispose();
      _spinnerCts = null;
      _spinnerTask = null;
    }
  }

  private void RedrawWindow(bool bypassThrottle)
  {
    if (_outputBuffer.Count == 0) return;

    if (!bypassThrottle)
    {
      var now = DateTime.UtcNow;
      if ((now - _lastRedraw).TotalMilliseconds < 50)
      {
        _redrawPending = true;
        return;
      }
      _lastRedraw = now;
    }

    _redrawPending = false;

    if (!_windowActive)
    {
      // First output line: clear any residual text
      System.Console.Write("\r                                                  \r");
      _windowActive = true;
    }

    int termWidth;
    try { termWidth = System.Console.WindowWidth; }
    catch { termWidth = 120; }
    var maxWidth = Math.Max(termWidth - 6, 10);

    var elapsed = FormatDuration(_stopwatch.Elapsed);

    if (_outputBuffer.Count <= WindowSize)
    {
      // Still filling the window: just write the newest line
      if (_windowDisplayLines < _outputBuffer.Count)
      {
        var newest = _outputBuffer.Last();
        System.Console.Write("  ");
        System.Console.WriteLine(TruncateForDisplay(newest, maxWidth));
        _windowDisplayLines = _outputBuffer.Count;
      }
    }
    else
    {
      // Window is full: cursor up and rewrite the visible lines
      if (_windowDisplayLines > 0)
      {
        System.Console.Write($"\x1b[{_windowDisplayLines}A");
      }

      var tail = _outputBuffer.Skip(_outputBuffer.Count - WindowSize).ToArray();
      for (var i = 0; i < WindowSize; i++)
      {
        System.Console.Write("\x1b[2K");
        var displayLine = TruncateForDisplay(tail[i], maxWidth);
        if (i == 0)
        {
          // Show line counter with elapsed time right-aligned on the first visible line
          var counter = $" [{_outputBuffer.Count} lines | {elapsed}]";
          var contentWidth = maxWidth - counter.Length;
          if (contentWidth > 0 && displayLine.Length > contentWidth)
          {
            displayLine = displayLine[..contentWidth];
          }
          System.Console.Write("  ");
          System.Console.Write(displayLine);
          System.Console.Write($"\x1b[{maxWidth + 4}G");
          System.Console.Write($"\x1b[2m{counter}\x1b[22m");
          System.Console.WriteLine();
        }
        else
        {
          System.Console.Write("  ");
          System.Console.WriteLine(displayLine);
        }
      }

      _windowDisplayLines = WindowSize;
    }
  }

  private static string TruncateForDisplay(string line, int maxWidth)
  {
    if (line.Length <= maxWidth) return line;
    return maxWidth > 3 ? line[..(maxWidth - 3)] + "..." : line[..maxWidth];
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

  private static string Truncate(string text, int maxLength)
  {
    if (text.Length <= maxLength)
    {
      return text;
    }

    return text[..maxLength] + "...";
  }

  private enum ExecutionState
  {
    Inactive,
    Waiting,
    Streaming,
  }
}
