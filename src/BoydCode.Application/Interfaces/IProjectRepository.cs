using BoydCode.Domain.Entities;

namespace BoydCode.Application.Interfaces;

public interface IProjectRepository
{
  Task<Project?> LoadAsync(string name, CancellationToken ct = default);
  Task SaveAsync(Project project, CancellationToken ct = default);
  Task DeleteAsync(string name, CancellationToken ct = default);
  Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken ct = default);
}
