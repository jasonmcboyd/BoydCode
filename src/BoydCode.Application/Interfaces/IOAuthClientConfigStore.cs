using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;

namespace BoydCode.Application.Interfaces;

public interface IOAuthClientConfigStore
{
  Task<OAuthClientConfig?> GetAsync(LlmProviderType provider, CancellationToken ct = default);
  Task SaveAsync(LlmProviderType provider, OAuthClientConfig config, CancellationToken ct = default);
}
