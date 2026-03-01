using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Configuration;

public sealed record JeaProfile(
    string Name,
    PSLanguageModeName LanguageMode,
    IReadOnlyList<string> Modules,
    IReadOnlyList<JeaProfileEntry> Entries)
{
  public IReadOnlyList<string> AllowedCommands =>
      Entries.Where(e => !e.IsDenied).Select(e => e.CommandName).ToList();

  public IReadOnlyList<string> DeniedCommands =>
      Entries.Where(e => e.IsDenied).Select(e => e.CommandName).ToList();
}
