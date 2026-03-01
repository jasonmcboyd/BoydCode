using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.LlmResponses;
using FluentAssertions;
using Xunit;

namespace BoydCode.Domain.Tests;

public sealed class StreamAccumulatorTests
{
  [Fact]
  public void TextOnly_ProducesTextBlock()
  {
    // Arrange
    var accumulator = new StreamAccumulator();
    accumulator.Process(new TextChunk("Hello, "));
    accumulator.Process(new TextChunk("world"));
    accumulator.Process(new TextChunk("!"));
    accumulator.Process(new CompletionChunk("end_turn", new TokenUsage(10, 5)));

    // Act
    var response = accumulator.ToResponse();

    // Assert
    response.Content.Should().HaveCount(1);
    response.Content[0].Should().BeOfType<TextBlock>()
        .Which.Text.Should().Be("Hello, world!");
  }

  [Fact]
  public void TextThenToolCall_ProducesTextBlockAndToolUseBlock()
  {
    // Arrange
    var accumulator = new StreamAccumulator();
    accumulator.Process(new TextChunk("Let me help with that."));
    accumulator.Process(new ToolCallChunk("call_1", "read_file", "{\"path\":\"/tmp/test.txt\"}"));
    accumulator.Process(new CompletionChunk("tool_use", new TokenUsage(20, 15)));

    // Act
    var response = accumulator.ToResponse();

    // Assert
    response.Content.Should().HaveCount(2);
    response.Content[0].Should().BeOfType<TextBlock>()
        .Which.Text.Should().Be("Let me help with that.");
    var toolUse = response.Content[1].Should().BeOfType<ToolUseBlock>().Subject;
    toolUse.Id.Should().Be("call_1");
    toolUse.Name.Should().Be("read_file");
    toolUse.ArgumentsJson.Should().Be("{\"path\":\"/tmp/test.txt\"}");
  }

  [Fact]
  public void MultipleToolCalls_AllAccumulated()
  {
    // Arrange
    var accumulator = new StreamAccumulator();
    accumulator.Process(new ToolCallChunk("call_1", "read_file", "{\"path\":\"a.txt\"}"));
    accumulator.Process(new ToolCallChunk("call_2", "write_file", "{\"path\":\"b.txt\",\"content\":\"hi\"}"));
    accumulator.Process(new CompletionChunk("tool_use", new TokenUsage(30, 20)));

    // Act
    var response = accumulator.ToResponse();

    // Assert
    response.Content.Should().HaveCount(2);
    response.Content.Should().AllBeOfType<ToolUseBlock>();
    var first = (ToolUseBlock)response.Content[0];
    first.Id.Should().Be("call_1");
    first.Name.Should().Be("read_file");
    var second = (ToolUseBlock)response.Content[1];
    second.Id.Should().Be("call_2");
    second.Name.Should().Be("write_file");
  }

  [Fact]
  public void EmptyStream_ProducesEmptyContentWithUsage()
  {
    // Arrange
    var accumulator = new StreamAccumulator();
    accumulator.Process(new CompletionChunk("end_turn", new TokenUsage(5, 0)));

    // Act
    var response = accumulator.ToResponse();

    // Assert
    response.Content.Should().BeEmpty();
    response.Usage.Should().Be(new TokenUsage(5, 0));
  }

  [Fact]
  public void CompletionChunk_CapturesStopReasonAndUsage()
  {
    // Arrange
    var accumulator = new StreamAccumulator();
    var expectedUsage = new TokenUsage(42, 17);
    accumulator.Process(new TextChunk("done"));
    accumulator.Process(new CompletionChunk("max_tokens", expectedUsage));

    // Act
    var response = accumulator.ToResponse();

    // Assert
    response.StopReason.Should().Be("max_tokens");
    response.Usage.Should().Be(expectedUsage);
    response.Usage.InputTokens.Should().Be(42);
    response.Usage.OutputTokens.Should().Be(17);
  }

  [Fact]
  public void ToResponse_WithNoCompletionChunk_UsesDefaults()
  {
    // Arrange
    var accumulator = new StreamAccumulator();
    accumulator.Process(new TextChunk("some text"));

    // Act
    var response = accumulator.ToResponse();

    // Assert
    response.StopReason.Should().Be("unknown");
    response.Usage.Should().Be(new TokenUsage(0, 0));
    response.Content.Should().HaveCount(1);
  }

  [Fact]
  public void ToolCallBetweenText_FlushesTextBeforeToolUse()
  {
    // Arrange -- text, tool, more text pattern
    var accumulator = new StreamAccumulator();
    accumulator.Process(new TextChunk("before "));
    accumulator.Process(new ToolCallChunk("call_1", "grep", "{\"pattern\":\"TODO\"}"));
    accumulator.Process(new TextChunk("after"));
    accumulator.Process(new CompletionChunk("end_turn", new TokenUsage(10, 10)));

    // Act
    var response = accumulator.ToResponse();

    // Assert -- should produce TextBlock, ToolUseBlock, TextBlock in order
    response.Content.Should().HaveCount(3);
    response.Content[0].Should().BeOfType<TextBlock>()
        .Which.Text.Should().Be("before ");
    response.Content[1].Should().BeOfType<ToolUseBlock>()
        .Which.Name.Should().Be("grep");
    response.Content[2].Should().BeOfType<TextBlock>()
        .Which.Text.Should().Be("after");
  }
}
