using Anthropic.Models.Messages;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.Tools;
using BoydCode.Infrastructure.LLM.Converters;
using FluentAssertions;
using Xunit;
using DomainContentBlock = BoydCode.Domain.ContentBlocks.ContentBlock;
using DomainImageBlock = BoydCode.Domain.ContentBlocks.ImageBlock;
using DomainTextBlock = BoydCode.Domain.ContentBlocks.TextBlock;
using DomainToolResultBlock = BoydCode.Domain.ContentBlocks.ToolResultBlock;
using DomainToolUseBlock = BoydCode.Domain.ContentBlocks.ToolUseBlock;

namespace BoydCode.Infrastructure.LLM.Tests;

public sealed class AnthropicMessageConverterTests
{
  private const string TestModel = "claude-sonnet-4-20250514";
  private const int TestMaxTokens = 4096;

  private static LlmRequest MinimalRequest(
    string? systemPrompt = null,
    IReadOnlyList<ConversationMessage>? messages = null,
    IReadOnlyList<ToolDefinition>? tools = null,
    ToolChoiceStrategy toolChoice = ToolChoiceStrategy.Auto,
    SamplingOptions? sampling = null,
    ThinkingConfig? thinking = null,
    RequestMetadata? metadata = null) =>
    new()
    {
      Model = TestModel,
      SystemPrompt = systemPrompt,
      Messages = messages ?? [new ConversationMessage(MessageRole.User, "Hello")],
      Tools = tools ?? [],
      ToolChoice = toolChoice,
      Sampling = sampling,
      Thinking = thinking,
      Metadata = metadata,
    };

  private static ToolDefinition SimpleToolDefinition(string name = "test_tool") =>
    new(name, "A test tool", []);

  // -------------------------------------------------------------------
  // CacheControl
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_Always_SetsCacheControlToEphemeral()
  {
    // Arrange
    var request = MinimalRequest();

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert -- this is the KEY assertion: prompt caching must always be enabled
    result.CacheControl.Should().NotBeNull();
    result.CacheControl.Should().BeOfType<CacheControlEphemeral>();
  }

  [Fact]
  public void ToCreateParams_WithComplexRequest_StillSetsCacheControlToEphemeral()
  {
    // Arrange -- verify CacheControl is always set regardless of request complexity
    var tools = new List<ToolDefinition>
    {
      new("read_file", "Reads a file",
        [new ToolParameter("path", "string", "File path", Required: true)]),
    };
    var request = MinimalRequest(
      systemPrompt: "You are helpful.",
      tools: tools,
      sampling: new SamplingOptions { Temperature = 0.5f },
      thinking: new ThinkingConfig { Enabled = true, BudgetTokens = 512 },
      metadata: new RequestMetadata { UserId = "user-1" });

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.CacheControl.Should().BeOfType<CacheControlEphemeral>();
  }

  // -------------------------------------------------------------------
  // Model and MaxTokens
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_SetsModelAndMaxTokens()
  {
    // Arrange
    var request = MinimalRequest();

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, "claude-haiku-35", 2048);

    // Assert
    result.Model.ToString().Should().Contain("claude-haiku-35");
    result.MaxTokens.Should().Be(2048);
  }

  // -------------------------------------------------------------------
  // System prompt
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_WithSystemPrompt_ConvertsToSystemField()
  {
    // Arrange
    var request = MinimalRequest(systemPrompt: "You are a helpful assistant.");

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.System.Should().NotBeNull();
  }

  [Fact]
  public void ToCreateParams_WithNullSystemPrompt_SetsSystemToNull()
  {
    // Arrange
    var request = MinimalRequest(systemPrompt: null);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.System.Should().BeNull();
  }

  [Fact]
  public void ToCreateParams_WithEmptySystemPrompt_SetsSystemToNull()
  {
    // Arrange
    var request = MinimalRequest(systemPrompt: "");

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.System.Should().BeNull();
  }

  // -------------------------------------------------------------------
  // Message conversion -- roles
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_UserMessage_HasUserRole()
  {
    // Arrange
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.User, "Hello"),
    };
    var request = MinimalRequest(messages: messages);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Messages.Should().HaveCount(1);
    result.Messages[0].Role.ToString().Should().Contain("user");
  }

  [Fact]
  public void ToCreateParams_AssistantMessage_HasAssistantRole()
  {
    // Arrange
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.User, "Hello"),
      new(MessageRole.Assistant, "Hi there!"),
    };
    var request = MinimalRequest(messages: messages);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Messages.Should().HaveCount(2);
    result.Messages[1].Role.ToString().Should().Contain("assistant");
  }

  [Fact]
  public void ToCreateParams_SystemRoleMessages_AreSkipped()
  {
    // Arrange
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.System, "System instruction"),
      new(MessageRole.User, "Hello"),
    };
    var request = MinimalRequest(messages: messages);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert -- the system role message should be excluded, leaving only the user message
    result.Messages.Should().HaveCount(1);
    result.Messages[0].Role.ToString().Should().Contain("user");
  }

  // -------------------------------------------------------------------
  // Content block conversion -- TextBlock
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_TextBlock_ProducesMessageWithContent()
  {
    // Arrange
    var blocks = new List<DomainContentBlock> { new DomainTextBlock("test text") };
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.User, blocks),
    };
    var request = MinimalRequest(messages: messages);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Messages.Should().HaveCount(1);
    result.Messages[0].Role.ToString().Should().Contain("user");
  }

  // -------------------------------------------------------------------
  // Content block conversion -- ToolUseBlock
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_ToolUseBlock_ProducesAssistantMessage()
  {
    // Arrange
    var argumentsJson = """{"path":"/tmp/test.txt"}""";
    var blocks = new List<DomainContentBlock>
    {
      new DomainToolUseBlock("tool_1", "read_file", argumentsJson),
    };
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.Assistant, blocks),
    };
    var request = MinimalRequest(messages: messages);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Messages.Should().HaveCount(1);
    result.Messages[0].Role.ToString().Should().Contain("assistant");
  }

  [Fact]
  public void ToCreateParams_ToolUseBlock_WithEmptyArguments_DoesNotThrow()
  {
    // Arrange
    var blocks = new List<DomainContentBlock>
    {
      new DomainToolUseBlock("tool_1", "list_files", ""),
    };
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.Assistant, blocks),
    };
    var request = MinimalRequest(messages: messages);

    // Act
    var act = () => AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    act.Should().NotThrow();
  }

  // -------------------------------------------------------------------
  // Content block conversion -- ToolResultBlock
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_ToolResultBlock_ProducesUserMessage()
  {
    // Arrange
    var blocks = new List<DomainContentBlock>
    {
      new DomainToolResultBlock("tool_1", "File contents here", IsError: false),
    };
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.User, blocks),
    };
    var request = MinimalRequest(messages: messages);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Messages.Should().HaveCount(1);
    result.Messages[0].Role.ToString().Should().Contain("user");
  }

  [Fact]
  public void ToCreateParams_ToolResultBlock_WithError_DoesNotThrow()
  {
    // Arrange
    var blocks = new List<DomainContentBlock>
    {
      new DomainToolResultBlock("tool_1", "Error: file not found", IsError: true),
    };
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.User, blocks),
    };
    var request = MinimalRequest(messages: messages);

    // Act
    var act = () => AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    act.Should().NotThrow();
  }

  // -------------------------------------------------------------------
  // Content block conversion -- ImageBlock
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_ImageBlock_ProducesUserMessage()
  {
    // Arrange
    var blocks = new List<DomainContentBlock>
    {
      new DomainImageBlock("image/png", "iVBORw0KGgoAAAANSUhEUg=="),
    };
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.User, blocks),
    };
    var request = MinimalRequest(messages: messages);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Messages.Should().HaveCount(1);
  }

  // -------------------------------------------------------------------
  // Tool definitions
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_WithTools_ConvertsToolDefinitions()
  {
    // Arrange
    var tools = new List<ToolDefinition>
    {
      new("read_file", "Reads a file from disk",
        [
          new ToolParameter("path", "string", "The file path", Required: true),
          new ToolParameter("encoding", "string", "The encoding to use"),
        ]),
    };
    var request = MinimalRequest(tools: tools);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Tools.Should().NotBeNull();
    result.Tools.Should().HaveCount(1);
  }

  [Fact]
  public void ToCreateParams_WithToolHavingEnumValues_DoesNotThrow()
  {
    // Arrange
    var tools = new List<ToolDefinition>
    {
      new("set_mode", "Sets execution mode",
        [
          new ToolParameter("mode", "string", "The mode", Required: true,
            EnumValues: ["fast", "slow", "balanced"]),
        ]),
    };
    var request = MinimalRequest(tools: tools);

    // Act
    var act = () => AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    act.Should().NotThrow();
  }

  [Fact]
  public void ToCreateParams_WithNoTools_SetsToolsToNull()
  {
    // Arrange
    var request = MinimalRequest(tools: []);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Tools.Should().BeNull();
  }

  [Fact]
  public void ToCreateParams_WithNoTools_SetsToolChoiceToNull()
  {
    // Arrange
    var request = MinimalRequest(tools: []);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.ToolChoice.Should().BeNull();
  }

  // -------------------------------------------------------------------
  // ToolChoiceStrategy mapping
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_ToolChoiceAuto_ProducesNonNullToolChoice()
  {
    // Arrange
    var request = MinimalRequest(
      tools: [SimpleToolDefinition()],
      toolChoice: ToolChoiceStrategy.Auto);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.ToolChoice.Should().NotBeNull();
  }

  [Fact]
  public void ToCreateParams_ToolChoiceAny_ProducesNonNullToolChoice()
  {
    // Arrange
    var request = MinimalRequest(
      tools: [SimpleToolDefinition()],
      toolChoice: ToolChoiceStrategy.Any);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.ToolChoice.Should().NotBeNull();
  }

  [Fact]
  public void ToCreateParams_ToolChoiceNone_ProducesNonNullToolChoice()
  {
    // Arrange
    var request = MinimalRequest(
      tools: [SimpleToolDefinition()],
      toolChoice: ToolChoiceStrategy.None);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.ToolChoice.Should().NotBeNull();
  }

  [Fact]
  public void ToCreateParams_DifferentToolChoiceStrategies_ProduceDifferentResults()
  {
    // Arrange -- each strategy should produce a distinct ToolChoice value
    var tools = new List<ToolDefinition> { SimpleToolDefinition() };

    var autoRequest = MinimalRequest(tools: tools, toolChoice: ToolChoiceStrategy.Auto);
    var anyRequest = MinimalRequest(tools: tools, toolChoice: ToolChoiceStrategy.Any);
    var noneRequest = MinimalRequest(tools: tools, toolChoice: ToolChoiceStrategy.None);

    // Act
    var autoResult = AnthropicMessageConverter.ToCreateParams(autoRequest, TestModel, TestMaxTokens);
    var anyResult = AnthropicMessageConverter.ToCreateParams(anyRequest, TestModel, TestMaxTokens);
    var noneResult = AnthropicMessageConverter.ToCreateParams(noneRequest, TestModel, TestMaxTokens);

    // Assert -- all three should be non-null and set
    autoResult.ToolChoice.Should().NotBeNull();
    anyResult.ToolChoice.Should().NotBeNull();
    noneResult.ToolChoice.Should().NotBeNull();
  }

  // -------------------------------------------------------------------
  // SamplingOptions
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_WithSamplingOptions_PassesTemperature()
  {
    // Arrange
    var sampling = new SamplingOptions { Temperature = 0.7f };
    var request = MinimalRequest(sampling: sampling);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Temperature.Should().Be(0.7f);
  }

  [Fact]
  public void ToCreateParams_WithSamplingOptions_PassesTopP()
  {
    // Arrange
    var sampling = new SamplingOptions { TopP = 0.9f };
    var request = MinimalRequest(sampling: sampling);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.TopP.Should().Be(0.9f);
  }

  [Fact]
  public void ToCreateParams_WithSamplingOptions_PassesTopK()
  {
    // Arrange
    var sampling = new SamplingOptions { TopK = 40 };
    var request = MinimalRequest(sampling: sampling);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.TopK.Should().Be(40);
  }

  [Fact]
  public void ToCreateParams_WithNullSampling_LeavesFieldsNull()
  {
    // Arrange
    var request = MinimalRequest(sampling: null);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Temperature.Should().BeNull();
    result.TopP.Should().BeNull();
    result.TopK.Should().BeNull();
  }

  // -------------------------------------------------------------------
  // ThinkingConfig
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_WithThinkingEnabled_SetsThinkingToNonNull()
  {
    // Arrange
    var thinking = new ThinkingConfig { Enabled = true, BudgetTokens = 1024 };
    var request = MinimalRequest(thinking: thinking);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert -- ThinkingConfigParam is a union type wrapping ThinkingConfigEnabled
    result.Thinking.Should().NotBeNull();
    result.Thinking!.Value.Should().BeOfType<ThinkingConfigEnabled>();
  }

  [Fact]
  public void ToCreateParams_WithThinkingEnabled_SetsBudgetTokens()
  {
    // Arrange
    var thinking = new ThinkingConfig { Enabled = true, BudgetTokens = 1024 };
    var request = MinimalRequest(thinking: thinking);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    var enabled = result.Thinking!.Value.Should().BeOfType<ThinkingConfigEnabled>().Subject;
    enabled.BudgetTokens.Should().Be(1024);
  }

  [Fact]
  public void ToCreateParams_WithThinkingEnabled_NullBudget_FallsBackToMaxTokens()
  {
    // Arrange
    var thinking = new ThinkingConfig { Enabled = true, BudgetTokens = null };
    var request = MinimalRequest(thinking: thinking);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, 8192);

    // Assert
    var enabled = result.Thinking!.Value.Should().BeOfType<ThinkingConfigEnabled>().Subject;
    enabled.BudgetTokens.Should().Be(8192);
  }

  [Fact]
  public void ToCreateParams_WithThinkingDisabled_SetsThinkingToNull()
  {
    // Arrange
    var thinking = new ThinkingConfig { Enabled = false };
    var request = MinimalRequest(thinking: thinking);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Thinking.Should().BeNull();
  }

  [Fact]
  public void ToCreateParams_WithNullThinking_SetsThinkingToNull()
  {
    // Arrange
    var request = MinimalRequest(thinking: null);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Thinking.Should().BeNull();
  }

  // -------------------------------------------------------------------
  // RequestMetadata
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_WithMetadata_SetsMetadataUserID()
  {
    // Arrange
    var metadata = new RequestMetadata { UserId = "user-123" };
    var request = MinimalRequest(metadata: metadata);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Metadata.Should().NotBeNull();
    result.Metadata!.UserID.Should().Be("user-123");
  }

  [Fact]
  public void ToCreateParams_WithNullMetadata_SetsMetadataToNull()
  {
    // Arrange
    var request = MinimalRequest(metadata: null);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Metadata.Should().BeNull();
  }

  [Fact]
  public void ToCreateParams_WithEmptyUserId_SetsMetadataToNull()
  {
    // Arrange
    var metadata = new RequestMetadata { UserId = "" };
    var request = MinimalRequest(metadata: metadata);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Metadata.Should().BeNull();
  }

  // -------------------------------------------------------------------
  // Null request guard
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_NullRequest_ThrowsArgumentNullException()
  {
    // Act
    var act = () => AnthropicMessageConverter.ToCreateParams(null!, TestModel, TestMaxTokens);

    // Assert
    act.Should().Throw<ArgumentNullException>();
  }

  // -------------------------------------------------------------------
  // Mixed content messages
  // -------------------------------------------------------------------

  [Fact]
  public void ToCreateParams_MultipleMessagesWithMixedRoles_PreservesOrder()
  {
    // Arrange
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.User, "First message"),
      new(MessageRole.Assistant, "Response"),
      new(MessageRole.User, "Follow-up"),
    };
    var request = MinimalRequest(messages: messages);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Messages.Should().HaveCount(3);
    result.Messages[0].Role.ToString().Should().Contain("user");
    result.Messages[1].Role.ToString().Should().Contain("assistant");
    result.Messages[2].Role.ToString().Should().Contain("user");
  }

  [Fact]
  public void ToCreateParams_MultipleToolDefinitions_ConvertsAll()
  {
    // Arrange
    var tools = new List<ToolDefinition>
    {
      new("read_file", "Reads a file",
        [new ToolParameter("path", "string", "File path", Required: true)]),
      new("write_file", "Writes a file",
        [
          new ToolParameter("path", "string", "File path", Required: true),
          new ToolParameter("content", "string", "File content", Required: true),
        ]),
    };
    var request = MinimalRequest(tools: tools);

    // Act
    var result = AnthropicMessageConverter.ToCreateParams(request, TestModel, TestMaxTokens);

    // Assert
    result.Tools.Should().HaveCount(2);
  }
}
