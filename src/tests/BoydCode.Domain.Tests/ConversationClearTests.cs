using BoydCode.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BoydCode.Domain.Tests;

public sealed class ConversationClearTests
{
  [Fact]
  public void Clear_EmptyConversation_ReturnsZero()
  {
    // Arrange
    var conversation = new Conversation();

    // Act
    var result = conversation.Clear();

    // Assert
    result.Should().Be(0);
    conversation.Messages.Should().BeEmpty();
  }

  [Fact]
  public void Clear_WithMessages_ReturnsCountAndClears()
  {
    // Arrange
    var conversation = new Conversation();
    conversation.AddUserMessage("First message");
    conversation.AddAssistantMessage("Second message");
    conversation.AddUserMessage("Third message");

    // Act
    var result = conversation.Clear();

    // Assert
    result.Should().Be(3);
    conversation.Messages.Count.Should().Be(0);
  }
}
