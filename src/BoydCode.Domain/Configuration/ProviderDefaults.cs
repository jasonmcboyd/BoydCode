namespace BoydCode.Domain.Configuration;

using BoydCode.Domain.Enums;

public static class ProviderDefaults
{
  public static ProviderCapabilities For(LlmProviderType provider) => provider switch
  {
    LlmProviderType.Anthropic => new ProviderCapabilities
    {
      SupportsExplicitCaching = false,
      SupportsOAuthSubscription = true,
      MaxContextWindowTokens = 200_000,
      SupportsExtendedThinking = true,
    },
    LlmProviderType.Gemini => new ProviderCapabilities
    {
      SupportsExplicitCaching = true,
      SupportsOAuthSubscription = true,
      MaxContextWindowTokens = 1_000_000,
      SupportsExtendedThinking = true,
    },
    LlmProviderType.OpenAi => new ProviderCapabilities
    {
      SupportsExplicitCaching = false,
      SupportsOAuthSubscription = false,
      MaxContextWindowTokens = 128_000,
      SupportsExtendedThinking = false,
    },
    LlmProviderType.Ollama => new ProviderCapabilities
    {
      SupportsExplicitCaching = false,
      SupportsOAuthSubscription = false,
      MaxContextWindowTokens = 32_000,
      SupportsExtendedThinking = false,
      SupportsImageInput = false,
    },
    _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, $"Unknown provider type: {provider}"),
  };

  public static string DefaultModelFor(LlmProviderType provider) => provider switch
  {
    LlmProviderType.Anthropic => "claude-sonnet-4-20250514",
    LlmProviderType.Gemini => "gemini-2.5-pro",
    LlmProviderType.OpenAi => "gpt-4o",
    LlmProviderType.Ollama => "llama3",
    _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, $"Unknown provider type: {provider}"),
  };
}
