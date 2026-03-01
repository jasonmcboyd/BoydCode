namespace BoydCode.Domain.Configuration;

public sealed class AppSettings
{
  public LlmProviderConfig Llm { get; set; } = new();
  public ExecutionConfig Execution { get; set; } = new();
  public int ContextWindowTokenLimit { get; set; } = 100000;
  public int CompactionThresholdPercent { get; set; } = 80;
  public int ContextWarningThresholdPercent { get; set; } = 70;
}
