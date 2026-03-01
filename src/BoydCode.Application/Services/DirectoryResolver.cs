using System.Diagnostics.CodeAnalysis;
using BoydCode.Domain.Configuration;

namespace BoydCode.Application.Services;

public sealed class DirectoryResolver
{
  [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance method for DI resolution")]
  public IReadOnlyList<ResolvedDirectory> Resolve(IReadOnlyList<ProjectDirectory> directories)
  {
    var results = new List<ResolvedDirectory>(directories.Count);

    foreach (var dir in directories)
    {
      var fullPath = Path.GetFullPath(dir.Path);
      var exists = Directory.Exists(fullPath);

      if (!exists)
      {
        results.Add(new ResolvedDirectory(
            fullPath,
            dir.AccessLevel,
            Exists: false,
            IsGitRepository: false));
        continue;
      }

      var (isGitRepo, gitBranch, repoRoot) = DetectGitRepository(fullPath);

      results.Add(new ResolvedDirectory(
          fullPath,
          dir.AccessLevel,
          Exists: true,
          IsGitRepository: isGitRepo,
          GitBranch: gitBranch,
          RepoRoot: repoRoot));
    }

    return results;
  }

  private static (bool IsGitRepo, string? Branch, string? RepoRoot) DetectGitRepository(string directoryPath)
  {
    var current = new DirectoryInfo(directoryPath);

    while (current is not null)
    {
      var gitDirPath = Path.Combine(current.FullName, ".git");

      if (Directory.Exists(gitDirPath))
      {
        // Standard git repository: .git is a directory
        var branch = ParseHeadFile(Path.Combine(gitDirPath, "HEAD"));
        return (true, branch, current.FullName);
      }

      if (File.Exists(gitDirPath))
      {
        // Git worktree: .git is a file containing "gitdir: <path>"
        var gitDir = ReadWorktreeGitDir(gitDirPath);
        if (gitDir is not null)
        {
          var branch = ParseHeadFile(Path.Combine(gitDir, "HEAD"));
          return (true, branch, current.FullName);
        }
      }

      current = current.Parent;
    }

    return (false, null, null);
  }

  private static string? ReadWorktreeGitDir(string gitFilePath)
  {
    try
    {
      var content = File.ReadAllText(gitFilePath).Trim();
      const string prefix = "gitdir:";

      if (!content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      {
        return null;
      }

      var gitDir = content[prefix.Length..].Trim();

      // Resolve relative paths against the directory containing the .git file
      if (!Path.IsPathRooted(gitDir))
      {
        var parentDir = Path.GetDirectoryName(gitFilePath)!;
        gitDir = Path.GetFullPath(Path.Combine(parentDir, gitDir));
      }

      return Directory.Exists(gitDir) ? gitDir : null;
    }
    catch (IOException)
    {
      return null;
    }
    catch (UnauthorizedAccessException)
    {
      return null;
    }
  }

  private static string? ParseHeadFile(string headFilePath)
  {
    try
    {
      var content = File.ReadAllText(headFilePath).Trim();
      const string refPrefix = "ref: refs/heads/";

      if (content.StartsWith(refPrefix, StringComparison.Ordinal))
      {
        return content[refPrefix.Length..];
      }

      // Detached HEAD: return first 8 characters of the SHA
      return content.Length >= 8 ? content[..8] : content;
    }
    catch (IOException)
    {
      return null;
    }
    catch (UnauthorizedAccessException)
    {
      return null;
    }
  }
}
