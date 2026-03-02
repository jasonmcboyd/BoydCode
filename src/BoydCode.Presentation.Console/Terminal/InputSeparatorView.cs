using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable IDE0060 // Remove unused parameter - context is required by the override signature

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class InputSeparatorView : View
{
  private static readonly Attribute RuleAttr = new(ColorName16.DarkGray, Color.None);

  protected override bool OnDrawingContent(DrawContext? context)
  {
    var width = Viewport.Width;
    if (width <= 0)
    {
      return true;
    }

    SetAttribute(RuleAttr);
    Move(0, 0);
    AddStr(new string('\u2500', width));
    return true;
  }
}
