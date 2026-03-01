using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Infrastructure.LLM.Converters;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace BoydCode.Infrastructure.LLM.Tests;

public sealed class MessageConverterTests
{
  [Fact]
  public void ToMeaiMessages_WithSingleTextMessage_ConvertsCorrectly()
  {
    // Arrange
    var request = new LlmRequest
    {
      Model = "test",
      Messages = [new ConversationMessage(MessageRole.User, "Hello from the test!")],
    };

    // Act
    var messages = MessageConverter.ToMeaiMessages(request);

    // Assert
    messages.Should().HaveCount(1);
    messages[0].Role.Should().Be(ChatRole.User);
    messages[0].Contents.Should().ContainSingle()
        .Which.Should().BeOfType<TextContent>()
        .Which.Text.Should().Be("Hello from the test!");
  }

  [Fact]
  public void ToMeaiMessages_WithSystemPrompt_PrependsChatMessage()
  {
    // Arrange
    var request = new LlmRequest
    {
      Model = "test",
      SystemPrompt = "You are helpful.",
      Messages = [new ConversationMessage(MessageRole.User, "Hi")],
    };

    // Act
    var messages = MessageConverter.ToMeaiMessages(request);

    // Assert
    messages.Should().HaveCount(2);

    messages[0].Role.Should().Be(ChatRole.System);
    messages[0].Contents.Should().ContainSingle()
        .Which.Should().BeOfType<TextContent>()
        .Which.Text.Should().Be("You are helpful.");

    messages[1].Role.Should().Be(ChatRole.User);
    messages[1].Contents.Should().ContainSingle()
        .Which.Should().BeOfType<TextContent>()
        .Which.Text.Should().Be("Hi");
  }
}
