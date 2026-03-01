using System.Text.Json;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.Tools;
using Microsoft.Extensions.AI;

namespace BoydCode.Infrastructure.LLM.Converters;

/// <summary>
/// Converts between BoydCode domain types and Microsoft.Extensions.AI (MEAI) types.
/// </summary>
public static class MessageConverter
{
  /// <summary>
  /// Converts an <see cref="LlmRequest"/> into a list of MEAI <see cref="ChatMessage"/> instances.
  /// System prompt is read from <paramref name="request"/>.SystemPrompt and prepended as a system message.
  /// </summary>
  public static IList<ChatMessage> ToMeaiMessages(LlmRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    var messages = new List<ChatMessage>();

    if (request.SystemPrompt is { Length: > 0 } systemPrompt)
    {
      messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
    }

    foreach (var domainMessage in request.Messages)
    {
      var role = MapRole(domainMessage.Role);
      var contents = new List<AIContent>();

      foreach (var block in domainMessage.Content)
      {
        var aiContent = ConvertContentBlock(block);
        if (aiContent is not null)
        {
          contents.Add(aiContent);
        }
      }

      messages.Add(new ChatMessage(role, contents));
    }

    return messages;
  }

  /// <summary>
  /// Converts domain <see cref="ToolDefinition"/> instances into MEAI <see cref="AITool"/> instances
  /// suitable for passing to <see cref="ChatOptions.Tools"/>.
  /// The tools are declaration-only; we never invoke through MEAI's function-calling middleware.
  /// </summary>
  public static IList<AITool> ToMeaiTools(IReadOnlyList<ToolDefinition> tools)
  {
    ArgumentNullException.ThrowIfNull(tools);

    var aiTools = new List<AITool>(tools.Count);

    foreach (var tool in tools)
    {
      var aiFunction = CreateDeclarationOnlyFunction(tool);
      aiTools.Add(aiFunction);
    }

    return aiTools;
  }

  private static ChatRole MapRole(MessageRole role) => role switch
  {
    MessageRole.User => ChatRole.User,
    MessageRole.Assistant => ChatRole.Assistant,
    MessageRole.System => ChatRole.System,
    _ => throw new ArgumentOutOfRangeException(nameof(role), role, $"Unsupported message role: {role}"),
  };

  private static AIContent? ConvertContentBlock(ContentBlock block) => block switch
  {
    TextBlock text => new TextContent(text.Text),
    ToolUseBlock toolUse => ConvertToolUseBlock(toolUse),
    ToolResultBlock toolResult => ConvertToolResultBlock(toolResult),
    ImageBlock image => ConvertImageBlock(image),
    _ => null,
  };

  private static FunctionCallContent ConvertToolUseBlock(ToolUseBlock toolUse)
  {
    IDictionary<string, object?>? arguments = null;

    if (toolUse.ArgumentsJson is { Length: > 0 })
    {
      arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolUse.ArgumentsJson);
    }

    return new FunctionCallContent(toolUse.Id, toolUse.Name, arguments);
  }

  private static FunctionResultContent ConvertToolResultBlock(ToolResultBlock toolResult)
  {
    object? result = toolResult.IsError
        ? $"Error: {toolResult.Content}"
        : toolResult.Content;

    return new FunctionResultContent(toolResult.ToolUseId, result);
  }

  private static DataContent ConvertImageBlock(ImageBlock image)
  {
    var bytes = Convert.FromBase64String(image.Base64Data);
    return new DataContent(bytes, image.MediaType);
  }

  private static DeclarationOnlyToolFunction CreateDeclarationOnlyFunction(ToolDefinition tool)
  {
    var schemaElement = BuildJsonSchema(tool.Parameters);
    return new DeclarationOnlyToolFunction(tool.Name, tool.Description, schemaElement);
  }

  private static JsonElement BuildJsonSchema(IReadOnlyList<ToolParameter> parameters)
  {
    var properties = new Dictionary<string, object>();
    var required = new List<string>();

    foreach (var param in parameters)
    {
      var propSchema = new Dictionary<string, object>
      {
        ["type"] = param.Type,
        ["description"] = param.Description,
      };

      if (param.EnumValues is { Count: > 0 } enumValues)
      {
        propSchema["enum"] = enumValues;
      }

      properties[param.Name] = propSchema;

      if (param.Required)
      {
        required.Add(param.Name);
      }
    }

    var schema = new Dictionary<string, object>
    {
      ["type"] = "object",
      ["properties"] = properties,
      ["required"] = required,
    };

    var json = JsonSerializer.Serialize(schema);
    using var doc = JsonDocument.Parse(json);
    return doc.RootElement.Clone();
  }
}
