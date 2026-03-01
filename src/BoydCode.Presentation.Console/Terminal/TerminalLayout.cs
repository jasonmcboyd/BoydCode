namespace BoydCode.Presentation.Console.Terminal;

internal sealed class TerminalLayout : IDisposable
{
  private const int MinTerminalHeight = 10;
  private const string Esc = "\x1b";

  // ANSI sequences
  private const string ClearScreen = $"{Esc}[2J";
  private const string CursorHome = $"{Esc}[H";
  private const string SaveCursor = $"{Esc}[s";
  private const string RestoreCursor = $"{Esc}[u";
  private const string ResetScrollRegion = $"{Esc}[r";
  private const string ClearLine = $"{Esc}[2K";
  private const string DimOn = $"{Esc}[2m";
  private const string DimOff = $"{Esc}[22m";

  private readonly object _consoleLock = new();
  private bool _isActive;
  private bool _isSuspended;
  private bool _useLayout;
  private int _lastHeight;
  private int _lastWidth;
  private string _statusText = "";
  private bool _agentBusy;
  private int _queueCount;
  private int _inputCursorCol = 3; // After "> "
  private bool _disposed;

  public static TerminalLayout? Current { get; private set; }

  public object ConsoleLock => _consoleLock;

  public bool IsActive => _isActive && !_isSuspended;

  public void Activate()
  {
    if (!CanUseLayout())
    {
      _useLayout = false;
      return;
    }

    _useLayout = true;

    lock (_consoleLock)
    {
      if (_isActive) return;

      Current = this;
      _isActive = true;
      _isSuspended = false;

      CaptureTerminalSize();

      // Enable VT processing on Windows (best effort; Windows Terminal has it on by default)
      EnableVirtualTerminalProcessing();

      EstablishLayout();
    }
  }

  public void Deactivate()
  {
    lock (_consoleLock)
    {
      if (!_isActive) return;

      _isActive = false;
      _isSuspended = false;

      if (_useLayout)
      {
        // Reset scroll region and clear screen to restore normal terminal
        System.Console.Write($"{ResetScrollRegion}{ClearScreen}{CursorHome}");
      }

      if (Current == this)
      {
        Current = null;
      }
    }
  }

  public void Suspend()
  {
    lock (_consoleLock)
    {
      if (!_isActive || _isSuspended || !_useLayout) return;

      _isSuspended = true;

      // Reset scroll region so Spectre.Console gets full terminal control
      System.Console.Write(ResetScrollRegion);

      // Move cursor to the bottom of the screen
      System.Console.Write($"{Esc}[{_lastHeight};1H");
      System.Console.Out.Flush();
    }
  }

  public void Resume()
  {
    lock (_consoleLock)
    {
      if (!_isActive || !_isSuspended || !_useLayout) return;

      _isSuspended = false;

      CaptureTerminalSize();
      EstablishLayout();
    }
  }

  public void WriteToOutput(string text)
  {
    if (!_useLayout)
    {
      System.Console.Write(text);
      return;
    }

    lock (_consoleLock)
    {
      if (!_isActive || _isSuspended)
      {
        System.Console.Write(text);
        return;
      }

      CheckForResize();

      var h = _lastHeight;
      var outputBottom = h - 3;

      // Move cursor to the bottom of the output scroll region
      System.Console.Write($"{Esc}[{outputBottom};1H");

      // Write the text (scroll region handles scrolling naturally)
      System.Console.Write(text);

      // Move cursor back to the input line at the user's cursor position
      PositionCursorAtInput();
    }
  }

  public void WriteLineToOutput(string text)
  {
    if (!_useLayout)
    {
      System.Console.WriteLine(text);
      return;
    }

    lock (_consoleLock)
    {
      if (!_isActive || _isSuspended)
      {
        System.Console.WriteLine(text);
        return;
      }

      CheckForResize();

      var h = _lastHeight;
      var outputBottom = h - 3;

      // Move cursor to the bottom of the output scroll region
      System.Console.Write($"{Esc}[{outputBottom};1H");

      // Write the text with newline (scroll region handles scrolling)
      System.Console.WriteLine(text);

      // Move cursor back to the input line
      PositionCursorAtInput();
    }
  }

  public void UpdateInputLine(string text, int cursorPosition)
  {
    if (!_useLayout) return;

    lock (_consoleLock)
    {
      if (!_isActive || _isSuspended) return;

      var h = _lastHeight;
      var w = _lastWidth;
      var inputRow = h - 1;

      // Move to input row and clear it
      System.Console.Write($"{Esc}[{inputRow};1H{ClearLine}");

      // Draw prompt and text, truncating if necessary
      var maxTextWidth = w - 4; // Reserve space for "> " prefix and some margin
      var displayText = text.Length > maxTextWidth
        ? text[..maxTextWidth]
        : text;
      System.Console.Write($"> {displayText}");

      // Show queue count if agent is busy and items are queued
      if (_agentBusy && _queueCount > 0)
      {
        var queueLabel = _queueCount == 1
          ? "[1 message queued]"
          : $"[{_queueCount} messages queued]";
        var labelCol = w - queueLabel.Length;
        if (labelCol > displayText.Length + 4)
        {
          System.Console.Write($"{Esc}[{inputRow};{labelCol}H{DimOn}{queueLabel}{DimOff}");
        }
      }

      // Position cursor where the user is editing (1-based column: 2 for "> " + cursorPosition)
      _inputCursorCol = cursorPosition + 3; // +2 for "> ", +1 for 1-based
      System.Console.Write($"{Esc}[{inputRow};{_inputCursorCol}H");
    }
  }

  public void UpdateStatusLine(string status)
  {
    _statusText = status;

    if (!_useLayout) return;

    lock (_consoleLock)
    {
      if (!_isActive || _isSuspended) return;

      DrawStatusLine();
      PositionCursorAtInput();
    }
  }

  public void UpdateQueueCount(int count)
  {
    _queueCount = count;

    // The queue indicator is redrawn as part of UpdateInputLine,
    // so we do not force a redraw here to avoid flicker.
  }

  public void SetAgentBusy(bool busy)
  {
    _agentBusy = busy;
  }

  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;

    Deactivate();
  }

  // ─────────────────────────────────────────────
  //  Internal helpers
  // ─────────────────────────────────────────────

  private static bool CanUseLayout()
  {
    try
    {
      if (System.Console.IsOutputRedirected || System.Console.IsInputRedirected)
      {
        return false;
      }

      return System.Console.WindowHeight >= MinTerminalHeight;
    }
    catch
    {
      // Console properties can throw on certain platforms/configurations
      return false;
    }
  }

  private static void EnableVirtualTerminalProcessing()
  {
    // On Windows, we need to ensure VT processing is enabled.
    // Writing a no-op VT sequence and checking for errors is the simplest approach.
    // Windows Terminal and modern ConEmu enable VT by default.
    // For legacy conhost, the .NET runtime enables it when Console.Write is used
    // with VT sequences if the console mode supports it.
    try
    {
      // Attempt a harmless VT query; if VT is not supported this is silently ignored
      System.Console.Write($"{Esc}[0m");
    }
    catch
    {
      // If this fails, VT is not supported and layout methods will degrade gracefully
    }
  }

  private void CaptureTerminalSize()
  {
    try
    {
      _lastHeight = System.Console.WindowHeight;
      _lastWidth = System.Console.WindowWidth;
    }
    catch
    {
      // Fallback to reasonable defaults
      _lastHeight = 24;
      _lastWidth = 120;
    }
  }

  private void CheckForResize()
  {
    try
    {
      var currentHeight = System.Console.WindowHeight;
      var currentWidth = System.Console.WindowWidth;

      if (currentHeight != _lastHeight || currentWidth != _lastWidth)
      {
        _lastHeight = currentHeight;
        _lastWidth = currentWidth;
        RefreshLayout();
      }
    }
    catch
    {
      // If we cannot read terminal size, keep the last known values
    }
  }

  private void RefreshLayout()
  {
    // Re-establish the scroll region and redraw chrome after a resize
    if (!_isActive || _isSuspended) return;

    EstablishLayout();
  }

  private void EstablishLayout()
  {
    var h = _lastHeight;
    var w = _lastWidth;

    if (h < MinTerminalHeight)
    {
      // Terminal too small; degrade gracefully
      return;
    }

    var scrollBottom = h - 3; // Rows 1 to H-3 are the output area
    var separatorRow = h - 2;
    var inputRow = h - 1;
    var statusRow = h;

    // Clear screen and set cursor to home
    System.Console.Write($"{ClearScreen}{CursorHome}");

    // Set scroll region to rows 1 through H-3
    System.Console.Write($"{Esc}[1;{scrollBottom}r");

    // Draw separator row (outside the scroll region)
    System.Console.Write($"{Esc}[{separatorRow};1H");
    var separator = BuildSeparator(w);
    System.Console.Write($"{DimOn}{separator}{DimOff}");

    // Draw input row with prompt
    System.Console.Write($"{Esc}[{inputRow};1H{ClearLine}> ");

    // Draw status line
    System.Console.Write($"{Esc}[{statusRow};1H{ClearLine}");
    if (_statusText.Length > 0)
    {
      var truncatedStatus = _statusText.Length > w
        ? _statusText[..w]
        : _statusText;
      System.Console.Write($"{DimOn}{truncatedStatus}{DimOff}");
    }

    // Move cursor to the top of the output area so initial output starts there
    System.Console.Write($"{Esc}[1;1H");

    System.Console.Out.Flush();
  }

  private void DrawStatusLine()
  {
    var h = _lastHeight;
    var w = _lastWidth;
    var statusRow = h;

    System.Console.Write($"{Esc}[{statusRow};1H{ClearLine}");

    if (_statusText.Length > 0)
    {
      var truncatedStatus = _statusText.Length > w
        ? _statusText[..w]
        : _statusText;
      System.Console.Write($"{DimOn}{truncatedStatus}{DimOff}");
    }
  }

  private void PositionCursorAtInput()
  {
    var inputRow = _lastHeight - 1;
    System.Console.Write($"{Esc}[{inputRow};{_inputCursorCol}H");
  }

  private static string BuildSeparator(int width)
  {
    // Use box-drawing horizontal line character repeated to terminal width.
    // String.Create is efficient for this, but for clarity and compatibility
    // we use a simple new string approach.
    return new string('\u2500', width);
  }
}
