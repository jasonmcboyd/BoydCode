using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;

namespace BoydCode.Application.Services;

public sealed class DirectoryGuard : IDirectoryGuard
{
  private IReadOnlyList<ProjectDirectory>? _directories;
  private IReadOnlyList<ResolvedDirectory>? _resolvedDirectories;
  private bool _isConfigured;

  public IReadOnlyList<ResolvedDirectory> ResolvedDirectories => _resolvedDirectories ?? [];

  public void ConfigureResolved(IReadOnlyList<ResolvedDirectory> resolved)
  {
    _resolvedDirectories = resolved;
    Configure(resolved.Select(r => new ProjectDirectory(r.Path, r.AccessLevel)).ToList());
  }

  public void Configure(IReadOnlyList<ProjectDirectory> directories)
  {
    _directories = directories;
    _isConfigured = true;
  }

  public DirectoryAccessLevel GetAccessLevel(string absolutePath)
  {
    // Not configured = ambient project, allow everything
    if (!_isConfigured || _directories is null || _directories.Count == 0)
    {
      return DirectoryAccessLevel.ReadWrite;
    }

    var normalizedPath = Path.GetFullPath(absolutePath);
    ProjectDirectory? bestMatch = null;
    var bestMatchLength = -1;

    foreach (var dir in _directories)
    {
      var normalizedDir = Path.GetFullPath(dir.Path);

      // Ensure directory path ends with separator for proper prefix matching
      if (!normalizedDir.EndsWith(Path.DirectorySeparatorChar))
      {
        normalizedDir += Path.DirectorySeparatorChar;
      }

      // Check if the path is under this directory (or IS this directory without trailing separator)
      if (normalizedPath.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase) ||
          string.Equals(normalizedPath, normalizedDir.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
      {
        if (normalizedDir.Length > bestMatchLength)
        {
          bestMatch = dir;
          bestMatchLength = normalizedDir.Length;
        }
      }
    }

    return bestMatch?.AccessLevel ?? DirectoryAccessLevel.None;
  }
}
