namespace BoydCode.Presentation.Console.Renderables;

internal sealed record BannerData
{
  public required string ProviderName { get; init; }
  public required string ModelName { get; init; }
  public required string ProjectName { get; init; }
  public required string ExecutionMode { get; init; }
  public required string WorkingDirectory { get; init; }
  public required string Version { get; init; }
  public string? DockerImage { get; init; }
  public IReadOnlyList<GitInfo> GitRepositories { get; init; } = [];
  public required bool IsConfigured { get; init; }
  public required int TerminalHeight { get; init; }
  public required int TerminalWidth { get; init; }
  public bool Accessible { get; init; }
  public bool SupportsUnicode { get; init; } = true;
  public bool IsResumedSession { get; init; }
  public string? ResumeSessionId { get; init; }
  public int ResumeMessageCount { get; init; }
  public DateTimeOffset? ResumeTimestamp { get; init; }

  public sealed record GitInfo(string RepoRoot, string? Branch);
}
