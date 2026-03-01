namespace BoydCode.Domain.Configuration;

public sealed class ContainerConfig
{
  public required string Image { get; set; }
  public bool Network { get; set; } = true;
  public string Shell { get; set; } = "pwsh";
  public Dictionary<string, string> Environment { get; set; } = [];
}
