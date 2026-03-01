using BoydCode.Domain.Entities;

namespace BoydCode.Application.Interfaces;

public interface IAgentRegistry
{
  Task InitializeAsync(string? projectDirectory = null, CancellationToken ct = default);
  AgentDefinition? GetByName(string name);
  IReadOnlyList<AgentDefinition> GetAll();
}
