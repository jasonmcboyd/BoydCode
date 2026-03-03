using BoydCode.Application.Interfaces;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable IDE0060 // Remove unused parameter - context is required by the override signature

namespace BoydCode.Presentation.Console.Terminal;

/// <summary>
/// A custom view that draws structured key-value content using Terminal.Gui native drawing.
/// Used inside modal windows for <c>/project show</c>, <c>/provider show</c>, etc.
/// </summary>
internal sealed class DetailModalView : View
{
  private readonly IReadOnlyList<DetailSection> _sections;
  private readonly int _labelColumnWidth;

  public DetailModalView(IReadOnlyList<DetailSection> sections)
  {
    _sections = sections;
    _labelColumnWidth = ComputeLabelColumnWidth(sections);
  }

  /// <summary>
  /// Measures the total content height including section dividers, rows, and spacing.
  /// </summary>
  public static int MeasureContentHeight(IReadOnlyList<DetailSection> sections, int contentWidth)
  {
    var height = 0;

    for (var i = 0; i < sections.Count; i++)
    {
      var section = sections[i];

      // Section divider: blank line + title line (skip blank line for first section)
      if (section.Title is not null)
      {
        if (i > 0)
        {
          height++; // blank line before section divider
        }

        height++; // section title line
      }

      foreach (var row in section.Rows)
      {
        if (row.IsMultiLine)
        {
          height++; // label line
          var valueLines = WordWrap(row.Value, Math.Max(contentWidth - 4, 1));
          height += valueLines.Count;
        }
        else
        {
          height++; // single row: label + value on same line
        }
      }
    }

    return height;
  }

  protected override bool OnDrawingContent(DrawContext? context)
  {
    var width = Viewport.Width;
    if (width <= 0)
    {
      return true;
    }

    var y = 0;

    for (var i = 0; i < _sections.Count; i++)
    {
      var section = _sections[i];

      // Section divider
      if (section.Title is not null)
      {
        if (i > 0)
        {
          y++; // blank line before section divider
        }

        DrawSectionDivider(y, width, section.Title);
        y++;
      }

      foreach (var row in section.Rows)
      {
        if (row.IsMultiLine)
        {
          DrawMultiLineRow(ref y, width, row);
        }
        else
        {
          DrawSingleLineRow(y, width, row);
          y++;
        }
      }
    }

    return true;
  }

  private void DrawSectionDivider(int y, int width, string title)
  {
    SetAttribute(Theme.Semantic.Muted);
    Move(2, y);

    // Format: "── {title} ──" using Rule characters
    var titlePart = $" {title} ";
    var sideLen = Math.Max(2, (width - 2 - titlePart.Length) / 2);
    var left = new string(Theme.Symbols.Rule, sideLen);
    var right = new string(Theme.Symbols.Rule, sideLen);
    var rule = $"{left}{titlePart}{right}";
    AddStr(Truncate(rule, width - 2));
  }

  private void DrawSingleLineRow(int y, int width, DetailRow row)
  {
    // Label at X=2 with Muted
    SetAttribute(Theme.Semantic.Muted);
    Move(2, y);
    var paddedLabel = row.Label.PadRight(_labelColumnWidth);
    AddStr(Truncate(paddedLabel, Math.Max(width - 2, 0)));

    // Value after label column with Info
    var valueX = 2 + _labelColumnWidth;
    if (valueX < width)
    {
      SetAttribute(GetValueAttribute(row));
      Move(valueX, y);
      AddStr(Truncate(row.Value, width - valueX));
    }
  }

  private void DrawMultiLineRow(ref int y, int width, DetailRow row)
  {
    // Label on its own line at X=2
    SetAttribute(Theme.Semantic.Muted);
    Move(2, y);
    AddStr(Truncate(row.Label, Math.Max(width - 2, 0)));
    y++;

    // Value on subsequent lines at X=4
    var valueAttr = GetValueAttribute(row);
    SetAttribute(valueAttr);
    var valueLines = WordWrap(row.Value, Math.Max(width - 4, 1));
    foreach (var line in valueLines)
    {
      Move(4, y);
      AddStr(Truncate(line, width - 4));
      y++;
    }
  }

  private static Attribute GetValueAttribute(DetailRow row)
  {
    return row.Style switch
    {
      DetailValueStyle.Success => Theme.Semantic.Success,
      DetailValueStyle.Warning => Theme.Semantic.Warning,
      DetailValueStyle.Error => Theme.Semantic.Error,
      DetailValueStyle.Muted => Theme.Semantic.Muted,
      DetailValueStyle.Default => Theme.Semantic.Default,
      DetailValueStyle.Info => Theme.Semantic.Info,
      // Auto: multi-line text content uses Default (white); data values use Info (cyan)
      _ => row.IsMultiLine ? Theme.Semantic.Default : Theme.Semantic.Info,
    };
  }

  private static int ComputeLabelColumnWidth(IReadOnlyList<DetailSection> sections)
  {
    var maxLabelLen = 0;
    foreach (var section in sections)
    {
      foreach (var row in section.Rows)
      {
        if (!row.IsMultiLine && row.Label.Length > maxLabelLen)
        {
          maxLabelLen = row.Label.Length;
        }
      }
    }

    // Label width + 2 padding between label and value
    return maxLabelLen + 2;
  }

  private static List<string> WordWrap(string text, int width)
  {
    return ConversationBlockRenderer.WordWrap(text, width);
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
