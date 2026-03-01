using Spectre.Console;
using Spectre.Console.Rendering;

namespace BoydCode.Presentation.Console.Renderables;

internal static class ConversationRenderables
{
  /// <summary>
  /// User message: "  > {text}" with bold blue ">".
  /// </summary>
  public static IRenderable UserMessage(string text)
  {
    return new Markup($"  [bold blue]>[/] {Markup.Escape(text)}");
  }

  /// <summary>
  /// Assistant text block: indented plain text (no border panel).
  /// </summary>
  public static IRenderable AssistantText(string text)
  {
    return new Markup($"  {Markup.Escape(text)}");
  }

  /// <summary>
  /// Tool call preview panel: rounded border, grey, tool name header.
  /// </summary>
  public static IRenderable ToolCallBadge(string toolName, string preview)
  {
    return new Panel(Markup.Escape(preview))
      .Header($"[dim]{Markup.Escape(toolName)}[/]")
      .Border(BoxBorder.Rounded)
      .BorderColor(Color.Grey)
      .Padding(1, 0);
  }

  /// <summary>
  /// Tool result success badge: checkmark + tool name + line count and duration.
  /// </summary>
  public static IRenderable ToolResultSuccess(string toolName, int lineCount, string duration)
  {
    if (lineCount > 0)
    {
      return new Markup($"  [green]\u2713[/] [dim]{Markup.Escape(toolName)}  {lineCount} lines | {duration}[/]");
    }

    return new Markup($"  [green]\u2713[/] [dim]{Markup.Escape(toolName)}  Command completed successfully.[/]");
  }

  /// <summary>
  /// Tool result success badge with a summary string instead of line count.
  /// </summary>
  public static IRenderable ToolResultSuccessWithSummary(string toolName, string summary)
  {
    return new Markup($"  [green]\u2713[/] [dim]{Markup.Escape(toolName)}  {Markup.Escape(summary)}[/]");
  }

  /// <summary>
  /// Tool result error badge: cross + tool name + line count and duration.
  /// </summary>
  public static IRenderable ToolResultError(string toolName, int lineCount, string duration)
  {
    if (lineCount > 0)
    {
      return new Markup($"  [red]\u2717[/] [dim]{Markup.Escape(toolName)} error  {lineCount} lines | {duration}[/]");
    }

    return new Markup($"  [red]\u2717[/] [dim]{Markup.Escape(toolName)} error[/]");
  }

  /// <summary>
  /// Tool result error badge with a specific error message.
  /// </summary>
  public static IRenderable ToolResultErrorWithMessage(string toolName, string message)
  {
    return new Markup($"  [red]\u2717[/] [dim]{Markup.Escape(toolName)}  {Markup.Escape(message)}[/]");
  }

  /// <summary>
  /// Expand hint shown after collapsed tool output.
  /// </summary>
  public static IRenderable ExpandHint()
  {
    return new Markup("  [dim italic]/expand to show full output[/]");
  }

  /// <summary>
  /// Token usage display: cumulative tokens in dim text.
  /// </summary>
  public static IRenderable TokenUsage(int inputTokens, int outputTokens)
  {
    var total = inputTokens + outputTokens;
    return new Markup(
      $"  [dim]{inputTokens:N0} in / {outputTokens:N0} out / {total:N0} total[/]");
  }

  /// <summary>
  /// Turn separator: blank line between conversation turns.
  /// </summary>
  public static IRenderable TurnSeparator()
  {
    return new Text("");
  }
}
