namespace BoydCode.Domain.Configuration;

public sealed record ProviderCapabilities
{
  public bool SupportsExplicitCaching { get; init; }
  public bool SupportsOAuthSubscription { get; init; }
  public int MaxContextWindowTokens { get; init; }
  public bool SupportsStreaming { get; init; } = true;
  public bool SupportsToolUse { get; init; } = true;
  public bool SupportsImageInput { get; init; } = true;
  public bool SupportsExtendedThinking { get; init; }
}
