using BoydCode.Domain.LlmResponses;
using BoydCode.Infrastructure.LLM.Converters;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace BoydCode.Infrastructure.LLM.Tests;

public sealed class StreamingResponseConverterTests
{
  [Fact]
  public void TextUpdate_YieldsTextChunk()
  {
    // Arrange
    var converter = new StreamingResponseConverter();
    var update = new ChatResponseUpdate(ChatRole.Assistant, "hello");

    // Act
    var chunks = converter.ProcessUpdate(update).ToList();

    // Assert
    chunks.Should().ContainSingle()
        .Which.Should().BeOfType<TextChunk>()
        .Which.Text.Should().Be("hello");
  }

  [Fact]
  public void EmptyTextUpdate_YieldsNothing()
  {
    // Arrange
    var converter = new StreamingResponseConverter();
    var update = new ChatResponseUpdate(ChatRole.Assistant, "");

    // Act
    var chunks = converter.ProcessUpdate(update).ToList();

    // Assert
    chunks.Should().BeEmpty();
  }

  [Fact]
  public void NullTextUpdate_YieldsNothing()
  {
    // Arrange
    var converter = new StreamingResponseConverter();
    var update = new ChatResponseUpdate { Role = ChatRole.Assistant };

    // Act
    var chunks = converter.ProcessUpdate(update).ToList();

    // Assert
    chunks.Should().BeEmpty();
  }

  [Fact]
  public void Complete_AfterTextUpdates_YieldsCompletionChunk()
  {
    // Arrange -- ToChatResponse() requires at least one update in the list
    var converter = new StreamingResponseConverter();
    var update = new ChatResponseUpdate(ChatRole.Assistant, "some text");
    _ = converter.ProcessUpdate(update).ToList();

    // Act
    var chunks = converter.Complete().ToList();

    // Assert -- last chunk should be a CompletionChunk
    chunks.Should().NotBeEmpty();
    chunks.Last().Should().BeOfType<CompletionChunk>();
    var completion = (CompletionChunk)chunks.Last();
    // ToChatResponse() with no FinishReason set produces null, mapped to "unknown"
    completion.StopReason.Should().Be("unknown");
    completion.Usage.Should().NotBeNull();
  }
}
