using Terminal.Gui.ViewBase;

#pragma warning disable IDE0060 // Remove unused parameter - context is required by the override signature

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class ChatStatusBar : View
{
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
    SetAttribute(Theme.StatusBar.Fill);
    Move(0, 0);
    AddStr(new string(' ', width));

    // Draw status text on the left
    Move(1, 0);
    SetAttribute(Theme.StatusBar.Status);
    var maxStatusWidth = Math.Max(width / 2, 1);
    AddStr(Truncate(_statusText, maxStatusWidth));

    // Choose key hints based on available width
    var hints = width >= Theme.Layout.FullWidth ? Theme.Text.HintsWide
      : width >= Theme.Layout.StandardWidth ? Theme.Text.HintsMedium
      : Theme.Text.HintsNarrow;

    // Draw hints on the right
    if (hints.Length < width - 1)
    {
      var hintsX = width - hints.Length - 1;
      Move(hintsX, 0);
      SetAttribute(Theme.StatusBar.Hint);
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
