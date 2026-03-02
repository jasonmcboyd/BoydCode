using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace BoydCode.Presentation.Console.Terminal;

internal static class ConversationBlockRenderer
{
  // Semantic color palette — Color.None inherits the terminal's default background
  private static readonly Attribute SuccessAttr = new(ColorName16.Green, Color.None);
  private static readonly Attribute ErrorAttr = new(ColorName16.BrightRed, Color.None);
  private static readonly Attribute WarningAttr = new(ColorName16.Yellow, Color.None);
  private static readonly Attribute InfoAttr = new(ColorName16.Cyan, Color.None);
  private static readonly Attribute AccentAttr = new(ColorName16.Blue, Color.None);
  private static readonly Attribute MutedAttr = new(ColorName16.DarkGray, Color.None);
  private static readonly Attribute DefaultAttr = new(ColorName16.White, Color.None);

  private static readonly Color UserBg = new(50, 50, 50);
  private static readonly Attribute UserAttr = new(ColorName16.White, UserBg);
  private static readonly Attribute UserPrefixAttr = new(ColorName16.DarkGray, UserBg);

  private static readonly Attribute BoxAttr = new(ColorName16.DarkGray, Color.None);

  public static int MeasureHeight(ConversationBlock block, int width)
  {
    if (width <= 0)
    {
      return 1;
    }

    return block switch
    {
      UserMessageBlock b => MeasureWrapped(b.Text, width - 2), // "> " prefix
      AssistantTextBlock b => MeasureWrapped(b.Text, width - 2), // 2-space indent
      ToolCallConversationBlock b => MeasureToolCall(b, width),
      ToolResultConversationBlock => 1,
      ExpandHintBlock => 1,
      TokenUsageBlock => 1,
      SeparatorBlock => 1,
      SectionBlock => 1,
      StatusMessageBlock b => MeasureWrapped(b.Text, width - 2),
      PlainTextBlock b => MeasureWrapped(b.Text, width),
      BannerBlock b => BannerRenderer.MeasureBanner(b.Data, width),
      _ => 1,
    };
  }

  public static void Draw(View view, ConversationBlock block, int y, int width)
  {
    if (width <= 0)
    {
      return;
    }

    switch (block)
    {
      case UserMessageBlock b:
        DrawUserMessage(view, b, y, width);
        break;
      case AssistantTextBlock b:
        DrawAssistantText(view, b, y, width);
        break;
      case ToolCallConversationBlock b:
        DrawToolCall(view, b, y, width);
        break;
      case ToolResultConversationBlock b:
        DrawToolResult(view, b, y, width);
        break;
      case ExpandHintBlock:
        DrawExpandHint(view, y);
        break;
      case TokenUsageBlock b:
        DrawTokenUsage(view, b, y);
        break;
      case SeparatorBlock:
        // Blank line, nothing to draw
        break;
      case SectionBlock b:
        DrawSection(view, b, y, width);
        break;
      case StatusMessageBlock b:
        DrawStatusMessage(view, b, y, width);
        break;
      case PlainTextBlock b:
        DrawPlainText(view, b, y, width);
        break;
      case BannerBlock b:
        BannerRenderer.DrawBanner(view, b.Data, y, width);
        break;
    }
  }

  // -----------------------------------------------
  //  Block drawing implementations
  // -----------------------------------------------

  private static void DrawUserMessage(View view, UserMessageBlock block, int y, int width)
  {
    var lines = WordWrap(block.Text, width - 2);
    foreach (var line in lines)
    {
      // Fill entire row with user background
      view.SetAttribute(UserAttr);
      view.Move(0, y);
      view.AddStr(new string(' ', width));

      // Draw "> " prefix
      view.Move(0, y);
      view.SetAttribute(UserPrefixAttr);
      view.AddStr("> ");

      // Draw text
      view.SetAttribute(UserAttr);
      view.AddStr(Truncate(line, width - 2));
      y++;
    }
  }

  private static void DrawAssistantText(View view, AssistantTextBlock block, int y, int width)
  {
    var lines = WordWrap(block.Text, width - 2);
    view.SetAttribute(DefaultAttr);
    foreach (var line in lines)
    {
      view.Move(0, y);
      view.AddStr("  ");
      view.AddStr(Truncate(line, width - 2));
      y++;
    }
  }

  private static void DrawToolCall(View view, ToolCallConversationBlock block, int y, int width)
  {
    var innerWidth = Math.Max(width - 2, 1); // 1 char left + 1 char right border

    // Top border: ┌─ Shell ─────────┐
    view.SetAttribute(BoxAttr);
    view.Move(0, y);
    var header = $"\u250c\u2500 {block.ToolName} ";
    var remaining = width - header.Length - 1; // -1 for ┐
    if (remaining > 0)
    {
      header += new string('\u2500', remaining);
    }
    header += "\u2510";
    view.AddStr(Truncate(header, width));
    y++;

    // Content lines: │ preview text │
    var previewLines = WordWrap(block.Preview, innerWidth - 2); // -2 for "│ " and " │" padding
    foreach (var line in previewLines)
    {
      view.SetAttribute(BoxAttr);
      view.Move(0, y);
      view.AddStr("\u2502 ");
      view.SetAttribute(DefaultAttr);
      var padded = line.PadRight(innerWidth - 2);
      view.AddStr(Truncate(padded, innerWidth - 2));
      view.SetAttribute(BoxAttr);
      view.AddStr(" \u2502");
      y++;
    }

    // Bottom border: └──────────────────┘
    view.SetAttribute(BoxAttr);
    view.Move(0, y);
    var bottom = "\u2514" + new string('\u2500', Math.Max(width - 2, 0)) + "\u2518";
    view.AddStr(Truncate(bottom, width));
  }

  private static void DrawToolResult(View view, ToolResultConversationBlock block, int y, int width)
  {
    view.Move(0, y);
    view.AddStr("  ");

    if (block.IsError)
    {
      view.SetAttribute(ErrorAttr);
      view.AddStr("\u2717 ");
      view.AddStr(block.ToolName);
      view.AddStr(" error");
    }
    else
    {
      view.SetAttribute(SuccessAttr);
      view.AddStr("\u2713 ");
      view.AddStr(block.ToolName);
    }

    view.SetAttribute(MutedAttr);
    if (block.LineCount > 0)
    {
      view.AddStr($"  {block.LineCount} lines | {block.Duration}");
    }
    else
    {
      var suffix = block.IsError ? " error" : " Command completed successfully.";
      view.AddStr($"  {suffix}");
    }
  }

  private static void DrawExpandHint(View view, int y)
  {
    view.SetAttribute(MutedAttr);
    view.Move(0, y);
    view.AddStr("  /expand to show full output");
  }

  private static void DrawTokenUsage(View view, TokenUsageBlock block, int y)
  {
    view.SetAttribute(MutedAttr);
    view.Move(0, y);
    var total = block.InputTokens + block.OutputTokens;
    view.AddStr($"  {block.InputTokens:N0} in / {block.OutputTokens:N0} out / {total:N0} total");
  }

  private static void DrawSection(View view, SectionBlock block, int y, int width)
  {
    view.SetAttribute(MutedAttr);
    view.Move(0, y);

    var title = $" {block.Title} ";
    var sideLen = Math.Max((width - title.Length) / 2, 2);
    var left = new string('\u2500', sideLen);
    var right = new string('\u2500', sideLen);
    var rule = $"{left}{title}{right}";
    view.AddStr(Truncate(rule, width));
  }

  private static void DrawStatusMessage(View view, StatusMessageBlock block, int y, int width)
  {
    var attr = block.Kind switch
    {
      MessageKind.Success => SuccessAttr,
      MessageKind.Error => ErrorAttr,
      MessageKind.Warning => WarningAttr,
      MessageKind.Hint => MutedAttr,
      _ => DefaultAttr,
    };

    view.SetAttribute(attr);
    var lines = WordWrap(block.Text, width - 2);
    foreach (var line in lines)
    {
      view.Move(0, y);
      view.AddStr("  ");
      view.AddStr(Truncate(line, width - 2));
      y++;
    }
  }

  private static void DrawPlainText(View view, PlainTextBlock block, int y, int width)
  {
    view.SetAttribute(DefaultAttr);
    var lines = WordWrap(block.Text, width);
    foreach (var line in lines)
    {
      view.Move(0, y);
      view.AddStr(Truncate(line, width));
      y++;
    }
  }

  // -----------------------------------------------
  //  Measurement helpers
  // -----------------------------------------------

  private static int MeasureWrapped(string text, int width)
  {
    if (width <= 0)
    {
      return 1;
    }

    var lines = WordWrap(text, width);
    return Math.Max(lines.Count, 1);
  }

  private static int MeasureToolCall(ToolCallConversationBlock block, int width)
  {
    var innerWidth = Math.Max(width - 4, 1); // borders + padding
    var previewLines = WordWrap(block.Preview, innerWidth);
    return 2 + previewLines.Count; // top border + content + bottom border
  }

  // -----------------------------------------------
  //  Text helpers
  // -----------------------------------------------

  internal static List<string> WordWrap(string text, int width)
  {
    var result = new List<string>();

    if (string.IsNullOrEmpty(text) || width <= 0)
    {
      result.Add(string.Empty);
      return result;
    }

    // Split on existing newlines first
    var paragraphs = text.Split('\n');
    foreach (var paragraph in paragraphs)
    {
      if (paragraph.Length <= width)
      {
        result.Add(paragraph);
        continue;
      }

      // Word-wrap this paragraph
      var pos = 0;
      while (pos < paragraph.Length)
      {
        if (pos + width >= paragraph.Length)
        {
          result.Add(paragraph[pos..]);
          break;
        }

        // Find last space within width
        var breakAt = paragraph.LastIndexOf(' ', pos + width - 1, width);
        if (breakAt <= pos)
        {
          // No space found, hard break at width
          result.Add(paragraph.Substring(pos, width));
          pos += width;
        }
        else
        {
          result.Add(paragraph[pos..breakAt]);
          pos = breakAt + 1; // skip the space
        }
      }
    }

    if (result.Count == 0)
    {
      result.Add(string.Empty);
    }

    return result;
  }

  private static string Truncate(string text, int maxWidth)
  {
    if (maxWidth <= 0)
    {
      return string.Empty;
    }

    return text.Length <= maxWidth ? text : text[..maxWidth];
  }
}
