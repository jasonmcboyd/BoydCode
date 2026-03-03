using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.LlmResponses;
using BoydCode.Domain.Tools;
using BoydCode.Presentation.Console;
using BoydCode.Presentation.Console.Commands;
using BoydCode.Presentation.Console.Terminal;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class ContextSlashCommandTests
{
  private static ContextSlashCommand CreateSut(
    ActiveSession? activeSession = null,
    ActiveProvider? activeProvider = null,
    AppSettings? settings = null,
    ActiveExecutionEngine? activeEngine = null,
    IConversationLogger? conversationLogger = null,
    IProjectRepository? projectRepository = null,
    ISessionRepository? sessionRepository = null,
    IContextCompactor? contextCompactor = null,
    ActiveProject? activeProject = null,
    DirectoryResolver? directoryResolver = null,
    DirectoryGuard? directoryGuard = null,
    IExecutionEngineFactory? engineFactory = null,
    IUserInterface? ui = null)
  {
    activeSession ??= new ActiveSession();
    activeProvider ??= new ActiveProvider(Substitute.For<ILlmProviderFactory>());
    settings ??= new AppSettings();
    activeEngine ??= new ActiveExecutionEngine();
    conversationLogger ??= Substitute.For<IConversationLogger>();
    projectRepository ??= Substitute.For<IProjectRepository>();
    sessionRepository ??= Substitute.For<ISessionRepository>();
    contextCompactor ??= Substitute.For<IContextCompactor>();
    activeProject ??= new ActiveProject();
    directoryResolver ??= new DirectoryResolver();
    directoryGuard ??= new DirectoryGuard();
    engineFactory ??= Substitute.For<IExecutionEngineFactory>();
    ui ??= Substitute.For<IUserInterface>();
    return new ContextSlashCommand(
      activeSession,
      activeProvider,
      Options.Create(settings),
      activeEngine,
      conversationLogger,
      projectRepository,
      sessionRepository,
      contextCompactor,
      activeProject,
      directoryResolver,
      directoryGuard,
      engineFactory,
      ui);
  }

  private static (ActiveProvider provider, ILlmProvider mockLlm) CreateActiveProvider()
  {
    var mockLlm = Substitute.For<ILlmProvider>();
    mockLlm.Capabilities.Returns(new ProviderCapabilities { MaxContextWindowTokens = 100000 });
    var factory = Substitute.For<ILlmProviderFactory>();
    factory.Create(Arg.Any<LlmProviderConfig>()).Returns(mockLlm);
    var provider = new ActiveProvider(factory);
    provider.Activate(new LlmProviderConfig
    {
      ProviderType = LlmProviderType.Anthropic,
      Model = "test-model",
    });
    return (provider, mockLlm);
  }

  // ---------------------------------------------------------------------------
  // TryHandleAsync routing tests
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task TryHandleAsync_UnrelatedInput_ReturnsFalse()
  {
    // Arrange
    var sut = CreateSut();

    // Act
    var result = await sut.TryHandleAsync("/help");

    // Assert
    result.Should().BeFalse();
  }

  [Fact]
  public async Task TryHandleAsync_ContextBare_ShowsUsage_ReturnsTrue()
  {
    // Arrange -- bare /context shows subcommand usage, same as other commands.
    var sut = CreateSut();

    // Act
    var result = await sut.TryHandleAsync("/context");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task TryHandleAsync_ContextSummarize_ReturnsTrue()
  {
    // Arrange
    var activeSession = new ActiveSession();
    activeSession.Set(new Session("."));
    var sut = CreateSut(activeSession: activeSession);

    // Act
    var result = await sut.TryHandleAsync("/context summarize");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task TryHandleAsync_ContextRefresh_ReturnsTrue()
  {
    // Arrange -- no session/project set, so it hits the error guard early
    var sut = CreateSut();

    // Act
    var result = await sut.TryHandleAsync("/context refresh");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task TryHandleAsync_UnknownSubcommand_ReturnsTrue()
  {
    // Arrange -- unknown subcommand should still be handled (shows usage)
    var sut = CreateSut();

    // Act
    var result = await sut.TryHandleAsync("/context foo");

    // Assert
    result.Should().BeTrue();
  }

  // ---------------------------------------------------------------------------
  // HandleSummarize tests
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task HandleSummarize_NoSession_DoesNotThrow()
  {
    // Arrange -- no session set on ActiveSession
    var sut = CreateSut();

    // Act
    var act = () => sut.TryHandleAsync("/context summarize");

    // Assert -- should complete without throwing
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task HandleSummarize_NoProvider_DoesNotCallLlm()
  {
    // Arrange -- provider not configured (ActiveProvider.IsConfigured = false)
    var activeSession = new ActiveSession();
    var session = new Session(".");
    session.Conversation.AddUserMessage("Hello");
    session.Conversation.AddAssistantMessage("Hi there");
    session.Conversation.AddUserMessage("How are you?");
    session.Conversation.AddAssistantMessage("I'm good");
    activeSession.Set(session);

    var mockFactory = Substitute.For<ILlmProviderFactory>();
    var activeProvider = new ActiveProvider(mockFactory);
    // Do NOT call Activate -- provider is not configured

    var sut = CreateSut(
      activeSession: activeSession,
      activeProvider: activeProvider);

    // Act
    await sut.TryHandleAsync("/context summarize");

    // Assert -- no LLM call should have been made (factory.Create never called)
    mockFactory.DidNotReceive().Create(Arg.Any<LlmProviderConfig>());
  }

  [Fact]
  public async Task HandleSummarize_TooFewMessages_DoesNotCallLlm()
  {
    // Arrange -- conversation with only 3 messages (not enough for meaningful summary)
    var activeSession = new ActiveSession();
    var session = new Session(".");
    session.Conversation.AddUserMessage("Hello");
    session.Conversation.AddAssistantMessage("Hi");
    session.Conversation.AddUserMessage("Bye");
    activeSession.Set(session);

    var (activeProvider, mockLlm) = CreateActiveProvider();

    var sut = CreateSut(
      activeSession: activeSession,
      activeProvider: activeProvider);

    // Act
    await sut.TryHandleAsync("/context summarize");

    // Assert -- LLM should not have been called for such a short conversation
    await mockLlm.DidNotReceive().SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task HandleSummarize_ReplacesConversation_WithSummaryPlusRecentExchange()
  {
    // Arrange -- conversation with enough messages for summarization
    var activeSession = new ActiveSession();
    var session = new Session(".");
    session.Conversation.AddUserMessage("First question about design patterns");
    session.Conversation.AddAssistantMessage("Design patterns are reusable solutions...");
    session.Conversation.AddUserMessage("Tell me about factory pattern");
    session.Conversation.AddAssistantMessage("The factory pattern creates objects...");
    session.Conversation.AddUserMessage("What about observer?");
    session.Conversation.AddAssistantMessage("The observer pattern defines a subscription mechanism.");
    activeSession.Set(session);

    var (activeProvider, mockLlm) = CreateActiveProvider();

    // Mock the LLM to return a summary response
    mockLlm.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
      .Returns(new LlmResponse
      {
        Content = [new TextBlock("Summary of the conversation.")],
        StopReason = "end_turn",
        Usage = new TokenUsage(100, 50),
      });

    var sut = CreateSut(
      activeSession: activeSession,
      activeProvider: activeProvider);

    // Act
    await sut.TryHandleAsync("/context summarize");

    // Assert -- conversation should be replaced with summary + recent exchange
    var messages = session.Conversation.Messages;

    // First message should be a user message with the summary prefix
    messages.Should().NotBeEmpty();
    var firstContent = messages[0].Content.OfType<TextBlock>().FirstOrDefault();
    firstContent.Should().NotBeNull();
    firstContent!.Text.Should().StartWith("[The following is a summary");

    // The recent exchange (last user + assistant pair) should be preserved
    // after the summary message
    messages.Count.Should().BeGreaterThanOrEqualTo(2);

    // Verify LLM was actually called
    await mockLlm.Received(1).SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task HandleSummarize_LlmError_PreservesOriginalConversation()
  {
    // Arrange -- build a conversation and make the LLM throw
    var activeSession = new ActiveSession();
    var session = new Session(".");
    session.Conversation.AddUserMessage("First question");
    session.Conversation.AddAssistantMessage("First answer");
    session.Conversation.AddUserMessage("Second question");
    session.Conversation.AddAssistantMessage("Second answer");
    session.Conversation.AddUserMessage("Third question");
    session.Conversation.AddAssistantMessage("Third answer");
    activeSession.Set(session);

    // Capture original messages to compare after the error
    var originalMessageCount = session.Conversation.Messages.Count;
    var originalFirstText = session.Conversation.Messages[0].Content
      .OfType<TextBlock>().First().Text;

    var (activeProvider, mockLlm) = CreateActiveProvider();

    // Mock the LLM to throw an exception
    mockLlm.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
      .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

    var sut = CreateSut(
      activeSession: activeSession,
      activeProvider: activeProvider);

    // Act -- should not throw to the caller
    var act = () => sut.TryHandleAsync("/context summarize");
    await act.Should().NotThrowAsync();

    // Assert -- conversation should be unchanged
    session.Conversation.Messages.Count.Should().Be(originalMessageCount);
    session.Conversation.Messages[0].Content
      .OfType<TextBlock>().First().Text.Should().Be(originalFirstText);
  }

  [Fact]
  public async Task HandleSummarize_WithFocusTopic_PassesFocusToLlm()
  {
    // Arrange
    var activeSession = new ActiveSession();
    var session = new Session(".");
    session.Conversation.AddUserMessage("How do I set up auth?");
    session.Conversation.AddAssistantMessage("You can use OAuth2...");
    session.Conversation.AddUserMessage("What about JWT?");
    session.Conversation.AddAssistantMessage("JWT is a token format...");
    session.Conversation.AddUserMessage("And refresh tokens?");
    session.Conversation.AddAssistantMessage("Refresh tokens allow...");
    activeSession.Set(session);

    var (activeProvider, mockLlm) = CreateActiveProvider();

    LlmRequest? capturedRequest = null;
    mockLlm.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
      .Returns(callInfo =>
      {
        capturedRequest = callInfo.Arg<LlmRequest>();
        return new LlmResponse
        {
          Content = [new TextBlock("Summary focused on authentication.")],
          StopReason = "end_turn",
          Usage = new TokenUsage(100, 50),
        };
      });

    var sut = CreateSut(
      activeSession: activeSession,
      activeProvider: activeProvider);

    // Act
    await sut.TryHandleAsync("/context summarize authentication");

    // Assert -- the LLM request should reference the focus topic
    capturedRequest.Should().NotBeNull();
    capturedRequest!.SystemPrompt.Should().Contain("Focus topic: authentication");
  }

  // ---------------------------------------------------------------------------
  // HandleRefresh tests
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task HandleRefresh_NoSession_DoesNotThrow()
  {
    // Arrange -- no session or project set
    var sut = CreateSut();

    // Act
    var act = () => sut.TryHandleAsync("/context refresh");

    // Assert
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task HandleRefresh_NoProject_DoesNotThrow()
  {
    // Arrange -- session set but no active project
    var activeSession = new ActiveSession();
    activeSession.Set(new Session("."));
    var sut = CreateSut(activeSession: activeSession);

    // Act
    var act = () => sut.TryHandleAsync("/context refresh");

    // Assert
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task HandleRefresh_ProjectNotFound_DoesNotThrow()
  {
    // Arrange -- project name set but repository returns null
    var activeSession = new ActiveSession();
    activeSession.Set(new Session("."));
    var activeProject = new ActiveProject();
    activeProject.Set("missing-project");
    var projectRepository = Substitute.For<IProjectRepository>();
    projectRepository.LoadAsync("missing-project", Arg.Any<CancellationToken>())
      .Returns((Project?)null);

    var sut = CreateSut(
      activeSession: activeSession,
      activeProject: activeProject,
      projectRepository: projectRepository);

    // Act
    var act = () => sut.TryHandleAsync("/context refresh");

    // Assert
    await act.Should().NotThrowAsync();
  }

  // ---------------------------------------------------------------------------
  // ExtractRecentExchange unit tests
  // ---------------------------------------------------------------------------

  [Fact]
  public void ExtractRecentExchange_EmptyMessages_ReturnsEmpty()
  {
    // Arrange
    var messages = Array.Empty<ConversationMessage>();

    // Act
    var result = ContextSlashCommand.ExtractRecentExchange(messages);

    // Assert
    result.Should().BeEmpty();
  }

  [Fact]
  public void ExtractRecentExchange_SingleMessage_ReturnsEmpty()
  {
    // Arrange -- only one message, need at least 2 for a valid exchange
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.User, "Hello"),
    };

    // Act
    var result = ContextSlashCommand.ExtractRecentExchange(messages);

    // Assert
    result.Should().BeEmpty();
  }

  [Fact]
  public void ExtractRecentExchange_ValidPair_ReturnsBoth()
  {
    // Arrange -- user followed by assistant (the expected pattern)
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.User, "What is clean architecture?"),
      new(MessageRole.Assistant, "Clean architecture separates concerns..."),
    };

    // Act
    var result = ContextSlashCommand.ExtractRecentExchange(messages);

    // Assert
    result.Should().HaveCount(2);
    result[0].Role.Should().Be(MessageRole.User);
    result[1].Role.Should().Be(MessageRole.Assistant);
  }

  [Fact]
  public void ExtractRecentExchange_LastUserIsToolResult_ReturnsEmpty()
  {
    // Arrange -- second-to-last is a user message containing a ToolResultBlock
    // This represents a tool result rather than a genuine user turn, so it
    // should not be extracted as a "recent exchange"
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.User, [new ToolResultBlock("tool-1", "result content")]),
      new(MessageRole.Assistant, "Based on the tool result..."),
    };

    // Act
    var result = ContextSlashCommand.ExtractRecentExchange(messages);

    // Assert -- tool-result user messages are not valid recent exchanges
    result.Should().BeEmpty();
  }

  // ---------------------------------------------------------------------------
  // /context show routing test
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task TryHandleAsync_ContextShow_ReturnsTrue()
  {
    // Arrange -- no session set, so it hits the error guard early
    var sut = CreateSut();

    // Act
    var result = await sut.TryHandleAsync("/context show");

    // Assert
    result.Should().BeTrue();
  }

  // ---------------------------------------------------------------------------
  // ComputeMessageBreakdown tests
  // ---------------------------------------------------------------------------

  [Fact]
  public void ComputeMessageBreakdown_EmptyMessages_AllZeros()
  {
    // Arrange
    var messages = Array.Empty<ConversationMessage>();

    // Act
    var result = ContextSlashCommand.ComputeMessageBreakdown(messages);

    // Assert
    result.UserTextCount.Should().Be(0);
    result.UserTextTokens.Should().Be(0);
    result.AssistantTextCount.Should().Be(0);
    result.AssistantTextTokens.Should().Be(0);
    result.ToolCallCount.Should().Be(0);
    result.ToolCallTokens.Should().Be(0);
    result.ToolResultCount.Should().Be(0);
    result.ToolResultTokens.Should().Be(0);
  }

  [Fact]
  public void ComputeMessageBreakdown_MixedContent_CategorizesCorrectly()
  {
    // Arrange -- messages with different content block types
    // Token estimation: chars / 4 (integer division)
    var messages = new List<ConversationMessage>
    {
      new(MessageRole.User, "Hello world"),           // 11 chars = 2 tokens (11/4=2)
      new(MessageRole.Assistant, "Hi there friend"),   // 15 chars = 3 tokens (15/4=3)
      new(MessageRole.Assistant, [new ToolUseBlock("t1", "Read", new string('a', 36))]),  // (4+36)/4=10 tokens
      new(MessageRole.User, [new ToolResultBlock("t1", new string('r', 80))]),            // 80/4=20 tokens
    };

    // Act
    var result = ContextSlashCommand.ComputeMessageBreakdown(messages);

    // Assert
    result.UserTextCount.Should().Be(1);
    result.UserTextTokens.Should().Be(2);
    result.AssistantTextCount.Should().Be(1);
    result.AssistantTextTokens.Should().Be(3);
    result.ToolCallCount.Should().Be(1);
    result.ToolCallTokens.Should().Be(10);
    result.ToolResultCount.Should().Be(1);
    result.ToolResultTokens.Should().Be(20);
  }

  // ---------------------------------------------------------------------------
  // EstimateToolDefinitionTokens tests
  // ---------------------------------------------------------------------------

  [Fact]
  public void EstimateToolDefinitionTokens_ComputesCorrectly()
  {
    // Arrange -- tool with known character counts
    // Name: "TestTool" = 8 chars
    // Description: "A test tool description" = 23 chars
    // Parameter name: "path" = 4, type: "string" = 6, description: "The file path" = 13 => 23 chars
    // Total: 8 + 23 + 23 = 54, tokens = 54/4 = 13
    var tool = new ToolDefinition(
      "TestTool",
      "A test tool description",
      [new ToolParameter("path", "string", "The file path", Required: true)]);

    // Act
    var result = ContextSlashCommand.EstimateToolDefinitionTokens(tool);

    // Assert
    result.Should().Be(13);
  }

  // ---------------------------------------------------------------------------
  // FormatCompact tests
  // ---------------------------------------------------------------------------

  [Fact]
  public void FormatCompact_Zero_ReturnsZero()
  {
    TokenFormatting.FormatCompact(0).Should().Be("0");
  }

  [Fact]
  public void FormatCompact_BelowThousand_ReturnsPlainNumber()
  {
    TokenFormatting.FormatCompact(500).Should().Be("500");
    TokenFormatting.FormatCompact(999).Should().Be("999");
  }

  [Fact]
  public void FormatCompact_Thousands_FormatsWithK()
  {
    TokenFormatting.FormatCompact(1000).Should().Be("1.0k");
    TokenFormatting.FormatCompact(1500).Should().Be("1.5k");
    TokenFormatting.FormatCompact(159300).Should().Be("159.3k");
  }

  [Fact]
  public void FormatCompact_Millions_FormatsWithM()
  {
    TokenFormatting.FormatCompact(1500000).Should().Be("1.5M");
  }

  // ---------------------------------------------------------------------------
  // FormatPercent tests
  // ---------------------------------------------------------------------------

  [Fact]
  public void FormatPercent_SmallValue_FormatsOneDecimal()
  {
    TokenFormatting.FormatPercent(0.096).Should().Be("0.1%");
  }

  [Fact]
  public void FormatPercent_Zero_FormatsAsZeroPointZero()
  {
    TokenFormatting.FormatPercent(0.0).Should().Be("0.0%");
  }

  [Fact]
  public void FormatPercent_LargeValue_FormatsOneDecimal()
  {
    TokenFormatting.FormatPercent(79.65).Should().Be("79.7%");
  }

  // ---------------------------------------------------------------------------
  // Descriptor tests
  // ---------------------------------------------------------------------------

  [Fact]
  public void Descriptor_HasCorrectPrefix()
  {
    // Arrange
    var sut = CreateSut();

    // Act & Assert
    sut.Descriptor.Prefix.Should().Be("/context");
  }

  [Fact]
  public void Descriptor_HasExpectedSubcommands()
  {
    // Arrange
    var sut = CreateSut();

    // Act
    var subcommandNames = sut.Descriptor.Subcommands
      .Select(s => s.Usage.Split(' ')[0])
      .ToList();

    // Assert -- compact was removed; show, summarize, prune, refresh remain
    subcommandNames.Should().Contain("show");
    subcommandNames.Should().Contain("summarize");
    subcommandNames.Should().Contain("refresh");
    subcommandNames.Should().NotContain("compact");
  }

  [Fact]
  public void Descriptor_HasPruneSubcommand()
  {
    // Arrange
    var sut = CreateSut();

    // Act
    var subcommandNames = sut.Descriptor.Subcommands
      .Select(s => s.Usage.Split(' ')[0])
      .ToList();

    // Assert
    subcommandNames.Should().Contain("prune");
  }

  [Fact]
  public async Task TryHandleAsync_ContextPrune_ReturnsTrue()
  {
    // Arrange -- no session set, so it hits the error guard early
    var activeSession = new ActiveSession();
    activeSession.Set(new Session("."));
    var sut = CreateSut(activeSession: activeSession);

    // Act
    var result = await sut.TryHandleAsync("/context prune");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public void HandleSummarize_FourChoicesAvailable()
  {
    // Arrange -- SummarizeChoices is private static readonly, so we verify
    // via reflection that the "Fork conversation" option is present
    var field = typeof(ContextSlashCommand)
      .GetField("SummarizeChoices", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

    // Act
    var choices = field?.GetValue(null) as string[];

    // Assert
    choices.Should().NotBeNull();
    choices.Should().HaveCount(4);
    choices.Should().Contain("Apply");
    choices.Should().Contain("Fork conversation");
    choices.Should().Contain("Revise");
    choices.Should().Contain("Cancel");
  }
}
