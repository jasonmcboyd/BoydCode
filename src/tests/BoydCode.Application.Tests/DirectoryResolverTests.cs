using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class DirectoryResolverTests : IDisposable
{
  private readonly DirectoryResolver _resolver = new();
  private readonly List<string> _tempDirs = [];

  private string CreateTempDir()
  {
    var path = Path.Combine(Path.GetTempPath(), "boydcode-test-" + Path.GetRandomFileName());
    Directory.CreateDirectory(path);
    _tempDirs.Add(path);
    return path;
  }

  private static void CreateGitRepo(string directory, string headContent)
  {
    var gitDir = Path.Combine(directory, ".git");
    Directory.CreateDirectory(gitDir);
    File.WriteAllText(Path.Combine(gitDir, "HEAD"), headContent);
  }

  public void Dispose()
  {
    foreach (var dir in _tempDirs)
    {
      try { Directory.Delete(dir, recursive: true); }
      catch { /* best-effort cleanup */ }
    }
  }

  [Fact]
  public void Resolve_NonExistentDirectory_ReturnsExistsFalseAndNotGitRepository()
  {
    // Arrange
    var fakePath = Path.Combine(Path.GetTempPath(), "boydcode-test-" + Path.GetRandomFileName());
    var directories = new List<ProjectDirectory>
        {
            new(fakePath)
        };

    // Act
    var results = _resolver.Resolve(directories);

    // Assert
    results.Should().HaveCount(1);
    var resolved = results[0];
    resolved.Path.Should().Be(Path.GetFullPath(fakePath));
    resolved.Exists.Should().BeFalse();
    resolved.IsGitRepository.Should().BeFalse();
    resolved.GitBranch.Should().BeNull();
    resolved.RepoRoot.Should().BeNull();
  }

  [Fact]
  public void Resolve_ExistingNonGitDirectory_ReturnsExistsTrueAndNotGitRepository()
  {
    // Arrange
    var tempDir = CreateTempDir();
    var directories = new List<ProjectDirectory>
        {
            new(tempDir)
        };

    // Act
    var results = _resolver.Resolve(directories);

    // Assert
    results.Should().HaveCount(1);
    var resolved = results[0];
    resolved.Path.Should().Be(Path.GetFullPath(tempDir));
    resolved.Exists.Should().BeTrue();
    resolved.IsGitRepository.Should().BeFalse();
    resolved.GitBranch.Should().BeNull();
    resolved.RepoRoot.Should().BeNull();
  }

  [Fact]
  public void Resolve_GitRepoWithBranchRef_ReturnsGitRepoWithBranchAndRepoRoot()
  {
    // Arrange
    var tempDir = CreateTempDir();
    CreateGitRepo(tempDir, "ref: refs/heads/main\n");
    var directories = new List<ProjectDirectory>
        {
            new(tempDir)
        };

    // Act
    var results = _resolver.Resolve(directories);

    // Assert
    results.Should().HaveCount(1);
    var resolved = results[0];
    resolved.Exists.Should().BeTrue();
    resolved.IsGitRepository.Should().BeTrue();
    resolved.GitBranch.Should().Be("main");
    resolved.RepoRoot.Should().Be(Path.GetFullPath(tempDir));
  }

  [Fact]
  public void Resolve_GitRepoWithDetachedHead_ReturnsTruncatedShaAsBranch()
  {
    // Arrange
    var tempDir = CreateTempDir();
    var fullSha = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0";
    CreateGitRepo(tempDir, fullSha + "\n");
    var directories = new List<ProjectDirectory>
        {
            new(tempDir)
        };

    // Act
    var results = _resolver.Resolve(directories);

    // Assert
    results.Should().HaveCount(1);
    var resolved = results[0];
    resolved.Exists.Should().BeTrue();
    resolved.IsGitRepository.Should().BeTrue();
    resolved.GitBranch.Should().Be("a1b2c3d4");
    resolved.RepoRoot.Should().Be(Path.GetFullPath(tempDir));
  }

  [Fact]
  public void Resolve_SubdirectoryOfGitRepo_DetectsParentGitRepoAndReturnsRepoRoot()
  {
    // Arrange
    var repoRoot = CreateTempDir();
    CreateGitRepo(repoRoot, "ref: refs/heads/feature/nested\n");
    var subDir = Path.Combine(repoRoot, "src", "lib");
    Directory.CreateDirectory(subDir);

    var directories = new List<ProjectDirectory>
        {
            new(subDir)
        };

    // Act
    var results = _resolver.Resolve(directories);

    // Assert
    results.Should().HaveCount(1);
    var resolved = results[0];
    resolved.Path.Should().Be(Path.GetFullPath(subDir));
    resolved.Exists.Should().BeTrue();
    resolved.IsGitRepository.Should().BeTrue();
    resolved.GitBranch.Should().Be("feature/nested");
    resolved.RepoRoot.Should().Be(Path.GetFullPath(repoRoot));
  }

  [Fact]
  public void Resolve_MultipleDirectories_ResolvesEachCorrectly()
  {
    // Arrange
    var nonExistentPath = Path.Combine(Path.GetTempPath(), "boydcode-test-" + Path.GetRandomFileName());

    var nonGitDir = CreateTempDir();

    var gitDir = CreateTempDir();
    CreateGitRepo(gitDir, "ref: refs/heads/develop\n");

    var directories = new List<ProjectDirectory>
        {
            new(nonExistentPath, DirectoryAccessLevel.None),
            new(nonGitDir, DirectoryAccessLevel.ReadOnly),
            new(gitDir, DirectoryAccessLevel.ReadWrite)
        };

    // Act
    var results = _resolver.Resolve(directories);

    // Assert
    results.Should().HaveCount(3);

    results[0].Exists.Should().BeFalse();
    results[0].IsGitRepository.Should().BeFalse();
    results[0].AccessLevel.Should().Be(DirectoryAccessLevel.None);

    results[1].Exists.Should().BeTrue();
    results[1].IsGitRepository.Should().BeFalse();
    results[1].AccessLevel.Should().Be(DirectoryAccessLevel.ReadOnly);

    results[2].Exists.Should().BeTrue();
    results[2].IsGitRepository.Should().BeTrue();
    results[2].GitBranch.Should().Be("develop");
    results[2].AccessLevel.Should().Be(DirectoryAccessLevel.ReadWrite);
  }

  [Fact]
  public void Resolve_AccessLevel_IsPreservedFromProjectDirectory()
  {
    // Arrange
    var readOnlyDir = CreateTempDir();
    var readWriteDir = CreateTempDir();
    var noneDir = CreateTempDir();

    var directories = new List<ProjectDirectory>
        {
            new(readOnlyDir, DirectoryAccessLevel.ReadOnly),
            new(readWriteDir, DirectoryAccessLevel.ReadWrite),
            new(noneDir, DirectoryAccessLevel.None)
        };

    // Act
    var results = _resolver.Resolve(directories);

    // Assert
    results.Should().HaveCount(3);
    results[0].AccessLevel.Should().Be(DirectoryAccessLevel.ReadOnly);
    results[1].AccessLevel.Should().Be(DirectoryAccessLevel.ReadWrite);
    results[2].AccessLevel.Should().Be(DirectoryAccessLevel.None);
  }

  [Fact]
  public void Resolve_GitWorktreeWithGitFile_DetectsAsGitRepository()
  {
    // Arrange -- simulate a git worktree where .git is a file pointing to the real git dir
    var fakeGitDir = CreateTempDir();
    var worktreeGitDir = Path.Combine(fakeGitDir, "worktrees", "my-worktree");
    Directory.CreateDirectory(worktreeGitDir);
    File.WriteAllText(Path.Combine(worktreeGitDir, "HEAD"), "ref: refs/heads/worktree-branch\n");

    var worktreeDir = CreateTempDir();
    File.WriteAllText(
        Path.Combine(worktreeDir, ".git"),
        $"gitdir: {worktreeGitDir}\n");

    var directories = new List<ProjectDirectory>
        {
            new(worktreeDir)
        };

    // Act
    var results = _resolver.Resolve(directories);

    // Assert
    results.Should().HaveCount(1);
    var resolved = results[0];
    resolved.Exists.Should().BeTrue();
    resolved.IsGitRepository.Should().BeTrue();
    resolved.GitBranch.Should().Be("worktree-branch");
    resolved.RepoRoot.Should().Be(Path.GetFullPath(worktreeDir));
  }

  [Fact]
  public void Resolve_RelativePath_ConvertsToFullPath()
  {
    // Arrange -- use a relative path; Resolve should normalize it
    var tempDir = CreateTempDir();
    var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), tempDir);

    var directories = new List<ProjectDirectory>
        {
            new(relativePath)
        };

    // Act
    var results = _resolver.Resolve(directories);

    // Assert
    results.Should().HaveCount(1);
    var resolved = results[0];
    resolved.Path.Should().Be(Path.GetFullPath(tempDir));
  }

  [Fact]
  public void Resolve_EmptyList_ReturnsEmptyList()
  {
    // Arrange
    var directories = new List<ProjectDirectory>();

    // Act
    var results = _resolver.Resolve(directories);

    // Assert
    results.Should().BeEmpty();
  }

  [Fact]
  public void Resolve_GitRepoWithFeatureBranchSlashes_PreservesFullBranchName()
  {
    // Arrange -- branch names with slashes like "feature/JIRA-123/my-feature"
    var tempDir = CreateTempDir();
    CreateGitRepo(tempDir, "ref: refs/heads/feature/JIRA-123/my-feature\n");
    var directories = new List<ProjectDirectory>
        {
            new(tempDir)
        };

    // Act
    var results = _resolver.Resolve(directories);

    // Assert
    results.Should().HaveCount(1);
    results[0].GitBranch.Should().Be("feature/JIRA-123/my-feature");
  }
}
