using BoydCode.Domain.Entities;

namespace BoydCode.Application.Interfaces;

public interface IAgentDefinitionStore
{
  Task<IReadOnlyList<AgentDefinition>> LoadAllAsync(
      string? projectDirectory = null, CancellationToken ct = default);
}
