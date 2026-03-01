using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Permissions;
using BoydCode.Domain.Tools;
using Microsoft.Extensions.Options;

namespace BoydCode.Application.Services;

public sealed class PermissionEngine : IPermissionEngine
{
  private AppSettings _settings;
  private PermissionMode? _projectPermissionMode;
  private IReadOnlyList<PermissionRule>? _projectPermissionRules;

  public PermissionEngine(IOptions<AppSettings> settings)
  {
    _settings = settings.Value;
  }

  public void Configure(PermissionMode? mode, List<PermissionRule>? rules)
  {
    _projectPermissionMode = mode;
    _projectPermissionRules = rules;
  }

  public PermissionLevel Evaluate(ToolDefinition tool, string argumentsJson)
  {
    var effectiveMode = _projectPermissionMode ?? _settings.PermissionMode;

    // Bypass mode: JEA is the security boundary
    if (effectiveMode == PermissionMode.BypassPermissions)
      return PermissionLevel.Allow;

    // Check project-level rules first, then global rules (highest priority)
    var rule = _projectPermissionRules?
        .FirstOrDefault(r => string.Equals(r.ToolName, tool.Name, StringComparison.OrdinalIgnoreCase));
    rule ??= _settings.PermissionRules
        .FirstOrDefault(r => string.Equals(r.ToolName, tool.Name, StringComparison.OrdinalIgnoreCase));
    if (rule is not null)
      return rule.Level;

    // Mode-based defaults
    return effectiveMode switch
    {
      PermissionMode.Plan => tool.Category switch
      {
        ToolCategory.FileRead or ToolCategory.Search or ToolCategory.Web => PermissionLevel.Allow,
        _ => PermissionLevel.Deny
      },
      PermissionMode.DontAsk => PermissionLevel.Allow,
      PermissionMode.AcceptEdits => tool.Category switch
      {
        ToolCategory.FileRead or ToolCategory.FileWrite or ToolCategory.Search => PermissionLevel.Allow,
        ToolCategory.Shell => PermissionLevel.Ask,
        ToolCategory.Web or ToolCategory.Agent => PermissionLevel.Ask,
        _ => PermissionLevel.Ask
      },
      // Default mode
      _ => tool.Category switch
      {
        ToolCategory.FileRead or ToolCategory.Search => PermissionLevel.Allow,
        _ => PermissionLevel.Ask
      }
    };
  }
}
