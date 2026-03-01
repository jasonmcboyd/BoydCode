using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.LlmResponses;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class SubAgentExecutorTests
{
  private readonly ILlmProviderFactory _factory = Substitute.For<ILlmProviderFactory>();
  private readonly ILlmProvider _provider = Substitute.For<ILlmProvider>();
  private readonly IExecutionEngine _engine = Substitute.For<IExecutionEngine>();
  private readonly IUserInterface _ui = Substitute.For<IUserInterface>();
  private readonly IConversationLogger _logger = Substitute.For<IConversationLogger>();

  private readonly ActiveProvider _activeProvider;
  private readonly ActiveExecutionEngine _activeEngine = new();

  private readonly LlmProviderConfig _defaultConfig = new()
  {
    Model = "gemini-2.5-pro",
    ProviderType = LlmProviderType.Gemini,
    ApiKey = "test-key",
  };

  public SubAgentExecutorTests()
  {
    _activeProvider = new ActiveProvider(_factory);
    _provider.Capabilities.Returns(new ProviderCapabilities
    {
      MaxContextWindowTokens = 100_000,
      SupportsStreaming = true,
    });
    _engine.GetAvailableCommands().Returns(new List<string> { "Get-ChildItem", "Get-Content" });
  }

  private SubAgentExecutor CreateSut() =>
      new(
          _activeProvider,
          _activeEngine,
          _factory,
          _ui,
          _logger,
          NullLogger<SubAgentExecutor>.Instance);

  private async Task ActivateProviderAndEngine()
  {
    _factory.Create(_defaultConfig).Returns(_provider);
    _activeProvider.Activate(_defaultConfig);
    await _activeEngine.SetAsync(_engine, ExecutionMode.InProcess);
  }

  private static AgentDefinition CreateAgent(
      string name = "test-agent",
      string? modelOverride = null,
      int? maxTurns = null) =>
      new()
      {
        Name = name,
        Description = "Test agent",
        Instructions = "You are a test agent. Do the task.",
        ModelOverride = modelOverride,
        MaxTurns = maxTurns,
      };

  private static LlmResponse CreateTextResponse(string text) =>
      new()
      {
        Content = new List<ContentBlock> { new TextBlock(text) },
        StopReason = "end_turn",
        Usage = new TokenUsage(100, 50),
      };

  private static LlmResponse CreateToolUseResponse(string toolId, string command) =>
      new()
      {
        Content = new List<ContentBlock>
        {
          new TextBlock("Let me run that command."),
          new ToolUseBlock(toolId, "Shell", $"{{\"command\":\"{command}\"}}"),
        },
        StopReason = "tool_use",
        Usage = new TokenUsage(100, 50),
      };

  // ---------------------------------------------------------------------------
  // EndTurn immediately
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task ExecuteAsync_EndTurnImmediately_ReturnsTextResult()
  {
    // Arrange
    await ActivateProviderAndEngine();
    var agent = CreateAgent();
    var sut = CreateSut();

    _provider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .Returns(CreateTextResponse("The task is complete."));

    // Act
    var result = await sut.ExecuteAsync(agent, "Do something", "/work");

    // Assert
    result.Should().Be("The task is complete.");
    await _provider.Received(1).SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
  }

  // ---------------------------------------------------------------------------
  // Shell tool call then end-turn
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task ExecuteAsync_ShellToolCallThenEndTurn_ExecutesCommandAndReturnsText()
  {
    // Arrange
    await ActivateProviderAndEngine();
    var agent = CreateAgent();
    var sut = CreateSut();

    var callCount = 0;
    _provider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .Returns(_ =>
        {
          callCount++;
          return callCount == 1
              ? CreateToolUseResponse("tool-1", "Get-ChildItem")
              : CreateTextResponse("Done. Here are the files.");
        });

    _engine.ExecuteAsync("Get-ChildItem", "/work", null, Arg.Any<CancellationToken>())
        .Returns(new ShellExecutionResult("file1.txt\nfile2.txt", null, false, TimeSpan.FromMilliseconds(50)));

    // Act
    var result = await sut.ExecuteAsync(agent, "List files", "/work");

    // Assert
    result.Should().Be("Done. Here are the files.");
    await _engine.Received(1).ExecuteAsync("Get-ChildItem", "/work", null, Arg.Any<CancellationToken>());
    await _provider.Received(2).SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
  }

  // ---------------------------------------------------------------------------
  // Max turns exceeded
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task ExecuteAsync_MaxTurnsExceeded_ReturnsLastTextContent()
  {
    // Arrange
    await ActivateProviderAndEngine();
    var agent = CreateAgent(maxTurns: 2);
    var sut = CreateSut();

    // LLM keeps returning tool_use blocks on every turn
    _provider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .Returns(CreateToolUseResponse("tool-loop", "echo loop"));

    _engine.ExecuteAsync("echo loop", "/work", null, Arg.Any<CancellationToken>())
        .Returns(new ShellExecutionResult("loop", null, false, TimeSpan.FromMilliseconds(10)));

    // Act
    var result = await sut.ExecuteAsync(agent, "Do looping work", "/work");

    // Assert — the loop runs maxTurns (2) times, then returns the last text content
    // Each turn emits "Let me run that command." as text, so lastTextResponse is set
    result.Should().Be("Let me run that command.");
    await _provider.Received(2).SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task ExecuteAsync_MaxTurnsExceeded_NoText_ReturnsFallbackMessage()
  {
    // Arrange
    await ActivateProviderAndEngine();
    var agent = CreateAgent(maxTurns: 1);
    var sut = CreateSut();

    // Tool use response with no text blocks
    var toolOnlyResponse = new LlmResponse
    {
      Content = new List<ContentBlock>
      {
        new ToolUseBlock("tool-only", "Shell", "{\"command\":\"pwd\"}"),
      },
      StopReason = "tool_use",
      Usage = new TokenUsage(50, 25),
    };

    _provider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .Returns(toolOnlyResponse);

    _engine.ExecuteAsync("pwd", "/work", null, Arg.Any<CancellationToken>())
        .Returns(new ShellExecutionResult("/work", null, false, TimeSpan.FromMilliseconds(5)));

    // Act
    var result = await sut.ExecuteAsync(agent, "Check directory", "/work");

    // Assert — no text was ever returned so the fallback message is used
    result.Should().Be("(No response from agent)");
  }

  // ---------------------------------------------------------------------------
  // Model override
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task ExecuteAsync_ModelOverride_CreatesTemporaryProvider()
  {
    // Arrange
    await ActivateProviderAndEngine();
    var agent = CreateAgent(modelOverride: "claude-opus-4-20250514");
    var sut = CreateSut();

    var overrideProvider = Substitute.For<IDisposableLlmProvider>();
    overrideProvider.Capabilities.Returns(new ProviderCapabilities
    {
      MaxContextWindowTokens = 200_000,
    });
    overrideProvider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .Returns(CreateTextResponse("Override model result."));

    _factory.Create(Arg.Is<LlmProviderConfig>(c => c.Model == "claude-opus-4-20250514"))
        .Returns(overrideProvider);

    // Act
    var result = await sut.ExecuteAsync(agent, "Use override model", "/work");

    // Assert
    result.Should().Be("Override model result.");
    _factory.Received(1).Create(Arg.Is<LlmProviderConfig>(c => c.Model == "claude-opus-4-20250514"));
    overrideProvider.Received(1).Dispose();
  }

  [Fact]
  public async Task ExecuteAsync_ModelOverrideSameAsActive_UsesExistingProvider()
  {
    // Arrange
    await ActivateProviderAndEngine();
    // Model override matches the active provider's model exactly
    var agent = CreateAgent(modelOverride: "gemini-2.5-pro");
    var sut = CreateSut();

    _provider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .Returns(CreateTextResponse("Same model result."));

    // Act
    var result = await sut.ExecuteAsync(agent, "Use same model", "/work");

    // Assert — factory should not be called again for an override config
    // (the original Activate call is the only Create call)
    result.Should().Be("Same model result.");
    _factory.Received(1).Create(Arg.Any<LlmProviderConfig>());
  }

  // ---------------------------------------------------------------------------
  // LLM error handling
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task ExecuteAsync_LlmError_ReturnsErrorString()
  {
    // Arrange
    await ActivateProviderAndEngine();
    var agent = CreateAgent();
    var sut = CreateSut();

    _provider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .ThrowsAsync(new InvalidOperationException("API rate limit exceeded"));

    // Act
    var result = await sut.ExecuteAsync(agent, "Do something", "/work");

    // Assert
    result.Should().Contain("Agent 'test-agent' failed");
    result.Should().Contain("API rate limit exceeded");
  }

  [Fact]
  public async Task ExecuteAsync_OperationCanceled_PropagatesException()
  {
    // Arrange
    await ActivateProviderAndEngine();
    var agent = CreateAgent();
    var sut = CreateSut();

    _provider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .ThrowsAsync(new OperationCanceledException("Cancelled"));

    // Act
    var act = () => sut.ExecuteAsync(agent, "Do something", "/work");

    // Assert — OperationCanceledException should propagate, not be caught
    await act.Should().ThrowAsync<OperationCanceledException>();
  }

  // ---------------------------------------------------------------------------
  // UI feedback
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task ExecuteAsync_RendersHintOnStartAndFinish()
  {
    // Arrange
    await ActivateProviderAndEngine();
    var agent = CreateAgent(name: "my-agent");
    var sut = CreateSut();

    _provider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .Returns(CreateTextResponse("Done."));

    // Act
    await sut.ExecuteAsync(agent, "Do work", "/work");

    // Assert
    _ui.Received(1).RenderHint("Agent 'my-agent' working...");
    _ui.Received(1).RenderHint("Agent 'my-agent' finished.");
  }

  // ---------------------------------------------------------------------------
  // Max turns clamping
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task ExecuteAsync_MaxTurnsClampedToMaxAllowed()
  {
    // Arrange — agent requests 200 turns but max allowed is 100
    await ActivateProviderAndEngine();
    var agent = CreateAgent(maxTurns: 200);
    var sut = CreateSut();

    // LLM always returns end_turn immediately so we just verify construction works
    _provider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .Returns(CreateTextResponse("Quick result."));

    // Act
    var result = await sut.ExecuteAsync(agent, "Fast task", "/work");

    // Assert — the executor does not throw; it clamps to AgentDefaults.MaxAllowedTurns
    result.Should().Be("Quick result.");
  }

  [Fact]
  public async Task ExecuteAsync_NullMaxTurns_UsesDefault()
  {
    // Arrange — agent has no MaxTurns, so AgentDefaults.DefaultMaxTurns (25) is used
    await ActivateProviderAndEngine();
    var agent = CreateAgent(maxTurns: null);
    var sut = CreateSut();

    _provider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .Returns(CreateTextResponse("Default turns result."));

    // Act
    var result = await sut.ExecuteAsync(agent, "Some task", "/work");

    // Assert
    result.Should().Be("Default turns result.");
    // Only 1 call because the first response is end_turn
    await _provider.Received(1).SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
  }

  // ---------------------------------------------------------------------------
  // Unknown tool handling
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task ExecuteAsync_UnknownToolCall_AddsErrorAndContinues()
  {
    // Arrange
    await ActivateProviderAndEngine();
    var agent = CreateAgent();
    var sut = CreateSut();

    var unknownToolResponse = new LlmResponse
    {
      Content = new List<ContentBlock>
      {
        new ToolUseBlock("unknown-1", "ReadFile", "{\"path\":\"/etc/passwd\"}"),
      },
      StopReason = "tool_use",
      Usage = new TokenUsage(80, 40),
    };

    var callCount = 0;
    _provider.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
        .Returns(_ =>
        {
          callCount++;
          return callCount == 1
              ? unknownToolResponse
              : CreateTextResponse("Understood, using Shell instead.");
        });

    // Act
    var result = await sut.ExecuteAsync(agent, "Read a file", "/work");

    // Assert — engine should NOT have been called for the unknown tool
    result.Should().Be("Understood, using Shell instead.");
    await _engine.DidNotReceive().ExecuteAsync(
        Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Action<string>?>(), Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// Combined interface so NSubstitute can create a proxy that is both
  /// <see cref="ILlmProvider"/> and <see cref="IDisposable"/>.
  /// </summary>
  internal interface IDisposableLlmProvider : ILlmProvider, IDisposable { }
}
