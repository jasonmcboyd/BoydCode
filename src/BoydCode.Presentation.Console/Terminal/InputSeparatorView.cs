using Terminal.Gui.ViewBase;

#pragma warning disable IDE0060 // Remove unused parameter - context is required by the override signature

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class InputSeparatorView : View
{
  protected override bool OnDrawingContent(DrawContext? context)
  {
    var width = Viewport.Width;
    if (width <= 0)
    {
      return true;
    }

    SetAttribute(Theme.Semantic.Muted);
    Move(0, 0);
    AddStr(new string(Theme.Symbols.Rule, width));
    return true;
  }
}
