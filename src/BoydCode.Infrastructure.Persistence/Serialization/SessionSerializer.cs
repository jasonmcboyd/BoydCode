using System.Text.Json;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;

namespace BoydCode.Infrastructure.Persistence.Serialization;

/// <summary>
/// Handles conversion between <see cref="Session"/> domain objects and their
/// JSON-serializable <see cref="SessionDocument"/> counterparts.
/// </summary>
internal static class SessionSerializer
{
  private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

  private static JsonSerializerOptions CreateOptions()
  {
    var options = new JsonSerializerOptions
    {
      WriteIndented = true,
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    options.Converters.Add(new ContentBlockConverter());
    return options;
  }

  public static string Serialize(Session session)
  {
    var doc = ToDocument(session);
    return JsonSerializer.Serialize(doc, SerializerOptions);
  }

  public static Session Deserialize(string json)
  {
    var doc = JsonSerializer.Deserialize<SessionDocument>(json, SerializerOptions)
        ?? throw new JsonException("Failed to deserialize session document.");
    return FromDocument(doc);
  }

  private static SessionDocument ToDocument(Session session)
  {
    var messages = new List<MessageDocument>(session.Conversation.Messages.Count);

    foreach (var msg in session.Conversation.Messages)
    {
      messages.Add(new MessageDocument
      {
        Role = msg.Role.ToString().ToLowerInvariant(),
        Timestamp = msg.Timestamp,
        Content = [.. msg.Content],
      });
    }

    return new SessionDocument
    {
      Id = session.Id,
      WorkingDirectory = session.WorkingDirectory,
      CreatedAt = session.CreatedAt,
      LastAccessedAt = session.LastAccessedAt,
      ProjectName = session.ProjectName,
      SystemPrompt = session.SystemPrompt,
      Messages = messages,
    };
  }

  private static Session FromDocument(SessionDocument doc)
  {
    var conversation = new Conversation();

    foreach (var msgDoc in doc.Messages)
    {
      var role = ParseRole(msgDoc.Role);
      var content = (IReadOnlyList<ContentBlock>)msgDoc.Content;
      var message = new ConversationMessage(role, content, msgDoc.Timestamp);
      conversation.AddMessage(message);
    }

    var session = new Session(doc.Id, doc.WorkingDirectory, conversation, doc.CreatedAt)
    {
      LastAccessedAt = doc.LastAccessedAt,
      ProjectName = doc.ProjectName,
      SystemPrompt = doc.SystemPrompt,
    };

    return session;
  }

  private static MessageRole ParseRole(string role) =>
      role switch
      {
        "user" => MessageRole.User,
        "assistant" => MessageRole.Assistant,
        "system" => MessageRole.System,
        _ => throw new JsonException($"Unknown message role: {role}"),
      };
}
