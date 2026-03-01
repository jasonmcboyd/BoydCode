using BoydCode.Domain.Configuration;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.LlmResponses;

namespace BoydCode.Application.Interfaces;

public interface ILlmProvider
{
  ProviderCapabilities Capabilities { get; }
  Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default);
  IAsyncEnumerable<StreamChunk> StreamAsync(LlmRequest request, CancellationToken ct = default);
}
