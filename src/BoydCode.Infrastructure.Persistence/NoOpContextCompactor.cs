using BoydCode.Application.Interfaces;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;

namespace BoydCode.Infrastructure.Persistence;

/// <summary>
/// Simple context compaction strategy that trims older messages to fit within
/// a target token count. Retains messages from the end of the conversation.
/// System prompt token budget is handled by the caller.
/// </summary>
/// <remarks>
/// Phase 3 will replace this with an LLM-based summarization compactor.
/// </remarks>
public sealed class NoOpContextCompactor : IContextCompactor
{
  public Task<Conversation> CompactAsync(
      Conversation conversation,
      int targetTokenCount,
      CancellationToken ct = default)
  {
    var result = new Conversation();
    var messages = conversation.Messages;

    var keptMessages = new List<ConversationMessage>();
    var estimatedTokens = 0;

    for (var i = messages.Count - 1; i >= 0; i--)
    {
      var msgTokens = EstimateMessageTokens(messages[i]);

      if (estimatedTokens + msgTokens > targetTokenCount && keptMessages.Count > 0)
      {
        break;
      }

      keptMessages.Insert(0, messages[i]);
      estimatedTokens += msgTokens;
    }

    foreach (var msg in keptMessages)
    {
      result.AddMessage(msg);
    }

    return Task.FromResult(result);
  }

  private static int EstimateMessageTokens(ConversationMessage message)
  {
    var chars = 0;

    foreach (var block in message.Content)
    {
      chars += block switch
      {
        TextBlock t => t.Text.Length,
        ToolUseBlock tu => tu.Name.Length + tu.ArgumentsJson.Length,
        ToolResultBlock tr => tr.Content.Length,
        _ => 100,
      };
    }

    return chars / 4;
  }
}
