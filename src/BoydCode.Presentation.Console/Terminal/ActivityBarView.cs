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
    SetAttribute(Theme.Semantic.Muted);
    Move(0, 0);
    AddStr(new string(' ', width));

    switch (_state)
    {
      case ActivityState.Idle:
        Move(0, 0);
        SetAttribute(Theme.Semantic.Muted);
        AddStr(new string(Theme.Symbols.Rule, width));
        break;

      case ActivityState.Thinking:
        DrawSpinnerWithLabel(width, Theme.Semantic.Warning, Theme.Text.ThinkingLabel);
        break;

      case ActivityState.Streaming:
        DrawSpinnerWithLabel(width, Theme.Semantic.Info, Theme.Text.StreamingLabel);
        break;

      case ActivityState.Executing:
        DrawSpinnerWithLabel(width, Theme.Semantic.Info, Theme.Text.ExecutingLabel);
        break;

      case ActivityState.CancelHint:
        Move(1, 0);
        SetAttribute(Theme.Semantic.Warning);
        AddStr(Truncate(Theme.Text.CancelHint, width - 1));
        break;

      case ActivityState.Modal:
        Move(1, 0);
        SetAttribute(Theme.Semantic.Muted);
        AddStr(Truncate(Theme.Text.EscToDismiss, width - 1));
        break;
    }

    return true;
  }

  private void DrawSpinnerWithLabel(int width, Attribute attr, string label)
  {
    Move(1, 0);
    SetAttribute(attr);
    var spinner = Theme.Symbols.SpinnerFrames[_spinnerFrame % Theme.Symbols.SpinnerFrames.Length];
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
      TimeSpan.FromMilliseconds(Theme.Layout.SpinnerIntervalMs),
      () =>
      {
        _spinnerFrame = (_spinnerFrame + 1) % Theme.Symbols.SpinnerFrames.Length;
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
