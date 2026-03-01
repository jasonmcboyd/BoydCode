using BoydCode.Application.Interfaces;
using BoydCode.Domain.Tools;

namespace BoydCode.Application.Services;

public sealed class ToolRegistry : IToolRegistry
{
  private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

  public void Register(ITool tool)
  {
    _tools[tool.Definition.Name] = tool;
  }

  public ITool? GetTool(string name) =>
      _tools.GetValueOrDefault(name);

  public IReadOnlyList<ToolDefinition> GetAllDefinitions() =>
      _tools.Values.Select(t => t.Definition).ToList().AsReadOnly();

  public IReadOnlyList<ITool> GetAllTools() =>
      _tools.Values.ToList().AsReadOnly();
}
