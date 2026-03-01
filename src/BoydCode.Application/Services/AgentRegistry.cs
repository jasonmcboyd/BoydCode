using BoydCode.Application.Interfaces;
using BoydCode.Domain.Entities;

namespace BoydCode.Application.Services;

public sealed class AgentRegistry : IAgentRegistry
{
  private readonly IAgentDefinitionStore _store;
  private readonly Dictionary<string, AgentDefinition> _agents = new(StringComparer.OrdinalIgnoreCase);

  public AgentRegistry(IAgentDefinitionStore store)
  {
    _store = store;
  }

  public async Task InitializeAsync(string? projectDirectory = null, CancellationToken ct = default)
  {
    _agents.Clear();
    var definitions = await _store.LoadAllAsync(projectDirectory, ct);

    // User-scoped come first, project-scoped second — assignment naturally overrides
    foreach (var agent in definitions)
    {
      _agents[agent.Name] = agent;
    }
  }

  public AgentDefinition? GetByName(string name) =>
      _agents.TryGetValue(name, out var agent) ? agent : null;

  public IReadOnlyList<AgentDefinition> GetAll() =>
      _agents.Values.ToList().AsReadOnly();
}
