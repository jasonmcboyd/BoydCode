using BoydCode.Application.Interfaces;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable IDE0060 // Remove unused parameter - context is required by the override signature

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class ContextUsageView : View
{
  private const int BarWidth = 72;

  private static readonly Attribute BoldDefault = new(ColorName16.White, Color.None, TextStyle.Bold);
  private static readonly Attribute BoldAccent = new(ColorName16.Blue, Color.None, TextStyle.Bold);
  private static readonly Attribute BoldSuccess = new(ColorName16.Green, Color.None, TextStyle.Bold);
  private static readonly Attribute BoldChart = new(Theme.Chart.Tools, Color.None, TextStyle.Bold);

  private readonly ContextUsageData _data;
  private readonly Segment[] _segments;
  private int _focusedSegment;

  internal ContextUsageView(ContextUsageData data)
  {
    _data = data;

    _segments =
    [
      new Segment("System prompt", data.SystemPromptTokens, Theme.Semantic.Accent, BoldAccent),
      new Segment("Tools", data.ToolTokensTotal, Theme.Chart.ToolsAttr, BoldChart),
      new Segment("Messages", data.MessageTokensTotal, Theme.Semantic.Success, BoldSuccess),
      new Segment("Free space", data.FreeTokens, Theme.Chart.FreeSpaceAttr, Theme.Chart.FreeSpaceAttr),
      new Segment("Compact buffer", data.BufferTokens, Theme.Chart.BufferAttr, Theme.Chart.BufferAttr),
    ];

    // Start focus on the first non-zero segment
    _focusedSegment = 0;
    for (var i = 0; i < _segments.Length; i++)
    {
      if (_segments[i].Tokens > 0)
      {
        _focusedSegment = i;
        break;
      }
    }
  }

  internal static int MeasureContentHeight() => 25;

  protected override bool OnDrawingContent(DrawContext? context)
  {
    var width = Viewport.Width;
    if (width <= 0) return true;

    var barCharWidths = ComputeBarWidths();
    var y = 0;

    // Y=0: Header line
    DrawHeader(y, width);
    y += 2;

    // Y=2: Stacked bar
    DrawStackedBar(y, barCharWidths);
    y++;

    // Y=3: Segment cursor
    DrawSegmentCursor(y, barCharWidths);
    y += 2;

    // Y=5-9: Legend
    DrawLegend(y);
    y += _segments.Length + 1;

    // Y=11-13: System prompt breakdown
    DrawSystemPromptBreakdown(y, width);
    y += 4;

    // Y=15-19: Messages breakdown
    DrawMessagesBreakdown(y, width);
    y += 6;

    // Y=21-22: Tools breakdown
    DrawToolsBreakdown(y, width);
    y += 3;

    // Y=24: Hint
    DrawHint(y, width);

    return true;
  }

  protected override bool OnKeyDown(Key keyEvent)
  {
    if (keyEvent == Key.CursorLeft)
    {
      MoveFocus(-1);
      keyEvent.Handled = true;
      return true;
    }

    if (keyEvent == Key.CursorRight)
    {
      MoveFocus(1);
      keyEvent.Handled = true;
      return true;
    }

    return base.OnKeyDown(keyEvent);
  }

  private void MoveFocus(int direction)
  {
    var barCharWidths = ComputeBarWidths();
    var next = _focusedSegment + direction;

    // Skip zero-width segments
    while (next >= 0 && next < _segments.Length && barCharWidths[next] <= 0)
    {
      next += direction;
    }

    if (next >= 0 && next < _segments.Length)
    {
      _focusedSegment = next;
      SetNeedsDraw();
    }
  }

  // ─── Drawing helpers ──────────────────────────────

  private void DrawHeader(int y, int width)
  {
    var usagePercent = _data.ContextLimit > 0
      ? (double)_data.TotalUsed / _data.ContextLimit * 100
      : 0;

    var usageAttr = usagePercent switch
    {
      < 50 => Theme.Semantic.Success,
      < 80 => Theme.Semantic.Warning,
      _ => Theme.Semantic.Error,
    };

    var x = 2;

    // Provider name (bold)
    SetAttribute(BoldDefault);
    Move(x, y);
    AddStr(_data.ProviderName);
    x += _data.ProviderName.Length;

    // Separator
    SetAttribute(Theme.Semantic.Muted);
    AddStr(" \u00b7 ");
    x += 3;

    // Model name (bold)
    SetAttribute(BoldDefault);
    Move(x, y);
    AddStr(_data.ModelName);
    x += _data.ModelName.Length;

    // Separator
    SetAttribute(Theme.Semantic.Muted);
    AddStr(" \u00b7 ");
    x += 3;

    // Usage stats (colored)
    var usageText = $"{TokenFormatting.FormatCompact(_data.TotalUsed)}/{TokenFormatting.FormatCompact(_data.ContextLimit)} tokens ({TokenFormatting.FormatPercent(usagePercent)})";
    SetAttribute(usageAttr);
    Move(x, y);
    AddStr(Truncate(usageText, width - x));
  }

  private void DrawStackedBar(int y, int[] barCharWidths)
  {
    var x = 2;
    Move(x, y);

    for (var i = 0; i < _segments.Length; i++)
    {
      if (barCharWidths[i] <= 0) continue;

      SetAttribute(_segments[i].Attr);
      var ch = i == 3 ? Theme.Symbols.LightShade : Theme.Symbols.FullBlock;
      AddStr(new string(ch, barCharWidths[i]));
    }
  }

  private void DrawSegmentCursor(int y, int[] barCharWidths)
  {
    // Find the midpoint of the focused segment
    var offset = 2; // initial indent
    for (var i = 0; i < _focusedSegment; i++)
    {
      offset += barCharWidths[i];
    }

    var segWidth = barCharWidths[_focusedSegment];
    if (segWidth <= 0) return;

    var midpoint = offset + segWidth / 2;
    SetAttribute(_segments[_focusedSegment].Attr);
    Move(midpoint, y);
    AddStr("^");
  }

  private void DrawLegend(int y)
  {
    for (var i = 0; i < _segments.Length; i++)
    {
      var seg = _segments[i];
      var percent = _data.ContextLimit > 0
        ? (double)seg.Tokens / _data.ContextLimit * 100
        : 0;

      var isFocused = i == _focusedSegment;
      var labelAttr = isFocused ? seg.BoldAttr : Theme.Semantic.Default;

      // Colored square
      SetAttribute(seg.Attr);
      Move(4, y + i);
      AddStr($"{Theme.Symbols.BlackSquare}");

      // Label
      SetAttribute(labelAttr);
      var label = $" {seg.Name,-18}";
      AddStr(label);

      // Token count + percent
      var tokenStr = TokenFormatting.FormatCompact(seg.Tokens);
      var percentStr = TokenFormatting.FormatPercent(percent);
      var stats = $"{tokenStr,8} tokens  ({percentStr})";
      SetAttribute(isFocused ? BoldDefault : Theme.Semantic.Muted);
      AddStr(stats);
    }
  }

  private void DrawSystemPromptBreakdown(int y, int width)
  {
    SetAttribute(BoldAccent);
    Move(2, y);
    AddStr("System prompt");

    SetAttribute(Theme.Semantic.Muted);
    AddStr(" \u00b7 ");

    SetAttribute(Theme.Semantic.Default);
    AddStr($"{TokenFormatting.FormatCompact(_data.SystemPromptTokens)} tokens");

    // Tree lines
    DrawTreeLine(y + 1, false, "Meta prompt", $"{TokenFormatting.FormatCompact(_data.MetaPromptTokens)} tokens");
    DrawTreeLine(y + 2, true, "Session prompt", $"{TokenFormatting.FormatCompact(_data.SessionPromptTokens)} tokens");
  }

  private void DrawMessagesBreakdown(int y, int width)
  {
    SetAttribute(BoldSuccess);
    Move(2, y);
    AddStr("Messages");

    SetAttribute(Theme.Semantic.Muted);
    AddStr(" \u00b7 ");

    SetAttribute(Theme.Semantic.Default);
    AddStr($"{_data.TotalMessageCount} messages, {TokenFormatting.FormatCompact(_data.MessageTokensTotal)} tokens");

    // Tree lines
    DrawTreeLine(y + 1, false, "User text", $"{_data.UserTextCount} messages, {TokenFormatting.FormatCompact(_data.UserTextTokens)} tokens");
    DrawTreeLine(y + 2, false, "Assistant text", $"{_data.AssistantTextCount} messages, {TokenFormatting.FormatCompact(_data.AssistantTextTokens)} tokens");
    DrawTreeLine(y + 3, false, "Tool calls", $"{_data.ToolCallCount} calls, {TokenFormatting.FormatCompact(_data.ToolCallTokens)} tokens");
    DrawTreeLine(y + 4, true, "Tool results", $"{_data.ToolResultCount} results, {TokenFormatting.FormatCompact(_data.ToolResultTokens)} tokens");
  }

  private void DrawToolsBreakdown(int y, int width)
  {
    SetAttribute(BoldChart);
    Move(2, y);
    AddStr("Tools");

    SetAttribute(Theme.Semantic.Muted);
    AddStr(" \u00b7 ");

    SetAttribute(Theme.Semantic.Default);
    AddStr($"1 tool, {TokenFormatting.FormatCompact(_data.ToolTokensTotal)} tokens");

    DrawTreeLine(y + 1, true, _data.ToolName, $"{TokenFormatting.FormatCompact(_data.ToolTokensTotal)} tokens");
  }

  private void DrawTreeLine(int y, bool isLast, string label, string value)
  {
    var connector = isLast ? "\u2514\u2500\u2500" : "\u251c\u2500\u2500";
    SetAttribute(Theme.Semantic.Muted);
    Move(2, y);
    AddStr(connector);
    AddStr(" ");

    SetAttribute(Theme.Semantic.Default);
    var paddedLabel = label.Length < 20 ? label.PadRight(20) : label;
    AddStr(paddedLabel);

    SetAttribute(Theme.Semantic.Muted);
    AddStr(value);
  }

  private void DrawHint(int y, int width)
  {
    var hint = $"{Theme.Symbols.ArrowLeft}/{Theme.Symbols.ArrowRight}: browse segments  Esc: dismiss";
    SetAttribute(Theme.Semantic.Muted);
    Move(2, y);
    AddStr(Truncate(hint, width - 2));
  }

  // ─── Bar width calculation ────────────────────────

  private int[] ComputeBarWidths()
  {
    var charWidths = new int[_segments.Length];

    if (_data.ContextLimit <= 0) return charWidths;

    var nonZeroCount = 0;
    for (var i = 0; i < _segments.Length; i++)
    {
      if (_segments[i].Tokens > 0) nonZeroCount++;
    }

    var reserved = nonZeroCount; // 1 char minimum each
    var remaining = BarWidth - reserved;

    var totalTokens = 0;
    for (var i = 0; i < _segments.Length; i++)
    {
      totalTokens += _segments[i].Tokens;
    }

    if (totalTokens > 0 && remaining > 0)
    {
      for (var i = 0; i < _segments.Length; i++)
      {
        if (_segments[i].Tokens > 0)
        {
          charWidths[i] = 1 + (int)((long)_segments[i].Tokens * remaining / totalTokens);
        }
      }
    }
    else
    {
      for (var i = 0; i < _segments.Length; i++)
      {
        charWidths[i] = _segments[i].Tokens > 0 ? 1 : 0;
      }
    }

    // Adjust to exactly BarWidth
    var currentTotal = 0;
    for (var i = 0; i < charWidths.Length; i++)
    {
      currentTotal += charWidths[i];
    }

    var diff = BarWidth - currentTotal;
    if (diff != 0)
    {
      // Prefer adjusting free space (index 3), otherwise the largest segment
      var adjustIdx = 3;
      if (charWidths[adjustIdx] == 0)
      {
        adjustIdx = 0;
        for (var i = 1; i < charWidths.Length; i++)
        {
          if (charWidths[i] > charWidths[adjustIdx])
          {
            adjustIdx = i;
          }
        }
      }

      charWidths[adjustIdx] += diff;
      if (charWidths[adjustIdx] < 0) charWidths[adjustIdx] = 0;
    }

    return charWidths;
  }

  private static string Truncate(string text, int maxWidth)
  {
    if (maxWidth <= 0) return string.Empty;
    return text.Length <= maxWidth ? text : text[..maxWidth];
  }

  // ─── Internal types ───────────────────────────────

  private sealed record Segment(string Name, int Tokens, Attribute Attr, Attribute BoldAttr);
}
