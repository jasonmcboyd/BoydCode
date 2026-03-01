using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BoydCode.Domain.Tests;

public sealed class ConversationTests
{
  [Fact]
  public void AddUserMessage_AppendsMessageToConversation()
  {
    // Arrange
    var conversation = new Conversation();

    // Act
    conversation.AddUserMessage("Hello, world!");

    // Assert
    conversation.Messages.Should().HaveCount(1);
    conversation.Messages[0].Role.Should().Be(MessageRole.User);
  }

  [Fact]
  public void EstimateTokenCount_WithTextMessages_ReturnsGreaterThanZero()
  {
    // Arrange
    var conversation = new Conversation();
    conversation.AddUserMessage("This is a test message with enough characters to produce tokens.");

    // Act
    var tokenCount = conversation.EstimateTokenCount();

    // Assert
    tokenCount.Should().BeGreaterThan(0);
  }
}
