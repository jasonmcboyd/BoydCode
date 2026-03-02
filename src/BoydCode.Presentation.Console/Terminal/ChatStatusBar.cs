using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable IDE0060 // Remove unused parameter - context is required by the override signature

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class ChatStatusBar : View
{
  private static readonly Color BarBg = new(30, 30, 30);
  private static readonly Attribute StatusAttr = new(ColorName16.White, BarBg);
  private static readonly Attribute HintAttr = new(ColorName16.DarkGray, BarBg);
  private static readonly Attribute BarAttr = new(BarBg, BarBg);

  private const string HintsWide = "Esc:Cancel  PgUp/PgDn:Scroll  /help:Commands  /quit:Exit";
  private const string HintsMedium = "Esc:Cancel  PgUp/Dn:Scroll  /quit:Exit";
  private const string HintsNarrow = "/help  /quit";

  private string _statusText = string.Empty;

  public string StatusText
  {
    get => _statusText;
    set
    {
      _statusText = value;
      SetNeedsDraw();
    }
  }

  protected override bool OnDrawingContent(DrawContext? context)
  {
    var width = Viewport.Width;
    if (width <= 0)
    {
      return true;
    }

    // Fill entire row with dark background
    SetAttribute(BarAttr);
    Move(0, 0);
    AddStr(new string(' ', width));

    // Draw status text on the left
    Move(1, 0);
    SetAttribute(StatusAttr);
    var maxStatusWidth = Math.Max(width / 2, 1);
    AddStr(Truncate(_statusText, maxStatusWidth));

    // Choose key hints based on available width
    var hints = width >= 120 ? HintsWide
      : width >= 80 ? HintsMedium
      : HintsNarrow;

    // Draw hints on the right
    if (hints.Length < width - 1)
    {
      var hintsX = width - hints.Length - 1;
      Move(hintsX, 0);
      SetAttribute(HintAttr);
      AddStr(hints);
    }

    return true;
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
