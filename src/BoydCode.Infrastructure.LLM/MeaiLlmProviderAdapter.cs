using System.Runtime.CompilerServices;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.LlmResponses;
using BoydCode.Infrastructure.LLM.Converters;
using Microsoft.Extensions.AI;

namespace BoydCode.Infrastructure.LLM;

/// <summary>
/// Adapts any MEAI <see cref="IChatClient"/> into the domain <see cref="ILlmProvider"/> interface.
/// Uses <see cref="IChatClient"/> directly without FunctionInvokingChatClient, so that tool calls
/// can be intercepted for permissions, JEA routing, hooks, and UI feedback.
/// </summary>
public sealed class MeaiLlmProviderAdapter : ILlmProvider, IDisposable
{
  private readonly IChatClient _chatClient;
  private readonly string _model;
  private readonly int _maxTokens;
  private readonly ProviderCapabilities _capabilities;

  public MeaiLlmProviderAdapter(IChatClient chatClient, string model, int maxTokens, ProviderCapabilities capabilities)
  {
    _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    _model = model;
    _maxTokens = maxTokens;
    _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
  }

  public ProviderCapabilities Capabilities => _capabilities;

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
