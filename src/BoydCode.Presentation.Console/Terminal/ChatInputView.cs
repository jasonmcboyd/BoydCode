using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using TguiApp = Terminal.Gui.App.Application;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable CS0618 // Application.AddTimeout/RemoveTimeout - using legacy static API during Terminal.Gui migration
#pragma warning disable IDE0060 // Remove unused parameter - context is required by the override signature

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class ChatInputView : View
{
  private const int MaxHistoryEntries = 100;
  private const int CancelWindowMs = 1000;

  private static readonly Attribute PromptAttr = new(ColorName16.Blue, Color.None, TextStyle.Bold);
  private static readonly Attribute TextAttr = new(ColorName16.White, Color.None);
  private static readonly Attribute CursorAttr = new(ColorName16.White, Color.None, TextStyle.Underline);
  private static readonly Attribute CursorDimAttr = new(ColorName16.DarkGray, Color.None, TextStyle.Underline);
  private static readonly Attribute DisabledAttr = new(ColorName16.DarkGray, Color.None);
  private static readonly Attribute ClearAttr = new(ColorName16.White, Color.None);
  private static readonly Attribute ScrollIndicatorAttr = new(ColorName16.DarkGray, Color.None);

  private readonly StringBuilder _buffer = new();
  private int _cursorPos;
  private readonly List<string> _history = [];
  private int _historyIndex = -1;
  private string _savedInput = string.Empty;

  private TaskCompletionSource<string>? _inputTcs;
  private bool _enabled = true;

  // Cursor blink state — periodic redraw prevents Windows Terminal from
  // throttling rendering for idle tabs, which causes multi-second lag
  // on the first keypress after idle.
  private bool _cursorVisible = true;
  private object? _blinkTimerToken;

  // Cancellation state
  private Action? _onCancelRequested;
  private DateTime _lastEscPress = DateTime.MinValue;
  private bool _cancelHintShown;
  private object? _cancelHintTimerToken;

  public string Prompt { get; set; } = "> ";

  public new bool Enabled
  {
    get => _enabled;
    set
    {
      _enabled = value;
      if (_enabled)
      {
        StartBlinkTimer();
      }
      else
      {
        StopBlinkTimer();
      }
      SetNeedsDraw();
    }
  }

  /// <summary>
  /// Raised when the user presses Esc once and a cancel hint should be displayed.
  /// </summary>
  public event Action? CancelHintRequested;

  /// <summary>
  /// Raised when the cancel hint window expires without a second Esc press.
  /// </summary>
  public event Action? CancelHintCleared;

  public Task<string> GetUserInputAsync(CancellationToken ct = default)
  {
    _inputTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

    if (ct != default)
    {
      ct.Register(() => _inputTcs.TrySetCanceled());
    }

    Enabled = true;
    SetNeedsDraw();
    return _inputTcs.Task;
  }

  public void SetCancelCallback(Action? callback)
  {
    _onCancelRequested = callback;
  }

  protected override bool OnKeyDown(Key key)
  {
    if (!_enabled)
    {
      return base.OnKeyDown(key);
    }

    // Shift+Enter: insert newline (multi-line input)
    if (key == Key.Enter.WithShift)
    {
      InsertChar('\n');
      return true;
    }

    // Enter: submit
    if (key == Key.Enter)
    {
      HandleSubmit();
      return true;
    }

    // Ctrl+Backspace: delete word before cursor
    if (key == Key.Backspace.WithCtrl)
    {
      HandleDeleteWordLeft();
      return true;
    }

    // Backspace: delete char before cursor
    if (key == Key.Backspace)
    {
      HandleBackspace();
      return true;
    }

    // Ctrl+Delete: delete word at cursor
    if (key == Key.Delete.WithCtrl)
    {
      HandleDeleteWordRight();
      return true;
    }

    // Delete: delete char at cursor
    if (key == Key.Delete)
    {
      HandleDelete();
      return true;
    }

    // Ctrl+Left Arrow: move cursor to previous word boundary
    if (key == Key.CursorLeft.WithCtrl)
    {
      MoveCursorWordLeft();
      return true;
    }

    // Ctrl+Right Arrow: move cursor to next word boundary
    if (key == Key.CursorRight.WithCtrl)
    {
      MoveCursorWordRight();
      return true;
    }

    // Left Arrow: move cursor left
    if (key == Key.CursorLeft)
    {
      if (_cursorPos > 0)
      {
        _cursorPos--;
        SetNeedsDraw();
      }
      return true;
    }

    // Right Arrow: move cursor right
    if (key == Key.CursorRight)
    {
      if (_cursorPos < _buffer.Length)
      {
        _cursorPos++;
        SetNeedsDraw();
      }
      return true;
    }

    // Home or Ctrl+A: move cursor to start
    if (key == Key.Home || key == Key.A.WithCtrl)
    {
      _cursorPos = 0;
      SetNeedsDraw();
      return true;
    }

    // End or Ctrl+E: move cursor to end
    if (key == Key.End || key == Key.E.WithCtrl)
    {
      _cursorPos = _buffer.Length;
      SetNeedsDraw();
      return true;
    }

    // Up Arrow: navigate history (previous)
    if (key == Key.CursorUp)
    {
      HandleHistoryUp();
      return true;
    }

    // Down Arrow: navigate history (next)
    if (key == Key.CursorDown)
    {
      HandleHistoryDown();
      return true;
    }

    // Esc: cancel flow
    if (key == Key.Esc)
    {
      HandleEsc();
      return true;
    }

    // Printable character: insert at cursor position
    var rune = key.AsRune;
    if (rune.Value != 0 && !key.IsCtrl && !key.IsAlt && !char.IsControl((char)rune.Value))
    {
      InsertChar((char)rune.Value);
      return true;
    }

    return base.OnKeyDown(key);
  }

  protected override bool OnDrawingContent(DrawContext? context)
  {
    var width = Viewport.Width;
    if (width <= 0)
    {
      return true;
    }

    // Clear the row
    SetAttribute(ClearAttr);
    Move(0, 0);
    AddStr(new string(' ', width));

    var text = _buffer.ToString();
    var promptLen = Prompt.Length;

    if (!_enabled)
    {
      // Disabled: draw entire line (prompt + text) in dim
      SetAttribute(DisabledAttr);
      Move(0, 0);
      AddStr(Truncate(Prompt + text, width));
      return true;
    }

    // Draw prompt prefix (bold blue)
    Move(0, 0);
    SetAttribute(PromptAttr);
    AddStr(Truncate(Prompt, width));

    // Draw buffer text with cursor
    var availableWidth = width - promptLen;
    if (availableWidth <= 0)
    {
      return true;
    }

    // Calculate visible portion if text is wider than available space
    var visibleStart = 0;
    if (_cursorPos >= availableWidth)
    {
      // Scroll the text so the cursor is visible near the right edge
      visibleStart = _cursorPos - availableWidth + 1;
    }

    var hasOverflowLeft = visibleStart > 0;
    var hasOverflowRight = text.Length - visibleStart > availableWidth;

    var visibleText = text.Length > visibleStart
      ? text[visibleStart..]
      : string.Empty;
    visibleText = Truncate(visibleText, availableWidth);

    // Draw text before cursor
    var cursorInVisible = _cursorPos - visibleStart;
    var beforeCursor = cursorInVisible > 0 && cursorInVisible <= visibleText.Length
      ? visibleText[..cursorInVisible]
      : visibleText;
    var afterCursor = cursorInVisible >= 0 && cursorInVisible < visibleText.Length
      ? visibleText[(cursorInVisible + 1)..]
      : string.Empty;
    var cursorChar = cursorInVisible >= 0 && cursorInVisible < visibleText.Length
      ? visibleText[cursorInVisible].ToString()
      : "_";

    // Draw before cursor
    Move(promptLen, 0);
    SetAttribute(TextAttr);
    AddStr(beforeCursor);

    // Draw cursor character with underline style (blink alternates bright/dim)
    if (cursorInVisible >= 0 && cursorInVisible <= visibleText.Length)
    {
      Move(promptLen + cursorInVisible, 0);
      SetAttribute(_cursorVisible ? CursorAttr : CursorDimAttr);
      AddStr(cursorChar);

      // Draw after cursor
      if (afterCursor.Length > 0)
      {
        SetAttribute(TextAttr);
        AddStr(afterCursor);
      }
    }

    // Horizontal scroll indicators
    if (hasOverflowLeft)
    {
      Move(promptLen, 0);
      SetAttribute(ScrollIndicatorAttr);
      AddStr("\u2190"); // ←
    }

    if (hasOverflowRight)
    {
      Move(width - 1, 0);
      SetAttribute(ScrollIndicatorAttr);
      AddStr("\u2192"); // →
    }

    return true;
  }

  // ----- Input handling -----

  private void InsertChar(char ch)
  {
    _buffer.Insert(_cursorPos, ch);
    _cursorPos++;
    ResetHistoryNavigation();
    SetNeedsDraw();
  }

  private void HandleSubmit()
  {
    var text = _buffer.ToString();

    if (text.Length > 0)
    {
      AddToHistory(text);
    }

    _buffer.Clear();
    _cursorPos = 0;
    ResetHistoryNavigation();
    ResetCancelState();
    SetNeedsDraw();

    // Complete the awaiting task (only for non-empty input)
    if (text.Length > 0)
    {
      _inputTcs?.TrySetResult(text);
    }
  }

  private void HandleBackspace()
  {
    if (_cursorPos <= 0)
    {
      return;
    }

    _buffer.Remove(_cursorPos - 1, 1);
    _cursorPos--;
    ResetHistoryNavigation();
    SetNeedsDraw();
  }

  private void HandleDelete()
  {
    if (_cursorPos >= _buffer.Length)
    {
      return;
    }

    _buffer.Remove(_cursorPos, 1);
    ResetHistoryNavigation();
    SetNeedsDraw();
  }

  // ----- Word navigation -----

  private void MoveCursorWordLeft()
  {
    if (_cursorPos <= 0)
    {
      return;
    }

    var pos = _cursorPos;

    // Skip whitespace to the left
    while (pos > 0 && char.IsWhiteSpace(_buffer[pos - 1]))
    {
      pos--;
    }

    // Skip word characters to the left
    while (pos > 0 && !char.IsWhiteSpace(_buffer[pos - 1]))
    {
      pos--;
    }

    _cursorPos = pos;
    SetNeedsDraw();
  }

  private void MoveCursorWordRight()
  {
    if (_cursorPos >= _buffer.Length)
    {
      return;
    }

    var pos = _cursorPos;

    // Skip word characters to the right
    while (pos < _buffer.Length && !char.IsWhiteSpace(_buffer[pos]))
    {
      pos++;
    }

    // Skip whitespace to the right
    while (pos < _buffer.Length && char.IsWhiteSpace(_buffer[pos]))
    {
      pos++;
    }

    _cursorPos = pos;
    SetNeedsDraw();
  }

  private void HandleDeleteWordLeft()
  {
    if (_cursorPos <= 0)
    {
      return;
    }

    var start = _cursorPos;

    // Skip whitespace to the left
    while (start > 0 && char.IsWhiteSpace(_buffer[start - 1]))
    {
      start--;
    }

    // Skip word characters to the left
    while (start > 0 && !char.IsWhiteSpace(_buffer[start - 1]))
    {
      start--;
    }

    _buffer.Remove(start, _cursorPos - start);
    _cursorPos = start;
    ResetHistoryNavigation();
    SetNeedsDraw();
  }

  private void HandleDeleteWordRight()
  {
    if (_cursorPos >= _buffer.Length)
    {
      return;
    }

    var end = _cursorPos;

    // Skip word characters to the right
    while (end < _buffer.Length && !char.IsWhiteSpace(_buffer[end]))
    {
      end++;
    }

    // Skip whitespace to the right
    while (end < _buffer.Length && char.IsWhiteSpace(_buffer[end]))
    {
      end++;
    }

    _buffer.Remove(_cursorPos, end - _cursorPos);
    ResetHistoryNavigation();
    SetNeedsDraw();
  }

  // ----- History -----

  private void HandleHistoryUp()
  {
    if (_history.Count == 0)
    {
      return;
    }

    if (_historyIndex == -1)
    {
      _savedInput = _buffer.ToString();
      _historyIndex = _history.Count - 1;
    }
    else if (_historyIndex > 0)
    {
      _historyIndex--;
    }
    else
    {
      return;
    }

    LoadHistoryEntry(_history[_historyIndex]);
  }

  private void HandleHistoryDown()
  {
    if (_historyIndex == -1)
    {
      return;
    }

    if (_historyIndex < _history.Count - 1)
    {
      _historyIndex++;
      LoadHistoryEntry(_history[_historyIndex]);
    }
    else
    {
      _historyIndex = -1;
      LoadHistoryEntry(_savedInput);
      _savedInput = string.Empty;
    }
  }

  private void LoadHistoryEntry(string entry)
  {
    _buffer.Clear();
    _buffer.Append(entry);
    _cursorPos = _buffer.Length;
    SetNeedsDraw();
  }

  private void AddToHistory(string text)
  {
    var trimmed = text.Trim();
    if (trimmed.Length == 0)
    {
      return;
    }

    // Avoid duplicate consecutive entries
    if (_history.Count > 0 && _history[^1] == trimmed)
    {
      return;
    }

    _history.Add(trimmed);

    if (_history.Count > MaxHistoryEntries)
    {
      _history.RemoveAt(0);
    }
  }

  private void ResetHistoryNavigation()
  {
    _historyIndex = -1;
    _savedInput = string.Empty;
  }

  // ----- Cancellation -----

  private void HandleEsc()
  {
    var now = DateTime.UtcNow;

    if (_cancelHintShown && (now - _lastEscPress).TotalMilliseconds <= CancelWindowMs)
    {
      // Second press within window: fire cancel
      ResetCancelState();
      _onCancelRequested?.Invoke();
      return;
    }

    // First press or expired: start fresh
    _lastEscPress = now;
    _cancelHintShown = true;
    CancelHintRequested?.Invoke();

    // Start auto-clear timer: revert cancel hint after 1 second if no second press
    StopCancelHintTimer();
    _cancelHintTimerToken = TguiApp.AddTimeout(
      TimeSpan.FromMilliseconds(CancelWindowMs),
      () =>
      {
        ResetCancelState();
        return false; // one-shot timer
      });
  }

  private void ResetCancelState()
  {
    StopCancelHintTimer();
    _lastEscPress = DateTime.MinValue;
    _cancelHintShown = false;
    CancelHintCleared?.Invoke();
  }

  private void StopCancelHintTimer()
  {
    if (_cancelHintTimerToken is not null)
    {
      TguiApp.RemoveTimeout(_cancelHintTimerToken);
      _cancelHintTimerToken = null;
    }
  }

  // ----- Cursor blink -----

  private void StartBlinkTimer()
  {
    if (_blinkTimerToken is not null)
    {
      return;
    }

    _cursorVisible = true;
    _blinkTimerToken = TguiApp.AddTimeout(
      TimeSpan.FromMilliseconds(500),
      () =>
      {
        _cursorVisible = !_cursorVisible;
        SetNeedsDraw();
        return _blinkTimerToken is not null;
      });
  }

  private void StopBlinkTimer()
  {
    if (_blinkTimerToken is not null)
    {
      TguiApp.RemoveTimeout(_blinkTimerToken);
      _blinkTimerToken = null;
    }
    _cursorVisible = true;
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
