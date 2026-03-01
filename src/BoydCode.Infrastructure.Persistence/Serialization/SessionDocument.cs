using System.Text.Json.Serialization;
using BoydCode.Domain.ContentBlocks;

namespace BoydCode.Infrastructure.Persistence.Serialization;

/// <summary>
/// Flat DTO for JSON serialization of a Session. Avoids coupling the domain model
/// to serialization concerns.
/// </summary>
internal sealed class SessionDocument
{
  [JsonPropertyName("id")]
  public string Id { get; set; } = string.Empty;

  [JsonPropertyName("working_directory")]
  public string WorkingDirectory { get; set; } = string.Empty;

  [JsonPropertyName("created_at")]
  public DateTimeOffset CreatedAt { get; set; }

  [JsonPropertyName("last_accessed_at")]
  public DateTimeOffset LastAccessedAt { get; set; }

  [JsonPropertyName("project_name")]
  public string? ProjectName { get; set; }

  [JsonPropertyName("name")]
  public string? Name { get; set; }

  [JsonPropertyName("system_prompt")]
  public string? SystemPrompt { get; set; }

  [JsonPropertyName("messages")]
  public List<MessageDocument> Messages { get; set; } = [];
}

internal sealed class MessageDocument
{
  [JsonPropertyName("role")]
  public string Role { get; set; } = string.Empty;

  [JsonPropertyName("timestamp")]
  public DateTimeOffset Timestamp { get; set; }

  [JsonPropertyName("content")]
  public List<ContentBlock> Content { get; set; } = [];
}
