using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Entities;

public sealed class Conversation
{
  private readonly List<ConversationMessage> _messages = [];

  public IReadOnlyList<ConversationMessage> Messages => _messages.AsReadOnly();

  public void AddMessage(ConversationMessage message) => _messages.Add(message);

  public void AddUserMessage(string text) =>
      _messages.Add(new ConversationMessage(MessageRole.User, text));

  public void AddAssistantMessage(string text) =>
      _messages.Add(new ConversationMessage(MessageRole.Assistant, text));

  public void AddAssistantMessage(IReadOnlyList<ContentBlock> content) =>
      _messages.Add(new ConversationMessage(MessageRole.Assistant, content));

  public void AddToolResult(string toolUseId, string content, bool isError = false) =>
      _messages.Add(new ConversationMessage(MessageRole.User, [new ToolResultBlock(toolUseId, content, isError)]));

  public int EstimateTokenCount()
  {
    // Rough estimation: ~4 chars per token
    var charCount = 0;
    foreach (var msg in _messages)
    {
      foreach (var block in msg.Content)
      {
        charCount += block switch
        {
          TextBlock t => t.Text.Length,
          ToolUseBlock tu => tu.Name.Length + tu.ArgumentsJson.Length,
          ToolResultBlock tr => tr.Content.Length,
          ImageBlock => 1000, // rough estimate for image tokens
          _ => 0
        };
      }
    }
    return charCount / 4;
  }

  public bool RemoveLastMessage()
  {
    if (_messages.Count == 0) return false;
    _messages.RemoveAt(_messages.Count - 1);
    return true;
  }

  public int Clear()
  {
    var count = _messages.Count;
    _messages.Clear();
    return count;
  }

  public void ReplaceMessages(IReadOnlyList<ConversationMessage> compactedMessages)
  {
    _messages.Clear();
    _messages.AddRange(compactedMessages);
  }
}
