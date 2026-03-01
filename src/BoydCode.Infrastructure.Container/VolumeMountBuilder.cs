using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;

namespace BoydCode.Infrastructure.Container;

internal static class VolumeMountBuilder
{
  internal const string MountRoot = "/project";

  internal static IReadOnlyList<string> Build(IReadOnlyList<ResolvedDirectory> directories)
  {
    var mounts = new List<string>();
    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var dir in directories)
    {
      if (dir.AccessLevel == DirectoryAccessLevel.None || !dir.Exists)
      {
        continue;
      }

      var dirName = GetUniqueMountName(dir.Path, usedNames);
      usedNames.Add(dirName);

      var hostPath = dir.Path;
      var containerPath = $"{MountRoot}/{dirName}";
      var mode = dir.AccessLevel == DirectoryAccessLevel.ReadOnly ? "ro" : "rw";

      mounts.Add($"-v \"{hostPath}:{containerPath}:{mode}\"");
    }

    return mounts;
  }

  internal static Dictionary<string, string> BuildPathMapping(IReadOnlyList<ResolvedDirectory> directories)
  {
    var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var dir in directories)
    {
      if (dir.AccessLevel == DirectoryAccessLevel.None || !dir.Exists)
      {
        continue;
      }

      var dirName = GetUniqueMountName(dir.Path, usedNames);
      usedNames.Add(dirName);

      var containerPath = $"{MountRoot}/{dirName}";
      mapping[dir.Path] = containerPath;
    }

    return mapping;
  }

  internal static string? ResolveContainerPath(
      string hostPath,
      Dictionary<string, string> pathMapping)
  {
    // Exact match
    if (pathMapping.TryGetValue(hostPath, out var exact))
    {
      return exact;
    }

    // Prefix match for subdirectories
    foreach (var (hostMount, containerMount) in pathMapping)
    {
      var normalizedHost = hostMount.TrimEnd('\\', '/');
      var normalizedPath = hostPath.TrimEnd('\\', '/');
      if (normalizedPath.StartsWith(normalizedHost, StringComparison.OrdinalIgnoreCase)
          && normalizedPath.Length > normalizedHost.Length
          && (normalizedPath[normalizedHost.Length] == '\\' || normalizedPath[normalizedHost.Length] == '/'))
      {
        var relative = normalizedPath[(normalizedHost.Length + 1)..].Replace('\\', '/');
        return $"{containerMount}/{relative}";
      }
    }

    return null;
  }

  private static string GetUniqueMountName(string path, HashSet<string> usedNames)
  {
    var dirName = Path.GetFileName(path.TrimEnd('\\', '/'));
    if (string.IsNullOrEmpty(dirName))
    {
      dirName = "root";
    }

    if (!usedNames.Contains(dirName))
    {
      return dirName;
    }

    // Disambiguate by prepending parent directory name
    var parent = Path.GetDirectoryName(path.TrimEnd('\\', '/'));
    var parentName = parent is not null ? Path.GetFileName(parent) : "dir";
    var combined = $"{parentName}-{dirName}";

    if (!usedNames.Contains(combined))
    {
      return combined;
    }

    // Last resort: append numeric suffix
    var counter = 2;
    while (usedNames.Contains($"{dirName}-{counter}"))
    {
      counter++;
    }
    return $"{dirName}-{counter}";
  }
}
