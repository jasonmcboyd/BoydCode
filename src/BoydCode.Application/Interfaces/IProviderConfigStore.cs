using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;

namespace BoydCode.Application.Interfaces;

public interface IProviderConfigStore
{
  Task<ProviderProfile?> GetAsync(LlmProviderType provider, CancellationToken ct = default);
  Task SaveAsync(ProviderProfile profile, CancellationToken ct = default);
  Task RemoveAsync(LlmProviderType provider, CancellationToken ct = default);
  Task<IReadOnlyList<ProviderProfile>> GetAllAsync(CancellationToken ct = default);
  Task<LlmProviderType?> GetLastUsedProviderAsync(CancellationToken ct = default);
  Task SetLastUsedProviderAsync(LlmProviderType provider, CancellationToken ct = default);
}
