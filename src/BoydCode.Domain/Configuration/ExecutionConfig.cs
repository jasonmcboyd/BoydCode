using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Configuration;

public sealed class ExecutionConfig
{
  public ExecutionMode Mode { get; set; } = ExecutionMode.InProcess;
  public ContainerConfig? Container { get; set; }
  public List<string> JeaProfiles { get; set; } = [];
  public bool AllowInProcess { get; set; } = true;
}
