using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Configuration;

public sealed class LlmProviderConfig
{
  public LlmProviderType ProviderType { get; set; } = LlmProviderType.Gemini;
  public string? ApiKey { get; set; }
  public string? AuthToken { get; set; }
  public string? BaseUrl { get; set; }
  public string Model { get; set; } = "gemini-2.5-pro";
  public int MaxTokens { get; set; } = 8192;
  public string? GcpProject { get; set; }
  public string? GcpLocation { get; set; }
}
