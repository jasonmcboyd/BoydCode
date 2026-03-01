using BoydCode.Domain.Enums;
using BoydCode.Domain.Hooks;
using BoydCode.Domain.Permissions;

namespace BoydCode.Domain.Configuration;

public sealed class AppSettings
{
  public LlmProviderConfig Llm { get; set; } = new();
  public ExecutionConfig Execution { get; set; } = new();
  public PermissionMode PermissionMode { get; set; } = PermissionMode.Default;
  public List<PermissionRule> PermissionRules { get; set; } = [];
  public List<HookDefinition> Hooks { get; set; } = [];
  public int ContextWindowTokenLimit { get; set; } = 100000;
  public int CompactionThresholdPercent { get; set; } = 80;
}
