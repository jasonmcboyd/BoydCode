using BoydCode.Domain.Tools;

namespace BoydCode.Application.Interfaces;

public interface IToolRegistry
{
  void Register(ITool tool);
  ITool? GetTool(string name);
  IReadOnlyList<ToolDefinition> GetAllDefinitions();
  IReadOnlyList<ITool> GetAllTools();
}
