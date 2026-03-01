using System.Diagnostics;
using BoydCode.Presentation.Console.Renderables;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class ExecutionWindow
{
  private const int MaxBufferLines = 10_000;

  private readonly TuiLayout? _layout;
  private readonly Stopwatch _stopwatch = new();
  private readonly Queue<string> _outputBuffer = new();

  // Post-execution state for /expand
  private List<string>? _lastOutputBuffer;
  private bool _lastOutputExpanded;

  public ExecutionWindow(TuiLayout? layout = null)
  {
    _layout = layout;
  }

  public int OutputLineCount => _outputBuffer.Count;

  public void Start()
  {
    _outputBuffer.Clear();
    _stopwatch.Restart();
    _layout?.SetActivity(ActivityState.Executing);
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
    _layout?.SetActivity(ActivityState.Idle);
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
      var badge = isError
        ? ConversationRenderables.ToolResultError(toolName, lineCount, duration)
        : ConversationRenderables.ToolResultSuccess(toolName, lineCount, duration);
      _layout?.AddContent(badge);
      _layout?.AddContent(ConversationRenderables.ExpandHint());
    }
    else if (lineCount > 0)
    {
      // Show the lines inline, then summary
      foreach (var line in _lastOutputBuffer)
      {
        _layout?.AddContentLine($"  {line}");
      }
      var badge = isError
        ? ConversationRenderables.ToolResultError(toolName, lineCount, duration)
        : ConversationRenderables.ToolResultSuccess(toolName, lineCount, duration);
      _layout?.AddContent(badge);
    }
    else
    {
      // No output — show summary or error message
      if (isError)
      {
        _layout?.AddContent(ConversationRenderables.ToolResultErrorWithMessage(toolName, Truncate(result, 500)));
      }
      else
      {
        _layout?.AddContent(ConversationRenderables.ToolResultSuccessWithSummary(toolName, Truncate(result, 200)));
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
