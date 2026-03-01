using BoydCode.Domain.Enums;
using BoydCode.Domain.Permissions;
using BoydCode.Domain.Tools;

namespace BoydCode.Application.Interfaces;

public interface IPermissionEngine
{
  PermissionLevel Evaluate(ToolDefinition tool, string argumentsJson);
  void Configure(PermissionMode? mode, List<PermissionRule>? rules);
}
