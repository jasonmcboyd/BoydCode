using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Configuration;

public sealed record ProviderProfile(
    LlmProviderType ProviderType,
    string? ApiKey,
    string? DefaultModel);
