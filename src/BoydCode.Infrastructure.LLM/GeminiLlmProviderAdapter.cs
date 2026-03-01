using System.Runtime.CompilerServices;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.LlmResponses;
using BoydCode.Infrastructure.LLM.Converters;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using Content = Google.GenAI.Types.Content;

namespace BoydCode.Infrastructure.LLM;

/// <summary>
/// Adapts the Google Gemini provider into the domain <see cref="ILlmProvider"/> interface.
/// Uses MEAI <see cref="IChatClient"/> for standard send/stream and the native
/// <see cref="Client"/> for explicit caching operations.
/// </summary>
public sealed class GeminiLlmProviderAdapter : ILlmProvider, IExplicitCacheProvider, IDisposable
{
  private readonly IChatClient _chatClient;
  private readonly Client _nativeClient;
  private readonly string _model;
  private readonly int _maxTokens;
  private readonly ProviderCapabilities _capabilities;

  public ProviderCapabilities Capabilities => _capabilities;

  public GeminiLlmProviderAdapter(
      IChatClient chatClient,
      Client nativeClient,
      string model,
      int maxTokens,
      ProviderCapabilities capabilities)
  {
    _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    _nativeClient = nativeClient ?? throw new ArgumentNullException(nameof(nativeClient));
    _model = model;
    _maxTokens = maxTokens;
    _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
  }

  public async Task<LlmResponse> SendAsync(
      LlmRequest request,
      CancellationToken ct = default)
  {
    var messages = MessageConverter.ToMeaiMessages(request);
    var aiTools = MessageConverter.ToMeaiTools(request.Tools);
    var options = BuildChatOptions(request, aiTools);

    var response = await _chatClient.GetResponseAsync(messages, options, ct).ConfigureAwait(false);

    return ResponseConverter.ToDomain(response);
  }

  public async IAsyncEnumerable<StreamChunk> StreamAsync(
      LlmRequest request,
      [EnumeratorCancellation] CancellationToken ct = default)
  {
    var messages = MessageConverter.ToMeaiMessages(request);
    var aiTools = MessageConverter.ToMeaiTools(request.Tools);
    var options = BuildChatOptions(request, aiTools);

    var converter = new StreamingResponseConverter();

    await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, options, ct).ConfigureAwait(false))
    {
      foreach (var chunk in converter.ProcessUpdate(update))
      {
        yield return chunk;
      }
    }

    foreach (var chunk in converter.Complete())
    {
      yield return chunk;
    }
  }

  public async Task<string> CreateCacheAsync(
      string displayName,
      string content,
      TimeSpan ttl,
      CancellationToken ct = default)
  {
    var systemContent = new Content
    {
      Parts = [new Part { Text = content }],
      Role = "user",
    };

    var cachedContent = await _nativeClient.Caches.CreateAsync(
        model: _model,
        config: new CreateCachedContentConfig
        {
          DisplayName = displayName,
          SystemInstruction = systemContent,
          Ttl = $"{(int)ttl.TotalSeconds}s",
        },
        cancellationToken: ct).ConfigureAwait(false);

    return cachedContent.Name!;
  }

  public async Task<LlmResponse> SendWithCacheAsync(
      string cacheId,
      LlmRequest request,
      CancellationToken ct = default)
  {
    // For cached requests, we use the native client to reference the cache,
    // but fall back to MEAI for the actual request since cache reference
    // is set via CachedContent property on the GenerateContentConfig.
    // For now, delegate to standard SendAsync -- cache integration will be
    // refined when the caching pipeline is fully wired.
    return await SendAsync(request, ct).ConfigureAwait(false);
  }

  public void Dispose()
  {
    _chatClient.Dispose();
  }

  private ChatOptions BuildChatOptions(LlmRequest request, IList<AITool> aiTools)
  {
    return new ChatOptions
    {
      ModelId = _model,
      MaxOutputTokens = request.Sampling?.MaxOutputTokens ?? _maxTokens,
      Temperature = request.Sampling?.Temperature,
      TopP = request.Sampling?.TopP,
      TopK = request.Sampling?.TopK,
      Tools = aiTools,
      ToolMode = MapToolChoice(request.ToolChoice),
    };
  }

  private static ChatToolMode MapToolChoice(ToolChoiceStrategy strategy) => strategy switch
  {
    ToolChoiceStrategy.Auto => ChatToolMode.Auto,
    ToolChoiceStrategy.Any => ChatToolMode.RequireAny,
    ToolChoiceStrategy.None => ChatToolMode.None,
    _ => ChatToolMode.Auto,
  };
}
