using BoydCode.Domain.Enums;

namespace BoydCode.Application.Interfaces;

public interface ICredentialStore
{
  Task<string?> GetValidTokenAsync(LlmProviderType provider, CancellationToken ct = default);
  Task SaveAsync(LlmProviderType provider, string accessToken, string refreshToken, DateTimeOffset expiresAt, string scope, CancellationToken ct = default);
  Task ClearAsync(LlmProviderType provider, CancellationToken ct = default);
}
