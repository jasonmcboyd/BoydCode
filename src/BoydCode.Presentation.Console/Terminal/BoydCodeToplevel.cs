using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class BoydCodeToplevel : Runnable
{
  public ConversationView ConversationView { get; }
  public ActivityBarView ActivityBar { get; }
  public ChatInputView InputView { get; }
  public ChatStatusBar StatusBar { get; }

  public BoydCodeToplevel()
  {
    // Status bar at bottom (1 row)
    StatusBar = new ChatStatusBar
    {
      Y = Pos.AnchorEnd(1),
      Width = Dim.Fill(),
      Height = 1,
    };

    // Input above status bar (1 row)
    InputView = new ChatInputView
    {
      Y = Pos.AnchorEnd(2), // 1 for status + 1 for input
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
      Height = Dim.Fill(3), // Reserve 3 rows: activity + input + status
    };

    Add(ConversationView, ActivityBar, InputView, StatusBar);
  }

  // Global key routing - forward scroll keys to ConversationView
  protected override bool OnKeyDown(Key key)
  {
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
