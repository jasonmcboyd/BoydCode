using BoydCode.Application.Interfaces;
using BoydCode.Domain.Entities;
using BoydCode.Infrastructure.Persistence.Serialization;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Persistence;

/// <summary>
/// Persists sessions as individual JSON files under ~/.boydcode/sessions/{sessionId}.json.
/// </summary>
public sealed partial class JsonSessionRepository : ISessionRepository
{
  private readonly ILogger<JsonSessionRepository> _logger;

  public JsonSessionRepository(ILogger<JsonSessionRepository> logger)
  {
    _logger = logger;
  }

  public async Task SaveAsync(Session session, CancellationToken ct = default)
  {
    var directory = GetSessionsDirectory();
    Directory.CreateDirectory(directory);

    var filePath = GetSessionFilePath(session.Id);
    var json = SessionSerializer.Serialize(session);

    await File.WriteAllTextAsync(filePath, json, ct).ConfigureAwait(false);
    LogSessionSaved(session.Id, filePath);
  }

  public async Task<Session?> LoadAsync(string sessionId, CancellationToken ct = default)
  {
    var filePath = GetSessionFilePath(sessionId);

    if (!File.Exists(filePath))
    {
      LogSessionFileNotFound(filePath);
      return null;
    }

    try
    {
      var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
      var session = SessionSerializer.Deserialize(json);
      LogSessionLoaded(sessionId, filePath);
      return session;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      LogSessionLoadFailed(sessionId, filePath, ex);
      return null;
    }
  }

  public async Task<IReadOnlyList<Session>> ListAsync(CancellationToken ct = default)
  {
    var directory = GetSessionsDirectory();

    if (!Directory.Exists(directory))
    {
      return [];
    }

    var sessions = new List<Session>();
    var files = Directory.GetFiles(directory, "*.json");

    foreach (var file in files)
    {
      ct.ThrowIfCancellationRequested();

      try
      {
        var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
        var session = SessionSerializer.Deserialize(json);
        sessions.Add(session);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        LogSessionFileSkipped(file, ex);
      }
    }

    return sessions.AsReadOnly();
  }

  public Task DeleteAsync(string sessionId, CancellationToken ct = default)
  {
    var filePath = GetSessionFilePath(sessionId);

    if (File.Exists(filePath))
    {
      File.Delete(filePath);
      LogSessionDeleted(sessionId, filePath);
    }
    else
    {
      LogSessionFileNotFoundForDeletion(filePath);
    }

    return Task.CompletedTask;
  }

  private static string GetSessionsDirectory() =>
      Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
          ".boydcode",
          "sessions");

  private static string GetSessionFilePath(string sessionId) =>
      Path.Combine(GetSessionsDirectory(), $"{sessionId}.json");

  [LoggerMessage(Level = LogLevel.Debug, Message = "Saved session {SessionId} to {FilePath}")]
  private partial void LogSessionSaved(string sessionId, string filePath);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Session file not found: {FilePath}")]
  private partial void LogSessionFileNotFound(string filePath);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded session {SessionId} from {FilePath}")]
  private partial void LogSessionLoaded(string sessionId, string filePath);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load session {SessionId} from {FilePath}")]
  private partial void LogSessionLoadFailed(string sessionId, string filePath, Exception exception);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load session from {FilePath}, skipping")]
  private partial void LogSessionFileSkipped(string filePath, Exception exception);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Deleted session {SessionId} at {FilePath}")]
  private partial void LogSessionDeleted(string sessionId, string filePath);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Session file not found for deletion: {FilePath}")]
  private partial void LogSessionFileNotFoundForDeletion(string filePath);
}
