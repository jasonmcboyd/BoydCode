using System.Text.Json;
using Anthropic.Models.Messages;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.Tools;
using DomainContentBlock = BoydCode.Domain.ContentBlocks.ContentBlock;

namespace BoydCode.Infrastructure.LLM.Converters;

/// <summary>
/// Converts domain <see cref="LlmRequest"/> into Anthropic native <see cref="MessageCreateParams"/>.
/// Sets <see cref="CacheControlEphemeral"/> on the params to enable automatic prompt caching.
/// </summary>
internal static class AnthropicMessageConverter
{
  private static readonly JsonElement ObjectTypeElement =
      JsonDocument.Parse("\"object\"").RootElement.Clone();
  public static MessageCreateParams ToCreateParams(LlmRequest request, string model, int maxTokens)
  {
    ArgumentNullException.ThrowIfNull(request);

    var tools = ConvertTools(request.Tools);

    return new MessageCreateParams
    {
      Model = model,
      MaxTokens = maxTokens,
      Messages = ConvertMessages(request.Messages),
      CacheControl = new CacheControlEphemeral(),
      System = request.SystemPrompt is { Length: > 0 } systemPrompt
          ? (MessageCreateParamsSystem)systemPrompt
          : null,
      Tools = tools.Count > 0 ? tools : null,
      ToolChoice = tools.Count > 0 ? MapToolChoice(request.ToolChoice) : null,
      Temperature = request.Sampling?.Temperature,
      TopP = request.Sampling?.TopP,
      TopK = request.Sampling?.TopK,
      Thinking = MapThinking(request.Thinking, maxTokens),
      Metadata = request.Metadata?.UserId is { Length: > 0 } userId
          ? new Metadata { UserID = userId }
          : null,
    };
  }

  private static ThinkingConfigParam? MapThinking(ThinkingConfig? thinking, int maxTokens)
  {
    if (thinking is not { Enabled: true })
    {
      return null;
    }

    return new ThinkingConfigEnabled
    {
      BudgetTokens = thinking.BudgetTokens ?? maxTokens,
    };
  }

  private static List<MessageParam> ConvertMessages(IReadOnlyList<ConversationMessage> messages)
  {
    var result = new List<MessageParam>(messages.Count);

    foreach (var message in messages)
    {
      if (message.Role == MessageRole.System)
      {
        continue;
      }

      var role = message.Role switch
      {
        MessageRole.User => "user",
        MessageRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(
            nameof(messages), message.Role,
            $"Unsupported message role for Anthropic: {message.Role}"),
      };
      var contentBlocks = ConvertContentBlocks(message.Content);

      result.Add(new MessageParam
      {
        Role = role,
        Content = contentBlocks,
      });
    }

    return result;
  }

  private static List<ContentBlockParam> ConvertContentBlocks(IReadOnlyList<DomainContentBlock> blocks)
  {
    var result = new List<ContentBlockParam>(blocks.Count);

    foreach (var block in blocks)
    {
      if (ConvertContentBlock(block) is { } converted)
      {
        result.Add(converted);
      }
    }

    return result;
  }

  private static ContentBlockParam? ConvertContentBlock(DomainContentBlock block) => block switch
  {
    Domain.ContentBlocks.TextBlock text => (ContentBlockParam)new TextBlockParam { Text = text.Text },
    Domain.ContentBlocks.ToolUseBlock toolUse => ConvertToolUseBlock(toolUse),
    Domain.ContentBlocks.ToolResultBlock toolResult => ConvertToolResultBlock(toolResult),
    Domain.ContentBlocks.ImageBlock image => ConvertImageBlock(image),
    _ => null,
  };

  private static ContentBlockParam ConvertToolUseBlock(Domain.ContentBlocks.ToolUseBlock toolUse)
  {
    var input = toolUse.ArgumentsJson is { Length: > 0 }
        ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(toolUse.ArgumentsJson)
            ?? new Dictionary<string, JsonElement>()
        : new Dictionary<string, JsonElement>();

    return new ToolUseBlockParam
    {
      ID = toolUse.Id,
      Name = toolUse.Name,
      Input = input,
    };
  }

  private static ContentBlockParam ConvertToolResultBlock(Domain.ContentBlocks.ToolResultBlock toolResult)
  {
    return new ToolResultBlockParam
    {
      ToolUseID = toolResult.ToolUseId,
      Content = toolResult.Content,
      IsError = toolResult.IsError,
    };
  }

  private static ContentBlockParam ConvertImageBlock(Domain.ContentBlocks.ImageBlock image)
  {
    return new ImageBlockParam
    {
      Source = new Base64ImageSource
      {
        Data = image.Base64Data,
        MediaType = image.MediaType,
      },
    };
  }

  private static List<ToolUnion> ConvertTools(IReadOnlyList<ToolDefinition> tools)
  {
    var result = new List<ToolUnion>(tools.Count);

    foreach (var tool in tools)
    {
      result.Add(ConvertToolDefinition(tool));
    }

    return result;
  }

  private static Tool ConvertToolDefinition(ToolDefinition tool)
  {
    var inputSchema = BuildInputSchema(tool.Parameters);

    return new Tool
    {
      Name = tool.Name,
      Description = tool.Description,
      InputSchema = inputSchema,
    };
  }

  private static InputSchema BuildInputSchema(IReadOnlyList<ToolParameter> parameters)
  {
    var properties = new Dictionary<string, JsonElement>();
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

      var json = JsonSerializer.Serialize(propSchema);
      using var doc = JsonDocument.Parse(json);
      properties[param.Name] = doc.RootElement.Clone();

      if (param.Required)
      {
        required.Add(param.Name);
      }
    }

    return new InputSchema
    {
      Type = ObjectTypeElement,
      Properties = properties,
      Required = required,
    };
  }

  private static ToolChoice MapToolChoice(ToolChoiceStrategy strategy) => strategy switch
  {
    ToolChoiceStrategy.Auto => new ToolChoiceAuto(),
    ToolChoiceStrategy.Any => new ToolChoiceAny(),
    ToolChoiceStrategy.None => new ToolChoiceNone(),
    _ => new ToolChoiceAuto(),
  };
}
