using System.Text.Json;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.LlmResponses;
using Microsoft.Extensions.AI;

namespace BoydCode.Infrastructure.LLM.Converters;

/// <summary>
/// Converts MEAI <see cref="ChatResponse"/> instances to domain <see cref="LlmResponse"/> instances.
/// </summary>
public static class ResponseConverter
{
  /// <summary>
  /// Converts a MEAI <see cref="ChatResponse"/> into a domain <see cref="LlmResponse"/>.
  /// </summary>
  public static LlmResponse ToDomain(ChatResponse response)
  {
    ArgumentNullException.ThrowIfNull(response);

    var contentBlocks = ExtractContentBlocks(response);
    var stopReason = MapFinishReason(response.FinishReason);
    var usage = MapUsage(response.Usage);

    return new LlmResponse
    {
      Content = contentBlocks,
      StopReason = stopReason,
      Usage = usage,
    };
  }

  private static List<ContentBlock> ExtractContentBlocks(ChatResponse response)
  {
    var blocks = new List<ContentBlock>();

    // The last message in the response is typically the assistant reply.
    // However, with auto-function-calling (which we don't use), there could be
    // multiple messages. We extract content from all assistant messages.
    foreach (var message in response.Messages)
    {
      if (message.Role != ChatRole.Assistant)
      {
        continue;
      }

      foreach (var content in message.Contents)
      {
        var block = ConvertAiContent(content);
        if (block is not null)
        {
          blocks.Add(block);
        }
      }
    }

    // If no assistant messages were found (unlikely but defensive),
    // fall back to extracting from all messages.
    if (blocks.Count == 0)
    {
      foreach (var message in response.Messages)
      {
        foreach (var content in message.Contents)
        {
          var block = ConvertAiContent(content);
          if (block is not null)
          {
            blocks.Add(block);
          }
        }
      }
    }

    return blocks;
  }

  private static ContentBlock? ConvertAiContent(AIContent content) => content switch
  {
    TextContent text when text.Text is not null => new TextBlock(text.Text),
    FunctionCallContent functionCall => ConvertFunctionCall(functionCall),
    _ => null,
  };

  private static ToolUseBlock ConvertFunctionCall(FunctionCallContent functionCall)
  {
    var argumentsJson = functionCall.Arguments is not null
        ? JsonSerializer.Serialize(functionCall.Arguments)
        : "{}";

    return new ToolUseBlock(
        functionCall.CallId,
        functionCall.Name,
        argumentsJson);
  }

  internal static string MapFinishReason(ChatFinishReason? finishReason)
  {
    if (finishReason is null)
    {
      return "unknown";
    }

    if (finishReason == ChatFinishReason.Stop)
    {
      return "end_turn";
    }

    if (finishReason == ChatFinishReason.ToolCalls)
    {
      return "tool_use";
    }

    if (finishReason == ChatFinishReason.Length)
    {
      return "max_tokens";
    }

    if (finishReason == ChatFinishReason.ContentFilter)
    {
      return "content_filter";
    }

    return finishReason.Value.Value;
  }

  internal static TokenUsage MapUsage(UsageDetails? usage)
  {
    if (usage is null)
    {
      return new TokenUsage(0, 0);
    }

    return new TokenUsage(
        (int)(usage.InputTokenCount ?? 0),
        (int)(usage.OutputTokenCount ?? 0));
  }
}
