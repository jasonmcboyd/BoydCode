using System.Text.Json;
using BoydCode.Domain.LlmResponses;
using Microsoft.Extensions.AI;

namespace BoydCode.Infrastructure.LLM.Converters;

/// <summary>
/// Bridges MEAI <see cref="ChatResponseUpdate"/> instances to domain <see cref="StreamChunk"/> types.
/// Collects all updates and uses MEAI's <see cref="ChatResponseExtensions.ToChatResponse"/>
/// at completion time to extract finalized tool calls and usage from the accumulated updates.
/// </summary>
internal sealed class StreamingResponseConverter
{
  private readonly List<ChatResponseUpdate> _updates = [];

  /// <summary>
  /// Processes a single streaming update, yielding any <see cref="StreamChunk"/> instances
  /// that can be produced immediately (text content).
  /// </summary>
  public IEnumerable<StreamChunk> ProcessUpdate(ChatResponseUpdate update)
  {
    _updates.Add(update);

    if (update.Text is { Length: > 0 } text)
    {
      yield return new TextChunk(text);
    }
  }

  /// <summary>
  /// Completes the streaming session by converting accumulated updates into a
  /// <see cref="ChatResponse"/>, extracting finalized tool calls from the coalesced
  /// messages, and yielding a <see cref="CompletionChunk"/> with the stop reason and usage.
  /// </summary>
  public IEnumerable<StreamChunk> Complete()
  {
    if (_updates.Count == 0)
    {
      yield return new CompletionChunk("unknown", new TokenUsage(0, 0));
      yield break;
    }

    var response = _updates.ToChatResponse();

    foreach (var message in response.Messages)
    {
      foreach (var content in message.Contents)
      {
        if (content is FunctionCallContent functionCall)
        {
          var argumentsJson = functionCall.Arguments is not null
              ? JsonSerializer.Serialize(functionCall.Arguments)
              : "{}";

          yield return new ToolCallChunk(
              functionCall.CallId ?? $"call_{Guid.NewGuid():N}",
              functionCall.Name,
              argumentsJson);
        }
      }
    }

    var stopReason = ResponseConverter.MapFinishReason(response.FinishReason);
    var usage = ResponseConverter.MapUsage(response.Usage);

    yield return new CompletionChunk(stopReason, usage);
  }
}
