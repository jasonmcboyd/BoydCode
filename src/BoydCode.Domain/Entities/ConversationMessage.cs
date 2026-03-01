using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Entities;

public sealed class ConversationMessage
{
  public MessageRole Role { get; }
  public IReadOnlyList<ContentBlock> Content { get; }
  public DateTimeOffset Timestamp { get; }

  public ConversationMessage(MessageRole role, IReadOnlyList<ContentBlock> content)
  {
    Role = role;
    Content = content;
    Timestamp = DateTimeOffset.UtcNow;
  }

  public ConversationMessage(MessageRole role, IReadOnlyList<ContentBlock> content, DateTimeOffset timestamp)
  {
    Role = role;
    Content = content;
    Timestamp = timestamp;
  }

  public ConversationMessage(MessageRole role, string text)
      : this(role, [new TextBlock(text)])
  {
  }
}
