namespace BoydCode.Domain.Entities;

public sealed class Session
{
  public string Id { get; }
  public string WorkingDirectory { get; set; }
  public Conversation Conversation { get; }
  public DateTimeOffset CreatedAt { get; }
  public DateTimeOffset LastAccessedAt { get; set; }
  public string? ProjectName { get; set; }
  public string? Name { get; set; }
  public string? SystemPrompt { get; set; }

  /// <summary>
  /// BOYDCODE.md content loaded from global and project directories.
  /// Not persisted — loaded fresh each session from disk.
  /// </summary>
  public string? PromptExtensions { get; set; }

  public Session(string workingDirectory)
  {
    Id = Guid.NewGuid().ToString("N")[..12];
    WorkingDirectory = workingDirectory;
    Conversation = new Conversation();
    CreatedAt = DateTimeOffset.UtcNow;
    LastAccessedAt = DateTimeOffset.UtcNow;
  }

  public Session(string id, string workingDirectory, Conversation conversation, DateTimeOffset createdAt)
  {
    Id = id;
    WorkingDirectory = workingDirectory;
    Conversation = conversation;
    CreatedAt = createdAt;
    LastAccessedAt = DateTimeOffset.UtcNow;
  }
}
