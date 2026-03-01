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
    _layout?.SetIndicator(IndicatorState.Executing);
  }

  public void AddOutputLine(string line)
  {
    if (_outputBuffer.Count >= MaxBufferLines)
    {
      _outputBuffer.Dequeue();
    }
    _outputBuffer.Enqueue(line);

    // Stream the line to the layout so the user sees it live
    if (_layout is not null && _layout.IsActive)
    {
      _layout.AddContentLine($"  {line}");
    }
  }

  public TimeSpan Stop()
  {
    _stopwatch.Stop();
    _layout?.SetIndicator(IndicatorState.Idle);
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
      RenderRenderable(badge);
      RenderRenderable(ConversationRenderables.ExpandHint());
    }
    else if (lineCount > 0)
    {
      // Show the lines inline, then summary
      foreach (var line in _lastOutputBuffer)
      {
        RenderLine($"  {line}");
      }
      var badge = isError
        ? ConversationRenderables.ToolResultError(toolName, lineCount, duration)
        : ConversationRenderables.ToolResultSuccess(toolName, lineCount, duration);
      RenderRenderable(badge);
    }
    else
    {
      // No output — show summary or error message
      if (isError)
      {
        RenderRenderable(ConversationRenderables.ToolResultErrorWithMessage(toolName, Truncate(result, 500)));
      }
      else
      {
        RenderRenderable(ConversationRenderables.ToolResultSuccessWithSummary(toolName, Truncate(result, 200)));
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

  public void ExpandLastToolOutput()
  {
    if (_lastOutputBuffer is null || _lastOutputBuffer.Count == 0)
    {
      RenderBadge("[dim]No tool output to expand.[/]");
      return;
    }

    if (_lastOutputExpanded)
    {
      RenderBadge("[dim]Output already expanded.[/]");
      return;
    }

    _lastOutputExpanded = true;
    foreach (var line in _lastOutputBuffer)
    {
      RenderLine($"  {line}");
    }
  }

  private void RenderRenderable(IRenderable renderable)
  {
    if (_layout is not null && _layout.IsActive)
    {
      _layout.AddContent(renderable);
    }
    else
    {
      AnsiConsole.Write(renderable);
      AnsiConsole.WriteLine();
    }
  }

  private void RenderBadge(string markup)
  {
    if (_layout is not null && _layout.IsActive)
    {
      _layout.AddContentMarkup(markup);
    }
    else
    {
      AnsiConsole.MarkupLine(markup);
    }
  }

  private void RenderLine(string text)
  {
    if (_layout is not null && _layout.IsActive)
    {
      _layout.AddContentLine(text);
    }
    else
    {
      System.Console.WriteLine(text);
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

  private static string Truncate(string text, int maxLength)
  {
    if (text.Length <= maxLength)
    {
      return text;
    }

    return text[..maxLength] + "...";
  }
}
