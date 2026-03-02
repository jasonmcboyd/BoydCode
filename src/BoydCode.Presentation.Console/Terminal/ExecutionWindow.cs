using System.Diagnostics;

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class ExecutionWindow
{
  private const int MaxBufferLines = 10_000;

  private readonly Stopwatch _stopwatch = new();
  private readonly Queue<string> _outputBuffer = new();

  // Post-execution state for /expand
  private List<string>? _lastOutputBuffer;
  private bool _lastOutputExpanded;

  /// <summary>
  /// Callback to add a conversation block to the UI. Set by SpectreUserInterface
  /// when the Terminal.Gui toplevel is available.
  /// </summary>
  internal Action<ConversationBlock>? AddBlock { get; set; }

  public int OutputLineCount => _outputBuffer.Count;

  public void Start()
  {
    _outputBuffer.Clear();
    _stopwatch.Restart();
  }

  public void AddOutputLine(string line)
  {
    if (_outputBuffer.Count >= MaxBufferLines)
    {
      _outputBuffer.Dequeue();
    }
    _outputBuffer.Enqueue(line);
  }

  public TimeSpan Stop()
  {
    _stopwatch.Stop();
    return _stopwatch.Elapsed;
  }

  public void RenderToolResult(string toolName, string result, bool isError)
  {
    var duration = FormatDuration(_stopwatch.Elapsed);
    var lineCount = _outputBuffer.Count;

    // Save output for /expand
    _lastOutputBuffer = new List<string>(_outputBuffer);
    _lastOutputExpanded = false;
    _outputBuffer.Clear();

    if (lineCount > 5)
    {
      // Collapsed view with /expand hint
      var badge = new ToolResultConversationBlock(toolName, lineCount, duration, isError);
      AddBlock?.Invoke(badge);
      AddBlock?.Invoke(new ExpandHintBlock());
    }
    else if (lineCount > 0)
    {
      // Show the lines inline, then summary
      foreach (var line in _lastOutputBuffer)
      {
        AddBlock?.Invoke(new PlainTextBlock($"  {line}"));
      }
      var badge = new ToolResultConversationBlock(toolName, lineCount, duration, isError);
      AddBlock?.Invoke(badge);
    }
    else
    {
      // No output -- show summary or error message
      if (isError)
      {
        var msg = Truncate(result, 500);
        AddBlock?.Invoke(new ToolResultConversationBlock(toolName, 0, msg, IsError: true));
      }
      else
      {
        var msg = Truncate(result, 200);
        AddBlock?.Invoke(new ToolResultConversationBlock(toolName, 0, msg, IsError: false));
      }
    }
  }

  public IReadOnlyList<string>? GetLastOutput()
  {
    if (_lastOutputBuffer is null || _lastOutputBuffer.Count == 0)
    {
      return null;
    }

    return _lastOutputBuffer;
  }

  public bool IsLastOutputExpanded => _lastOutputExpanded;

  public void MarkLastOutputExpanded()
  {
    _lastOutputExpanded = true;
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
}
