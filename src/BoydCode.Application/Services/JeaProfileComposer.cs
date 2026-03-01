using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BoydCode.Application.Services;

public sealed partial class JeaProfileComposer
{
  private readonly IJeaProfileStore _store;
  private readonly ILogger<JeaProfileComposer> _logger;

  public JeaProfileComposer(IJeaProfileStore store, ILogger<JeaProfileComposer> logger)
  {
    _store = store;
    _logger = logger;
  }

  public async Task<EffectiveJeaConfig> ComposeAsync(
      IReadOnlyList<string> profileNames,
      CancellationToken ct = default)
  {
    var globalProfile = await _store.LoadAsync(BuiltInJeaProfile.GlobalName, ct).ConfigureAwait(false)
        ?? BuiltInJeaProfile.Instance;

    var profiles = new List<JeaProfile> { globalProfile };

    foreach (var name in profileNames)
    {
      if (name.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      var profile = await _store.LoadAsync(name, ct).ConfigureAwait(false);
      if (profile is null)
      {
        LogProfileNotFound(name);
        continue;
      }
      profiles.Add(profile);
    }

    var result = Compose(profiles);
    var languageModeName = result.LanguageMode.ToString();
    LogCompositionResult(result.AllowedCommands.Count, languageModeName);
    return result;
  }

  public static EffectiveJeaConfig Compose(IReadOnlyList<JeaProfile> profiles)
  {
    var allows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var denials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var languageMode = PSLanguageModeName.FullLanguage;

    foreach (var profile in profiles)
    {
      foreach (var entry in profile.Entries)
      {
        if (entry.IsDenied)
        {
          denials.Add(entry.CommandName);
        }
        else
        {
          allows.Add(entry.CommandName);
        }
      }

      foreach (var module in profile.Modules)
      {
        modules.Add(module);
      }

      if (profile.LanguageMode > languageMode)
      {
        languageMode = profile.LanguageMode;
      }
    }

    allows.ExceptWith(denials);

    return new EffectiveJeaConfig(
        languageMode,
        allows.Order(StringComparer.OrdinalIgnoreCase).ToList(),
        modules.Order(StringComparer.OrdinalIgnoreCase).ToList());
  }

  [LoggerMessage(Level = LogLevel.Warning, Message = "JEA profile '{ProfileName}' not found; skipping")]
  private partial void LogProfileNotFound(string profileName);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Composed effective JEA config: {CommandCount} commands, language mode {LanguageMode}")]
  private partial void LogCompositionResult(int commandCount, string languageMode);
}
