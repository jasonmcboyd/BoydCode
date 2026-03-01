using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Configuration;

public sealed record ResolvedDirectory(
    string Path,
    DirectoryAccessLevel AccessLevel,
    bool Exists,
    bool IsGitRepository,
    string? GitBranch = null,
    string? RepoRoot = null);
