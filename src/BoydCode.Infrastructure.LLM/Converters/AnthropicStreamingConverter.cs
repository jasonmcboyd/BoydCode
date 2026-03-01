using System.Text;
using Anthropic.Models.Messages;
using BoydCode.Domain.LlmResponses;

namespace BoydCode.Infrastructure.LLM.Converters;

/// <summary>
/// Processes Anthropic SSE streaming events (<see cref="RawMessageStreamEvent"/>) into
/// domain <see cref="StreamChunk"/> instances. Accumulates partial tool call JSON across
/// delta events and emits a complete <see cref="ToolCallChunk"/> on block stop.
/// Tracks stop reason and usage from message-level events so the caller can emit
/// a <see cref="CompletionChunk"/> without relying on the SDK's aggregator (which has
/// bugs in v12.8.0: a stray <c>Console.WriteLine</c> in <c>CollectAsync</c> and a
/// <c>Single()</c> crash in <c>MergeBlock</c> for tool_use blocks).
/// </summary>
internal sealed class AnthropicStreamingConverter
{
  private readonly Dictionary<long, (string Id, string Name, StringBuilder Json)> _toolBlocks = [];
  private string _stopReason = "unknown";
  private int _inputTokens;
  private int _outputTokens;

  public IEnumerable<StreamChunk> ProcessEvent(RawMessageStreamEvent streamEvent)
  {
    var value = streamEvent.Value;

    if (value is RawMessageStartEvent startEvent)
    {
      // Capture initial usage from the message_start event (input tokens are set here).
      var usage = startEvent.Message.Usage;
      if (usage is not null)
      {
        _inputTokens = (int)(usage.InputTokens
            + (usage.CacheCreationInputTokens ?? 0)
            + (usage.CacheReadInputTokens ?? 0));
        _outputTokens = (int)usage.OutputTokens;
      }

      yield break;
    }

    if (value is RawContentBlockStartEvent blockStartEvent)
    {
      if (blockStartEvent.ContentBlock.Value is Anthropic.Models.Messages.ToolUseBlock toolStart)
      {
        _toolBlocks[blockStartEvent.Index] = (toolStart.ID, toolStart.Name, new StringBuilder());
      }

      yield break;
    }

    if (value is RawContentBlockDeltaEvent deltaEvent)
    {
      var delta = deltaEvent.Delta.Value;

      if (delta is TextDelta textDelta)
      {
        yield return new TextChunk(textDelta.Text);
      }
      else if (delta is InputJsonDelta jsonDelta
          && _toolBlocks.TryGetValue(deltaEvent.Index, out var toolBlock))
      {
        toolBlock.Json.Append(jsonDelta.PartialJson);
      }

      yield break;
    }

    if (value is RawContentBlockStopEvent stopEvent)
    {
      if (_toolBlocks.Remove(stopEvent.Index, out var completed))
      {
        var json = completed.Json.Length > 0 ? completed.Json.ToString() : "{}";
        yield return new ToolCallChunk(completed.Id, completed.Name, json);
      }

      yield break;
    }

    if (value is RawMessageDeltaEvent messageDeltaEvent)
    {
      // Capture stop reason and final output token count from the message_delta event.
      if (messageDeltaEvent.Delta.StopReason is { } sr)
      {
        _stopReason = AnthropicResponseConverter.MapStopReason(sr);
      }

      _outputTokens = (int)messageDeltaEvent.Usage.OutputTokens;

      if (messageDeltaEvent.Usage.InputTokens is { } inputTokens)
      {
        _inputTokens = (int)(inputTokens
            + (messageDeltaEvent.Usage.CacheCreationInputTokens ?? 0)
            + (messageDeltaEvent.Usage.CacheReadInputTokens ?? 0));
      }
    }
  }

  /// <summary>
  /// Returns a <see cref="CompletionChunk"/> built from state accumulated across
  /// <see cref="RawMessageStartEvent"/> and <see cref="RawMessageDeltaEvent"/> events.
  /// Call this after the stream has been fully consumed.
  /// </summary>
  public CompletionChunk ToCompletionChunk()
  {
    return new CompletionChunk(_stopReason, new TokenUsage(_inputTokens, _outputTokens));
  }
}
