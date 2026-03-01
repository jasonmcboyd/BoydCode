using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Persistence.Jea;

public sealed partial class FileJeaProfileStore : IJeaProfileStore
{
  private static readonly string ProfileDirectory =
      Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
          ".boydcode",
          "jea");

  private readonly ILogger<FileJeaProfileStore> _logger;

  public FileJeaProfileStore(ILogger<FileJeaProfileStore> logger)
  {
    _logger = logger;
  }

  public async Task<JeaProfile?> LoadAsync(string name, CancellationToken ct = default)
  {
    var filePath = GetProfilePath(name);
    if (!File.Exists(filePath))
    {
      return null;
    }

    try
    {
      var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
      var profile = Parse(name, content);
      LogProfileLoaded(name);
      return profile;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      LogProfileLoadFailed(name, ex);
      return null;
    }
  }

  public async Task SaveAsync(JeaProfile profile, CancellationToken ct = default)
  {
    Directory.CreateDirectory(ProfileDirectory);
    var filePath = GetProfilePath(profile.Name);
    var content = Serialize(profile);
    await File.WriteAllTextAsync(filePath, content, ct).ConfigureAwait(false);
  }

  public Task DeleteAsync(string name, CancellationToken ct = default)
  {
    var filePath = GetProfilePath(name);
    if (File.Exists(filePath))
    {
      File.Delete(filePath);
    }
    return Task.CompletedTask;
  }

  public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken ct = default)
  {
    if (!Directory.Exists(ProfileDirectory))
    {
      return Task.FromResult<IReadOnlyList<string>>([]);
    }

    var names = Directory.GetFiles(ProfileDirectory, "*.profile")
        .Select(f => Path.GetFileNameWithoutExtension(f))
        .Where(n => n is not null)
        .Cast<string>()
        .ToList()
        .AsReadOnly();

    return Task.FromResult<IReadOnlyList<string>>(names);
  }

  internal static JeaProfile Parse(string name, string content)
  {
    var languageMode = PSLanguageModeName.ConstrainedLanguage;
    var modules = new List<string>();
    var entries = new List<JeaProfileEntry>();

    foreach (var rawLine in content.Split('\n'))
    {
      var line = rawLine.Trim();
      if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
      {
        continue;
      }

      if (line.StartsWith("language:", StringComparison.OrdinalIgnoreCase))
      {
        var value = line["language:".Length..].Trim();
        if (Enum.TryParse<PSLanguageModeName>(value, ignoreCase: true, out var parsed))
        {
          languageMode = parsed;
        }
        continue;
      }

      if (line.StartsWith("module:", StringComparison.OrdinalIgnoreCase))
      {
        var value = line["module:".Length..].Trim();
        if (!string.IsNullOrEmpty(value))
        {
          modules.Add(value);
        }
        continue;
      }

      if (line.StartsWith('!'))
      {
        var commandName = line[1..].Trim();
        if (!string.IsNullOrEmpty(commandName))
        {
          entries.Add(new JeaProfileEntry(commandName, IsDenied: true));
        }
      }
      else
      {
        entries.Add(new JeaProfileEntry(line, IsDenied: false));
      }
    }

    return new JeaProfile(name, languageMode, modules, entries);
  }

  internal static string Serialize(JeaProfile profile)
  {
    var lines = new List<string>
        {
            $"language: {profile.LanguageMode}",
        };

    foreach (var module in profile.Modules)
    {
      lines.Add($"module: {module}");
    }

    if (profile.Modules.Count > 0)
    {
      lines.Add("");
    }

    foreach (var entry in profile.Entries)
    {
      lines.Add(entry.IsDenied ? $"!{entry.CommandName}" : entry.CommandName);
    }

    return string.Join("\n", lines) + "\n";
  }

  private static string GetProfilePath(string name) =>
      Path.Combine(ProfileDirectory, $"{name}.profile");

  [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded JEA profile: {ProfileName}")]
  private partial void LogProfileLoaded(string profileName);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load JEA profile: {ProfileName}")]
  private partial void LogProfileLoadFailed(string profileName, Exception exception);
}
