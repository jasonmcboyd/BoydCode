using BoydCode.Application.Interfaces;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;

namespace BoydCode.Infrastructure.Persistence;

/// <summary>
/// Context compaction strategy that trims older messages to fit within a target
/// token count. Groups tool-call assistant messages with their corresponding
/// tool-result messages so pairs are never orphaned.
/// </summary>
public sealed class EvictionContextCompactor : IContextCompactor
{
  public const string CompactionNotice =
    "[Earlier conversation context was compacted to manage context window size. " +
    "Some previous messages, tool calls, and results are no longer visible.]";

  public Task<Conversation> CompactAsync(
    Conversation conversation,
    int targetTokenCount,
    CancellationToken ct = default)
  {
    var messages = conversation.Messages;

    // Nothing to compact
    if (messages.Count == 0)
      return Task.FromResult(new Conversation());

    // Build message groups — a group is either:
    // 1. A standalone message (user text or assistant text-only)
    // 2. An assistant message containing ToolUseBlocks + its following ToolResultBlock messages
    var groups = BuildMessageGroups(messages);

    // Walk backward from newest, accumulating token estimates
    var keptGroups = new List<MessageGroup>();
    var estimatedTokens = 0;

    for (var i = groups.Count - 1; i >= 0; i--)
    {
      var groupTokens = groups[i].EstimatedTokens;

      if (estimatedTokens + groupTokens > targetTokenCount && keptGroups.Count > 0)
        break;

      keptGroups.Add(groups[i]);
      estimatedTokens += groupTokens;
    }

    keptGroups.Reverse();

    // Build result conversation
    var result = new Conversation();

    // Check if any messages were actually evicted
    var totalKeptMessages = keptGroups.Sum(g => g.Messages.Count);
    if (totalKeptMessages < messages.Count)
    {
      // Prepend compaction notice
      result.AddUserMessage(CompactionNotice);
    }

    foreach (var group in keptGroups)
    {
      foreach (var msg in group.Messages)
      {
        result.AddMessage(msg);
      }
    }

    return Task.FromResult(result);
  }

  private static List<MessageGroup> BuildMessageGroups(IReadOnlyList<ConversationMessage> messages)
  {
    var groups = new List<MessageGroup>();
    var i = 0;

    while (i < messages.Count)
    {
      var msg = messages[i];

      // Check if this is an assistant message with tool calls
      if (msg.Role == MessageRole.Assistant && HasToolUseBlocks(msg))
      {
        var groupMessages = new List<ConversationMessage> { msg };
        var toolCallCount = msg.Content.Count(b => b is ToolUseBlock);
        i++;

        // Collect the following tool result messages
        var resultsCollected = 0;
        while (i < messages.Count && resultsCollected < toolCallCount)
        {
          var next = messages[i];
          if (next.Role == MessageRole.User && HasToolResultBlocks(next))
          {
            groupMessages.Add(next);
            resultsCollected++;
            i++;
          }
          else
          {
            break;
          }
        }

        groups.Add(new MessageGroup(groupMessages));
      }
      else
      {
        groups.Add(new MessageGroup([msg]));
        i++;
      }
    }

    return groups;
  }

  private static bool HasToolUseBlocks(ConversationMessage message) =>
    message.Content.Any(b => b is ToolUseBlock);

  private static bool HasToolResultBlocks(ConversationMessage message) =>
    message.Content.Any(b => b is ToolResultBlock);

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
        ImageBlock => 1000,
        _ => 0,
      };
    }

    return chars / 4;
  }

  private sealed class MessageGroup
  {
    public List<ConversationMessage> Messages { get; }
    public int EstimatedTokens { get; }

    public MessageGroup(List<ConversationMessage> messages)
    {
      Messages = messages;
      EstimatedTokens = messages.Sum(EstimateMessageTokens);
    }
  }
}
