using System.Runtime.CompilerServices;
using Anthropic;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.LlmResponses;
using BoydCode.Infrastructure.LLM.Converters;

namespace BoydCode.Infrastructure.LLM;

/// <summary>
/// Adapts the Anthropic native SDK (<see cref="AnthropicClient"/>) into the domain
/// <see cref="ILlmProvider"/> interface. Uses the native API directly instead of MEAI
/// to enable prompt caching via <see cref="Anthropic.Models.Messages.CacheControlEphemeral"/>.
/// </summary>
public sealed class AnthropicLlmProviderAdapter : ILlmProvider
{
  private readonly AnthropicClient _client;
  private readonly string _model;
  private readonly int _maxTokens;
  private readonly ProviderCapabilities _capabilities;

  public AnthropicLlmProviderAdapter(
      AnthropicClient client,
      string model,
      int maxTokens,
      ProviderCapabilities capabilities)
  {
    _client = client ?? throw new ArgumentNullException(nameof(client));
    _model = model;
    _maxTokens = maxTokens;
    _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
  }

  public ProviderCapabilities Capabilities => _capabilities;

  public async Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default)
  {
    var maxOutputTokens = request.Sampling?.MaxOutputTokens ?? _maxTokens;
    var createParams = AnthropicMessageConverter.ToCreateParams(request, _model, maxOutputTokens);

    var message = await _client.Messages.Create(createParams, ct).ConfigureAwait(false);

    return AnthropicResponseConverter.ToDomain(message);
  }

  /// <summary>
  /// Streams the response by iterating <c>CreateStreaming</c> directly, bypassing
  /// the SDK's <c>MessageContentAggregator.CollectAsync</c> which has two bugs in
  /// v12.8.0: a stray <c>Console.WriteLine</c> that dumps raw SSE JSON to stdout,
  /// and a <c>.Single()</c> crash in <c>MergeBlock</c> for tool_use content blocks.
  /// The <see cref="AnthropicStreamingConverter"/> handles all event types and
  /// accumulates stop reason / usage from message-level events.
  /// </summary>
  public async IAsyncEnumerable<StreamChunk> StreamAsync(
      LlmRequest request,
      [EnumeratorCancellation] CancellationToken ct = default)
  {
    var maxOutputTokens = request.Sampling?.MaxOutputTokens ?? _maxTokens;
    var createParams = AnthropicMessageConverter.ToCreateParams(request, _model, maxOutputTokens);

    var converter = new AnthropicStreamingConverter();

    await foreach (var streamEvent in _client.Messages.CreateStreaming(createParams, ct)
        .WithCancellation(ct).ConfigureAwait(false))
    {
      foreach (var chunk in converter.ProcessEvent(streamEvent))
      {
        yield return chunk;
      }
    }

    yield return converter.ToCompletionChunk();
  }
}
