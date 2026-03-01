using System.Text.Json;
using System.Text.Json.Serialization;
using BoydCode.Domain.ContentBlocks;

namespace BoydCode.Infrastructure.Persistence.Serialization;

public sealed class ContentBlockConverter : JsonConverter<ContentBlock>
{
  public override ContentBlock Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    using var doc = JsonDocument.ParseValue(ref reader);
    var root = doc.RootElement;
    var type = root.GetProperty("type").GetString();

    return type switch
    {
      "text" => new TextBlock(root.GetProperty("text").GetString()!),
      "tool_use" => new ToolUseBlock(
          root.GetProperty("id").GetString()!,
          root.GetProperty("name").GetString()!,
          root.GetProperty("arguments_json").GetString()!),
      "tool_result" => new ToolResultBlock(
          root.GetProperty("tool_use_id").GetString()!,
          root.GetProperty("content").GetString()!,
          root.TryGetProperty("is_error", out var isErr) && isErr.GetBoolean()),
      "image" => new ImageBlock(
          root.GetProperty("media_type").GetString()!,
          root.GetProperty("base64_data").GetString()!),
      _ => throw new JsonException($"Unknown content block type: {type}")
    };
  }

  public override void Write(Utf8JsonWriter writer, ContentBlock value, JsonSerializerOptions options)
  {
    writer.WriteStartObject();

    switch (value)
    {
      case TextBlock t:
        writer.WriteString("type", "text");
        writer.WriteString("text", t.Text);
        break;
      case ToolUseBlock tu:
        writer.WriteString("type", "tool_use");
        writer.WriteString("id", tu.Id);
        writer.WriteString("name", tu.Name);
        writer.WriteString("arguments_json", tu.ArgumentsJson);
        break;
      case ToolResultBlock tr:
        writer.WriteString("type", "tool_result");
        writer.WriteString("tool_use_id", tr.ToolUseId);
        writer.WriteString("content", tr.Content);
        writer.WriteBoolean("is_error", tr.IsError);
        break;
      case ImageBlock img:
        writer.WriteString("type", "image");
        writer.WriteString("media_type", img.MediaType);
        writer.WriteString("base64_data", img.Base64Data);
        break;
      default:
        throw new JsonException($"Unknown content block type: {value.GetType().Name}");
    }

    writer.WriteEndObject();
  }
}
