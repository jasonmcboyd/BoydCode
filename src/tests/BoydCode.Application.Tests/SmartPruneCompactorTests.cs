using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.LlmResponses;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class SmartPruneCompactorTests
{
  // ---------------------------------------------------------------------------
  // Helper: create an ActiveProvider with a mock LLM
  // ---------------------------------------------------------------------------

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
  // Helper: build a conversation with N user/assistant pairs
  // ---------------------------------------------------------------------------

  private static Conversation BuildConversation(int pairCount)
  {
    var conversation = new Conversation();
    for (var i = 0; i < pairCount; i++)
    {
      conversation.AddUserMessage($"User message {i} with some content to give it length");
      conversation.AddAssistantMessage($"Assistant response {i} with enough text for token estimation");
    }
    return conversation;
  }

  // =========================================================================
  //  ParseBoundaryIndices tests
  // =========================================================================

  [Fact]
  public void ParseBoundaryIndices_ValidCommaSeparated_ReturnsCorrectList()
  {
    // Arrange -- 20 messages, valid indices in the interior range
    var text = "4, 8, 14";
    var messageCount = 20;

    // Act
    var result = SmartPruneCompactor.ParseBoundaryIndices(text, messageCount);

    // Assert
    result.Should().Equal(4, 8, 14);
  }

  [Fact]
  public void ParseBoundaryIndices_OutOfRangeIndices_FilteredOut()
  {
    // Arrange -- index 0, index == messageCount-1, and negative are invalid
    // ParseBoundaryIndices filters: index <= 0 or index >= messageCount - 1
    var text = "0, 5, 9, -1";
    var messageCount = 10;

    // Act
    var result = SmartPruneCompactor.ParseBoundaryIndices(text, messageCount);

    // Assert -- only 5 is valid (0 is excluded, 9 == messageCount-1 excluded,
    // -1 is not parseable after digit extraction yields empty)
    result.Should().Equal(5);
  }

  [Fact]
  public void ParseBoundaryIndices_IndicesTooClose_FilteredOut()
  {
    // Arrange -- gap < MinGapBetweenBoundaries (3) is filtered
    var text = "4, 5, 6, 10";
    var messageCount = 20;

    // Act
    var result = SmartPruneCompactor.ParseBoundaryIndices(text, messageCount);

    // Assert -- 4 is kept, 5 and 6 are too close to 4, 10 is kept
    result.Should().Equal(4, 10);
  }

  [Fact]
  public void ParseBoundaryIndices_NonNumericText_ReturnsEmpty()
  {
    // Arrange
    var text = "no boundaries found here";
    var messageCount = 20;

    // Act
    var result = SmartPruneCompactor.ParseBoundaryIndices(text, messageCount);

    // Assert
    result.Should().BeEmpty();
  }

  [Fact]
  public void ParseBoundaryIndices_MixedValidAndInvalid_ReturnsOnlyValid()
  {
    // Arrange -- text with numeric and non-numeric parts
    var text = "abc, 5, xyz, 10, !, 0, 15";
    var messageCount = 20;

    // Act
    var result = SmartPruneCompactor.ParseBoundaryIndices(text, messageCount);

    // Assert -- 5, 10, 15 are valid; 0 is excluded (index <= 0)
    result.Should().Equal(5, 10, 15);
  }

  [Fact]
  public void ParseBoundaryIndices_EmptyString_ReturnsEmpty()
  {
    // Arrange
    var text = "";
    var messageCount = 20;

    // Act
    var result = SmartPruneCompactor.ParseBoundaryIndices(text, messageCount);

    // Assert
    result.Should().BeEmpty();
  }

  // =========================================================================
  //  PruneAtBestBoundary tests
  // =========================================================================

  [Fact]
  public void PruneAtBestBoundary_SelectsBoundaryClosestToTargetTokenCount()
  {
    // Arrange -- 10 messages (5 pairs). Each message ~14 tokens (56 chars / 4).
    // Boundary at index 4 keeps 6 messages (~84 tokens).
    // Boundary at index 6 keeps 4 messages (~56 tokens).
    // Target = 60 tokens, so boundary 6 (diff=4) is closer than boundary 4 (diff=24).
    var conversation = BuildConversation(5);
    var boundaries = new List<int> { 4, 6 };

    // Act
    var result = SmartPruneCompactor.PruneAtBestBoundary(conversation, 60, boundaries);

    // Assert -- should pick boundary 6 (closer to target of 60)
    // Result has compaction notice (1 msg) + 4 kept messages = 5 total
    result.Messages.Count.Should().Be(5);
  }

  [Fact]
  public void PruneAtBestBoundary_PrependsCompactionNotice_WhenPruningFromStart()
  {
    // Arrange -- boundary > 0 means messages were pruned from the beginning
    var conversation = BuildConversation(5);
    var boundaries = new List<int> { 4 };

    // Act
    var result = SmartPruneCompactor.PruneAtBestBoundary(conversation, 50, boundaries);

    // Assert -- first message should be the compaction notice
    var firstMessage = result.Messages[0];
    firstMessage.Role.Should().Be(MessageRole.User);
    var textBlock = firstMessage.Content.OfType<TextBlock>().FirstOrDefault();
    textBlock.Should().NotBeNull();
    textBlock!.Text.Should().Contain("pruned at a topic boundary");
  }

  [Fact]
  public void PruneAtBestBoundary_PreservesMessagesFromBoundaryToEnd()
  {
    // Arrange -- 10 messages, boundary at 6 keeps messages 6..9
    var conversation = BuildConversation(5);
    var boundaries = new List<int> { 6 };

    // Act
    var result = SmartPruneCompactor.PruneAtBestBoundary(conversation, 50, boundaries);

    // Assert -- compaction notice + 4 kept messages (indices 6,7,8,9)
    result.Messages.Count.Should().Be(5);

    // The last message of the result should match the last message of the original
    var originalLast = conversation.Messages[^1].Content.OfType<TextBlock>().First().Text;
    var resultLast = result.Messages[^1].Content.OfType<TextBlock>().First().Text;
    resultLast.Should().Be(originalLast);
  }

  // =========================================================================
  //  CompactAsync tests
  // =========================================================================

  [Fact]
  public async Task CompactAsync_ProviderNotConfigured_FallsBackToEviction()
  {
    // Arrange -- provider not activated (IsConfigured = false)
    var unconfiguredProvider = new ActiveProvider(Substitute.For<ILlmProviderFactory>());
    var fallback = Substitute.For<IContextCompactor>();
    var logger = Substitute.For<ILogger<SmartPruneCompactor>>();

    var conversation = BuildConversation(5);
    var expectedResult = new Conversation();
    expectedResult.AddUserMessage("fallback result");
    fallback.CompactAsync(conversation, 500, Arg.Any<CancellationToken>())
      .Returns(expectedResult);

    var sut = new SmartPruneCompactor(unconfiguredProvider, fallback, logger);

    // Act
    var result = await sut.CompactAsync(conversation, 500);

    // Assert
    result.Should().BeSameAs(expectedResult);
    await fallback.Received(1).CompactAsync(conversation, 500, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task CompactAsync_TooFewMessages_FallsBackToEviction()
  {
    // Arrange -- fewer than MinMessagesForBoundaryDetection (6)
    var (activeProvider, _) = CreateActiveProvider();
    var fallback = Substitute.For<IContextCompactor>();
    var logger = Substitute.For<ILogger<SmartPruneCompactor>>();

    var conversation = BuildConversation(2); // only 4 messages
    var expectedResult = new Conversation();
    expectedResult.AddUserMessage("fallback result");
    fallback.CompactAsync(conversation, 500, Arg.Any<CancellationToken>())
      .Returns(expectedResult);

    var sut = new SmartPruneCompactor(activeProvider, fallback, logger);

    // Act
    var result = await sut.CompactAsync(conversation, 500);

    // Assert
    result.Should().BeSameAs(expectedResult);
    await fallback.Received(1).CompactAsync(conversation, 500, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task CompactAsync_BoundaryDetectionThrows_FallsBackToEviction()
  {
    // Arrange -- LLM throws during boundary detection
    var (activeProvider, mockLlm) = CreateActiveProvider();
    var fallback = Substitute.For<IContextCompactor>();
    var logger = Substitute.For<ILogger<SmartPruneCompactor>>();

    var conversation = BuildConversation(5); // 10 messages, above threshold
    var expectedResult = new Conversation();
    expectedResult.AddUserMessage("fallback result");
    fallback.CompactAsync(conversation, 500, Arg.Any<CancellationToken>())
      .Returns(expectedResult);

    mockLlm.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
      .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

    var sut = new SmartPruneCompactor(activeProvider, fallback, logger);

    // Act
    var result = await sut.CompactAsync(conversation, 500);

    // Assert
    result.Should().BeSameAs(expectedResult);
    await fallback.Received(1).CompactAsync(conversation, 500, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task CompactAsync_NoValidBoundaries_FallsBackToEviction()
  {
    // Arrange -- LLM returns text that yields no valid boundary indices
    var (activeProvider, mockLlm) = CreateActiveProvider();
    var fallback = Substitute.For<IContextCompactor>();
    var logger = Substitute.For<ILogger<SmartPruneCompactor>>();

    var conversation = BuildConversation(5); // 10 messages
    var expectedResult = new Conversation();
    expectedResult.AddUserMessage("fallback result");
    fallback.CompactAsync(conversation, 500, Arg.Any<CancellationToken>())
      .Returns(expectedResult);

    // Return indices that are all out of range (0 and 9 for a 10-message conversation)
    mockLlm.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
      .Returns(new LlmResponse
      {
        Content = [new TextBlock("0, 9")],
        StopReason = "end_turn",
        Usage = new TokenUsage(50, 20),
      });

    var sut = new SmartPruneCompactor(activeProvider, fallback, logger);

    // Act
    var result = await sut.CompactAsync(conversation, 500);

    // Assert
    result.Should().BeSameAs(expectedResult);
    await fallback.Received(1).CompactAsync(conversation, 500, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task CompactAsync_HappyPath_BoundariesDetectedAndBestSelected()
  {
    // Arrange -- LLM returns valid boundaries, pruning should occur
    var (activeProvider, mockLlm) = CreateActiveProvider();
    var fallback = Substitute.For<IContextCompactor>();
    var logger = Substitute.For<ILogger<SmartPruneCompactor>>();

    var conversation = BuildConversation(5); // 10 messages
    var originalMessageCount = conversation.Messages.Count;

    // LLM returns boundary at index 4 (keeps messages 4..9 = 6 messages)
    mockLlm.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
      .Returns(new LlmResponse
      {
        Content = [new TextBlock("4")],
        StopReason = "end_turn",
        Usage = new TokenUsage(50, 20),
      });

    var sut = new SmartPruneCompactor(activeProvider, fallback, logger);

    // Act
    var result = await sut.CompactAsync(conversation, 500);

    // Assert -- should NOT have fallen back to eviction
    await fallback.DidNotReceive().CompactAsync(
      Arg.Any<Conversation>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

    // Result should have fewer messages than original (compaction notice + kept messages)
    result.Messages.Count.Should().BeLessThan(originalMessageCount);

    // First message should be the compaction notice
    result.Messages[0].Content.OfType<TextBlock>().First().Text
      .Should().Contain("pruned at a topic boundary");
  }
}
