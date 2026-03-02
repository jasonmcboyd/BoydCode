using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using TguiApp = Terminal.Gui.App.Application;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable CS0618 // Application.AddTimeout/RemoveTimeout - using legacy static API during Terminal.Gui migration
#pragma warning disable IDE0060 // Remove unused parameter - context is required by the override signature

namespace BoydCode.Presentation.Console.Terminal;

internal enum ActivityState
{
  Idle,
  Thinking,
  Streaming,
  Executing,
  CancelHint,
  Modal,
}

internal sealed class ActivityBarView : View
{
  private static readonly char[] SpinnerFrames =
    ['⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏'];

  private static readonly Attribute IdleAttr = new(ColorName16.DarkGray, Color.None);
  private static readonly Attribute CyanAttr = new(ColorName16.Cyan, Color.None);
  private static readonly Attribute YellowAttr = new(ColorName16.Yellow, Color.None);

  private ActivityState _state = ActivityState.Idle;
  private int _spinnerFrame;
  private object? _timerToken;

  public void SetState(ActivityState state)
  {
    if (_state == state)
    {
      return;
    }

    _state = state;

    var needsAnimation = state is ActivityState.Thinking
      or ActivityState.Streaming
      or ActivityState.Executing;

    if (needsAnimation)
    {
      StartTimer();
    }
    else
    {
      StopTimer();
    }

    SetNeedsDraw();
  }

  protected override bool OnDrawingContent(DrawContext? context)
  {
    var width = Viewport.Width;
    if (width <= 0)
    {
      return true;
    }

    // Clear the row
    SetAttribute(IdleAttr);
    Move(0, 0);
    AddStr(new string(' ', width));

    switch (_state)
    {
      case ActivityState.Idle:
        Move(0, 0);
        SetAttribute(IdleAttr);
        AddStr(new string('\u2500', width)); // ─ dim horizontal rule
        break;

      case ActivityState.Thinking:
        DrawSpinnerWithLabel(width, YellowAttr, "Thinking...");
        break;

      case ActivityState.Streaming:
        DrawSpinnerWithLabel(width, CyanAttr, "Streaming...");
        break;

      case ActivityState.Executing:
        DrawSpinnerWithLabel(width, CyanAttr, "Executing...");
        break;

      case ActivityState.CancelHint:
        Move(1, 0);
        SetAttribute(YellowAttr);
        AddStr(Truncate("Press Esc again to cancel", width - 1));
        break;

      case ActivityState.Modal:
        Move(1, 0);
        SetAttribute(IdleAttr);
        AddStr(Truncate("Esc to dismiss", width - 1));
        break;
    }

    return true;
  }

  private void DrawSpinnerWithLabel(int width, Attribute attr, string label)
  {
    Move(1, 0);
    SetAttribute(attr);
    var spinner = SpinnerFrames[_spinnerFrame % SpinnerFrames.Length];
    AddStr($"{spinner} ");
    AddStr(Truncate(label, width - 3)); // 1 left pad + spinner char + space
  }

  private void StartTimer()
  {
    if (_timerToken is not null)
    {
      return;
    }

    _spinnerFrame = 0;
    _timerToken = TguiApp.AddTimeout(
      TimeSpan.FromMilliseconds(100),
      () =>
      {
        _spinnerFrame = (_spinnerFrame + 1) % SpinnerFrames.Length;
        SetNeedsDraw();
        return _timerToken is not null; // continue if timer is still active
      });
  }

  private void StopTimer()
  {
    if (_timerToken is not null)
    {
      TguiApp.RemoveTimeout(_timerToken);
      _timerToken = null;
    }
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
