using System.Text;
using Terminal.Gui.ViewBase;

#pragma warning disable IDE0060 // Remove unused parameter - context is required by the override signature

namespace BoydCode.Presentation.Console.Terminal;

internal sealed class ConversationView : View
{
  private const int MaxBlocks = 2000;

  private readonly List<ConversationBlock> _blocks = new();
  private readonly List<int> _blockHeights = new();
  private int _totalHeight;
  private int _scrollOffset;
  private bool _pinToBottom = true;
  private bool _isStreaming;
  private readonly StringBuilder _streamBuffer = new();
  private int _lastMeasuredWidth;

  public void AddBlock(ConversationBlock block)
  {
    _blocks.Add(block);
    var height = ConversationBlockRenderer.MeasureHeight(block, Viewport.Width);
    _blockHeights.Add(height);
    _totalHeight += height;

    TrimIfNeeded();

    if (_pinToBottom)
    {
      AdjustScrollToBottom();
    }

    SetNeedsDraw();
  }

  // ----- Streaming support -----

  public void BeginStream()
  {
    _isStreaming = true;
    _streamBuffer.Clear();
  }

  public void AppendStreamText(string text)
  {
    _streamBuffer.Append(text);

    var streamBlock = new AssistantTextBlock(_streamBuffer.ToString());
    var width = Viewport.Width;
    var newHeight = ConversationBlockRenderer.MeasureHeight(streamBlock, width);

    if (_isStreaming && _blocks.Count > 0 && _blocks[^1] is AssistantTextBlock)
    {
      // Replace the last block with updated content
      var oldHeight = _blockHeights[^1];
      _blocks[^1] = streamBlock;
      _blockHeights[^1] = newHeight;
      _totalHeight += newHeight - oldHeight;
    }
    else
    {
      // First chunk: add a new block
      _blocks.Add(streamBlock);
      _blockHeights.Add(newHeight);
      _totalHeight += newHeight;
    }

    if (_pinToBottom)
    {
      AdjustScrollToBottom();
    }

    SetNeedsDraw();
  }

  public void EndStream()
  {
    _isStreaming = false;
    _streamBuffer.Clear();
  }

  // ----- Scrolling -----

  public void ScrollUp(int lines = 1)
  {
    _scrollOffset = Math.Max(0, _scrollOffset - lines);
    _pinToBottom = false;
    SetNeedsDraw();
  }

  public void ScrollDown(int lines = 1)
  {
    var maxOffset = MaxScrollOffset();
    _scrollOffset = Math.Min(maxOffset, _scrollOffset + lines);

    if (_scrollOffset >= maxOffset)
    {
      _pinToBottom = true;
    }

    SetNeedsDraw();
  }

  public void ScrollToTop()
  {
    _scrollOffset = 0;
    _pinToBottom = false;
    SetNeedsDraw();
  }

  public void ScrollToBottom()
  {
    _pinToBottom = true;
    AdjustScrollToBottom();
    SetNeedsDraw();
  }

  public void ScrollPageUp()
  {
    ScrollUp(Math.Max(1, Viewport.Height - 1));
  }

  public void ScrollPageDown()
  {
    ScrollDown(Math.Max(1, Viewport.Height - 1));
  }

  // ----- Drawing -----

  protected override bool OnDrawingContent(DrawContext? context)
  {
    var viewportWidth = Viewport.Width;
    var viewportHeight = Viewport.Height;

    if (viewportWidth <= 0 || viewportHeight <= 0)
    {
      return true;
    }

    // Recalculate all block heights if width changed
    if (viewportWidth != _lastMeasuredWidth)
    {
      RecalculateAllHeights(viewportWidth);
      _lastMeasuredWidth = viewportWidth;
    }

    if (_pinToBottom)
    {
      AdjustScrollToBottom();
    }

    // Walk blocks, skip those above the visible area, draw visible ones
    var contentY = 0; // current row in content space (absolute)
    var visibleTop = _scrollOffset;
    var visibleBottom = _scrollOffset + viewportHeight;

    for (var i = 0; i < _blocks.Count; i++)
    {
      var blockHeight = _blockHeights[i];
      var blockBottom = contentY + blockHeight;

      if (blockBottom <= visibleTop)
      {
        // Entirely above the viewport, skip
        contentY = blockBottom;
        continue;
      }

      if (contentY >= visibleBottom)
      {
        // Past the viewport, stop
        break;
      }

      // This block is at least partially visible
      var drawY = contentY - _scrollOffset;
      ConversationBlockRenderer.Draw(this, _blocks[i], drawY, viewportWidth);

      contentY = blockBottom;
    }

    return true;
  }

  // ----- Private helpers -----

  private void RecalculateAllHeights(int width)
  {
    _totalHeight = 0;
    for (var i = 0; i < _blocks.Count; i++)
    {
      var h = ConversationBlockRenderer.MeasureHeight(_blocks[i], width);
      _blockHeights[i] = h;
      _totalHeight += h;
    }
  }

  private void AdjustScrollToBottom()
  {
    _scrollOffset = MaxScrollOffset();
  }

  private int MaxScrollOffset()
  {
    var viewportHeight = Viewport.Height;
    if (viewportHeight <= 0)
    {
      return 0;
    }

    return Math.Max(0, _totalHeight - viewportHeight);
  }

  private void TrimIfNeeded()
  {
    while (_blocks.Count > MaxBlocks)
    {
      _totalHeight -= _blockHeights[0];
      _blocks.RemoveAt(0);
      _blockHeights.RemoveAt(0);
    }
  }
}
