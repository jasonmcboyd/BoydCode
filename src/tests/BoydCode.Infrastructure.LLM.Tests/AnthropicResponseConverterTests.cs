using Anthropic.Models.Messages;
using BoydCode.Domain.LlmResponses;
using BoydCode.Infrastructure.LLM.Converters;
using FluentAssertions;
using Xunit;

namespace BoydCode.Infrastructure.LLM.Tests;

public sealed class AnthropicResponseConverterTests
{
  /// <summary>
  /// Creates a <see cref="Usage"/> instance with all required members set.
  /// The Anthropic SDK marks several properties as required; this helper
  /// avoids repeating boilerplate across every test.
  /// </summary>
  private static Usage CreateUsage(
    long inputTokens = 0,
    long outputTokens = 0,
    long? cacheCreationInputTokens = null,
    long? cacheReadInputTokens = null) =>
    new()
    {
      InputTokens = inputTokens,
      OutputTokens = outputTokens,
      CacheCreationInputTokens = cacheCreationInputTokens,
      CacheReadInputTokens = cacheReadInputTokens,
      CacheCreation = null,
      InferenceGeo = null,
      ServerToolUse = null,
      ServiceTier = null,
    };

  // -------------------------------------------------------------------
  // MapStopReason
  // -------------------------------------------------------------------

  [Fact]
  public void MapStopReason_EndTurn_ReturnsEndTurn()
  {
    // Act
    var result = AnthropicResponseConverter.MapStopReason(StopReason.EndTurn);

    // Assert
    result.Should().Be("end_turn");
  }

  [Fact]
  public void MapStopReason_ToolUse_ReturnsToolUse()
  {
    // Act
    var result = AnthropicResponseConverter.MapStopReason(StopReason.ToolUse);

    // Assert
    result.Should().Be("tool_use");
  }

  [Fact]
  public void MapStopReason_MaxTokens_ReturnsMaxTokens()
  {
    // Act
    var result = AnthropicResponseConverter.MapStopReason(StopReason.MaxTokens);

    // Assert
    result.Should().Be("max_tokens");
  }

  [Fact]
  public void MapStopReason_StopSequence_ReturnsStopSequence()
  {
    // Act
    var result = AnthropicResponseConverter.MapStopReason(StopReason.StopSequence);

    // Assert
    result.Should().Be("stop_sequence");
  }

  [Fact]
  public void MapStopReason_PauseTurn_ReturnsPauseTurn()
  {
    // Act
    var result = AnthropicResponseConverter.MapStopReason(StopReason.PauseTurn);

    // Assert
    result.Should().Be("pause_turn");
  }

  [Fact]
  public void MapStopReason_Refusal_ReturnsRefusal()
  {
    // Act
    var result = AnthropicResponseConverter.MapStopReason(StopReason.Refusal);

    // Assert
    result.Should().Be("refusal");
  }

  // -------------------------------------------------------------------
  // MapUsage -- basic
  // -------------------------------------------------------------------

  [Fact]
  public void MapUsage_NullUsage_ReturnsZeroTokens()
  {
    // Act
    var result = AnthropicResponseConverter.MapUsage(null);

    // Assert
    result.InputTokens.Should().Be(0);
    result.OutputTokens.Should().Be(0);
  }

  [Fact]
  public void MapUsage_WithInputAndOutputTokens_MapsCorrectly()
  {
    // Arrange
    var usage = CreateUsage(inputTokens: 100, outputTokens: 50);

    // Act
    var result = AnthropicResponseConverter.MapUsage(usage);

    // Assert
    result.InputTokens.Should().Be(100);
    result.OutputTokens.Should().Be(50);
  }

  // -------------------------------------------------------------------
  // MapUsage -- cache token aggregation
  // -------------------------------------------------------------------

  [Fact]
  public void MapUsage_WithCacheCreationTokens_SumsIntoInputTokens()
  {
    // Arrange
    var usage = CreateUsage(
      inputTokens: 100,
      outputTokens: 50,
      cacheCreationInputTokens: 20);

    // Act
    var result = AnthropicResponseConverter.MapUsage(usage);

    // Assert -- InputTokens should be 100 + 20 = 120
    result.InputTokens.Should().Be(120);
    result.OutputTokens.Should().Be(50);
  }

  [Fact]
  public void MapUsage_WithCacheReadTokens_SumsIntoInputTokens()
  {
    // Arrange
    var usage = CreateUsage(
      inputTokens: 100,
      outputTokens: 50,
      cacheReadInputTokens: 30);

    // Act
    var result = AnthropicResponseConverter.MapUsage(usage);

    // Assert -- InputTokens should be 100 + 30 = 130
    result.InputTokens.Should().Be(130);
    result.OutputTokens.Should().Be(50);
  }

  [Fact]
  public void MapUsage_WithBothCacheTokenTypes_SumsAllIntoInputTokens()
  {
    // Arrange
    var usage = CreateUsage(
      inputTokens: 100,
      outputTokens: 50,
      cacheCreationInputTokens: 20,
      cacheReadInputTokens: 30);

    // Act
    var result = AnthropicResponseConverter.MapUsage(usage);

    // Assert -- InputTokens should be 100 + 20 + 30 = 150
    result.InputTokens.Should().Be(150);
    result.OutputTokens.Should().Be(50);
  }

  [Fact]
  public void MapUsage_WithNullCacheTokens_TreatsAsZero()
  {
    // Arrange
    var usage = CreateUsage(
      inputTokens: 200,
      outputTokens: 75,
      cacheCreationInputTokens: null,
      cacheReadInputTokens: null);

    // Act
    var result = AnthropicResponseConverter.MapUsage(usage);

    // Assert
    result.InputTokens.Should().Be(200);
    result.OutputTokens.Should().Be(75);
  }

  // -------------------------------------------------------------------
  // MapUsage -- TokenUsage record
  // -------------------------------------------------------------------

  [Fact]
  public void MapUsage_ReturnedTokenUsage_HasCorrectTotalTokens()
  {
    // Arrange
    var usage = CreateUsage(
      inputTokens: 100,
      outputTokens: 50,
      cacheCreationInputTokens: 10,
      cacheReadInputTokens: 5);

    // Act
    var result = AnthropicResponseConverter.MapUsage(usage);

    // Assert -- TotalTokens = (100 + 10 + 5) + 50 = 165
    result.TotalTokens.Should().Be(165);
  }

  // -------------------------------------------------------------------
  // ToDomain -- null guard
  // -------------------------------------------------------------------

  [Fact]
  public void ToDomain_NullMessage_ThrowsArgumentNullException()
  {
    // Act
    var act = () => AnthropicResponseConverter.ToDomain(null!);

    // Assert
    act.Should().Throw<ArgumentNullException>();
  }
}
