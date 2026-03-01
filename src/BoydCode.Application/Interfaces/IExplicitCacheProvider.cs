using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.LlmResponses;

namespace BoydCode.Application.Interfaces;

public interface IExplicitCacheProvider
{
  Task<string> CreateCacheAsync(string displayName, string content, TimeSpan ttl, CancellationToken ct = default);
  Task<LlmResponse> SendWithCacheAsync(string cacheId, LlmRequest request, CancellationToken ct = default);
}
