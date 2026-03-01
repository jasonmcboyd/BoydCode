using BoydCode.Domain.Entities;

namespace BoydCode.Application.Interfaces;

public interface ISessionRepository
{
  Task SaveAsync(Session session, CancellationToken ct = default);
  Task<Session?> LoadAsync(string sessionId, CancellationToken ct = default);
  Task<IReadOnlyList<Session>> ListAsync(CancellationToken ct = default);
  Task DeleteAsync(string sessionId, CancellationToken ct = default);
}
