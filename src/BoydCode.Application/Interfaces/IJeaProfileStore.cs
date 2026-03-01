using BoydCode.Domain.Configuration;

namespace BoydCode.Application.Interfaces;

public interface IJeaProfileStore
{
  Task<JeaProfile?> LoadAsync(string name, CancellationToken ct = default);
  Task SaveAsync(JeaProfile profile, CancellationToken ct = default);
  Task DeleteAsync(string name, CancellationToken ct = default);
  Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken ct = default);
}
