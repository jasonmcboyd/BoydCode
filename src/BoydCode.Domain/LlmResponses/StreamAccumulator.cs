using System.Text;
using BoydCode.Domain.ContentBlocks;

namespace BoydCode.Domain.LlmResponses;

public sealed class StreamAccumulator
{
  private readonly StringBuilder _textBuffer = new();
  private readonly List<ContentBlock> _blocks = [];
  private string _stopReason = "unknown";
  private TokenUsage _usage = new(0, 0);

  public void Process(StreamChunk chunk)
  {
    switch (chunk)
    {
      case TextChunk text:
        _textBuffer.Append(text.Text);
        break;

      case ToolCallChunk toolCall:
        FlushText();
        _blocks.Add(new ToolUseBlock(toolCall.CallId, toolCall.Name, toolCall.ArgumentsJson));
        break;

      case CompletionChunk completion:
        _stopReason = completion.StopReason;
        _usage = completion.Usage;
        break;
    }
  }

  public LlmResponse ToResponse()
  {
    FlushText();

    return new LlmResponse
    {
      Content = _blocks.AsReadOnly(),
      StopReason = _stopReason,
      Usage = _usage,
    };
  }

  private void FlushText()
  {
    if (_textBuffer.Length > 0)
    {
      _blocks.Add(new TextBlock(_textBuffer.ToString()));
      _textBuffer.Clear();
    }
  }
}
