using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class BoydCodeToplevel : Runnable
{
  public ConversationView ConversationView { get; }
  public ActivityBarView ActivityBar { get; }
  public ChatInputView InputView { get; }
  public InputSeparatorView Separator { get; }
  public ChatStatusBar StatusBar { get; }

  /// <summary>
  /// Callback to dismiss an active modal window. Returns true if a modal was dismissed.
  /// </summary>
  internal Func<bool>? TryDismissModal { get; set; }

  public BoydCodeToplevel()
  {
    // Status bar at bottom (1 row)
    StatusBar = new ChatStatusBar
    {
      Y = Pos.AnchorEnd(1),
      Width = Dim.Fill(),
      Height = 1,
    };

    // Separator above status bar (1 row)
    Separator = new InputSeparatorView
    {
      Y = Pos.AnchorEnd(2), // 1 for status + 1 for separator
      Width = Dim.Fill(),
      Height = 1,
    };

    // Input above separator (1 row)
    InputView = new ChatInputView
    {
      Y = Pos.AnchorEnd(3), // 1 for status + 1 for separator + 1 for input
      Width = Dim.Fill(),
      Height = 1,
      CanFocus = true,
    };

    // Activity bar above input (1 row)
    ActivityBar = new ActivityBarView
    {
      Y = Pos.Top(InputView) - 1,
      Width = Dim.Fill(),
      Height = 1,
    };

    // Conversation fills remaining space
    ConversationView = new ConversationView
    {
      Y = 0,
      Width = Dim.Fill(),
      Height = Dim.Fill(4), // Reserve 4 rows: activity + input + separator + status
    };

    Add(ConversationView, ActivityBar, InputView, Separator, StatusBar);
  }

  // Global key routing - forward scroll keys to ConversationView
  protected override bool OnKeyDown(Key key)
  {
    if (key == Key.Esc && TryDismissModal?.Invoke() == true)
    {
      return true;
    }

    if (key == Key.PageUp)
    {
      ConversationView.ScrollPageUp();
      return true;
    }

    if (key == Key.PageDown)
    {
      ConversationView.ScrollPageDown();
      return true;
    }

    if (key == Key.Home.WithCtrl)
    {
      ConversationView.ScrollToTop();
      return true;
    }

    if (key == Key.End.WithCtrl)
    {
      ConversationView.ScrollToBottom();
      return true;
    }

    return base.OnKeyDown(key);
  }
}
