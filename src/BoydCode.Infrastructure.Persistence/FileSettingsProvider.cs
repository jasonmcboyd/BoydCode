using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using Microsoft.Extensions.Options;

namespace BoydCode.Infrastructure.Persistence;

/// <summary>
/// Provides application settings from the options pattern and loads system prompt
/// extensions from BOYDCODE.md files in the global and project directories.
/// </summary>
/// <remarks>
/// Settings hierarchy (highest priority wins):
/// CLI args (handled by host builder) -> .boydcode/settings.local.json -> .boydcode/settings.json -> ~/.boydcode/settings.json
///
/// System prompt extensions are loaded from:
/// ~/.boydcode/BOYDCODE.md + {project}/BOYDCODE.md + {project}/.boydcode/BOYDCODE.md
/// </remarks>
public sealed class FileSettingsProvider : ISettingsProvider
{
  private readonly AppSettings _settings;

  public FileSettingsProvider(IOptions<AppSettings> settings)
  {
    _settings = settings.Value;
  }

  public AppSettings GetSettings() => _settings;

  public string? GetSystemPromptExtensions(string workingDirectory)
  {
    var parts = new List<string>();

    var globalPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".boydcode",
        "BOYDCODE.md");

    if (File.Exists(globalPath))
    {
      parts.Add(File.ReadAllText(globalPath));
    }

    var projectPath = Path.Combine(workingDirectory, "BOYDCODE.md");
    if (File.Exists(projectPath))
    {
      parts.Add(File.ReadAllText(projectPath));
    }

    var projectDotPath = Path.Combine(workingDirectory, ".boydcode", "BOYDCODE.md");
    if (File.Exists(projectDotPath))
    {
      parts.Add(File.ReadAllText(projectDotPath));
    }

    return parts.Count > 0 ? string.Join("\n\n---\n\n", parts) : null;
  }
}
