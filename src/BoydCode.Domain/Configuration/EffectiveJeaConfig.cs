using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Configuration;

public sealed record EffectiveJeaConfig(
    PSLanguageModeName LanguageMode,
    IReadOnlyList<string> AllowedCommands,
    IReadOnlyList<string> Modules)
{
  public static EffectiveJeaConfig Empty { get; } = new(
      PSLanguageModeName.ConstrainedLanguage,
      [],
      []);
}
