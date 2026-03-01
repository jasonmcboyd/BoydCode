using System.Text.Json;
using Anthropic.Models.Messages;
using BoydCode.Domain.LlmResponses;
using DomainContentBlock = BoydCode.Domain.ContentBlocks.ContentBlock;

namespace BoydCode.Infrastructure.LLM.Converters;

/// <summary>
/// Converts Anthropic native <see cref="Message"/> responses into domain <see cref="LlmResponse"/> instances.
/// </summary>
internal static class AnthropicResponseConverter
{
  public static LlmResponse ToDomain(Message message)
  {
    ArgumentNullException.ThrowIfNull(message);

    var contentBlocks = ExtractContentBlocks(message);
    var stopReason = message.StopReason is { } sr ? MapStopReason(sr) : "unknown";
    var usage = MapUsage(message.Usage);

    return new LlmResponse
    {
      Content = contentBlocks,
      StopReason = stopReason,
      Usage = usage,
    };
  }

  private static List<DomainContentBlock> ExtractContentBlocks(Message message)
  {
    var blocks = new List<DomainContentBlock>();

    foreach (var contentBlock in message.Content)
    {
      var value = contentBlock.Value;

      if (value is Anthropic.Models.Messages.TextBlock textBlock)
      {
        blocks.Add(new Domain.ContentBlocks.TextBlock(textBlock.Text));
      }
      else if (value is Anthropic.Models.Messages.ToolUseBlock toolUseBlock)
      {
        var argumentsJson = toolUseBlock.Input is not null
            ? JsonSerializer.Serialize(toolUseBlock.Input)
            : "{}";

        blocks.Add(new Domain.ContentBlocks.ToolUseBlock(
            toolUseBlock.ID,
            toolUseBlock.Name,
            argumentsJson));
      }
    }

    return blocks;
  }

  internal static string MapStopReason(StopReason stopReason) => stopReason switch
  {
    _ when stopReason == StopReason.EndTurn => "end_turn",
    _ when stopReason == StopReason.ToolUse => "tool_use",
    _ when stopReason == StopReason.MaxTokens => "max_tokens",
    _ when stopReason == StopReason.StopSequence => "stop_sequence",
    _ when stopReason == StopReason.PauseTurn => "pause_turn",
    _ when stopReason == StopReason.Refusal => "refusal",
    _ => stopReason.ToString(),
  };

  internal static TokenUsage MapUsage(Usage? usage)
  {
    if (usage is null)
    {
      return new TokenUsage(0, 0);
    }

    var inputTokens = (int)(usage.InputTokens
        + (usage.CacheCreationInputTokens ?? 0)
        + (usage.CacheReadInputTokens ?? 0));

    var outputTokens = (int)usage.OutputTokens;

    return new TokenUsage(inputTokens, outputTokens);
  }
}
