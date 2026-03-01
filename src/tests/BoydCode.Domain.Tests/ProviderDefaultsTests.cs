using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BoydCode.Domain.Tests;

public sealed class ProviderDefaultsTests
{
  [Fact]
  public void For_Anthropic_ReturnsExpectedCapabilities()
  {
    // Arrange
    var provider = LlmProviderType.Anthropic;

    // Act
    var capabilities = ProviderDefaults.For(provider);

    // Assert
    capabilities.SupportsExplicitCaching.Should().BeFalse();
    capabilities.SupportsOAuthSubscription.Should().BeTrue();
    capabilities.MaxContextWindowTokens.Should().Be(200_000);
    capabilities.SupportsExtendedThinking.Should().BeTrue();
    capabilities.SupportsStreaming.Should().BeTrue();
    capabilities.SupportsToolUse.Should().BeTrue();
    capabilities.SupportsImageInput.Should().BeTrue();
  }

  [Fact]
  public void For_Gemini_ReturnsExpectedCapabilities()
  {
    // Arrange
    var provider = LlmProviderType.Gemini;

    // Act
    var capabilities = ProviderDefaults.For(provider);

    // Assert
    capabilities.SupportsExplicitCaching.Should().BeTrue();
    capabilities.SupportsOAuthSubscription.Should().BeTrue();
    capabilities.MaxContextWindowTokens.Should().Be(1_000_000);
    capabilities.SupportsExtendedThinking.Should().BeTrue();
  }

  [Fact]
  public void For_OpenAi_ReturnsExpectedCapabilities()
  {
    // Arrange
    var provider = LlmProviderType.OpenAi;

    // Act
    var capabilities = ProviderDefaults.For(provider);

    // Assert
    capabilities.MaxContextWindowTokens.Should().Be(128_000);
    capabilities.SupportsExtendedThinking.Should().BeFalse();
  }

  [Fact]
  public void For_Ollama_ReturnsExpectedCapabilities()
  {
    // Arrange
    var provider = LlmProviderType.Ollama;

    // Act
    var capabilities = ProviderDefaults.For(provider);

    // Assert
    capabilities.MaxContextWindowTokens.Should().Be(32_000);
    capabilities.SupportsImageInput.Should().BeFalse();
  }

  [Theory]
  [InlineData(LlmProviderType.Anthropic, "claude-sonnet-4-20250514")]
  [InlineData(LlmProviderType.Gemini, "gemini-2.5-pro")]
  [InlineData(LlmProviderType.OpenAi, "gpt-4o")]
  [InlineData(LlmProviderType.Ollama, "llama3")]
  public void DefaultModelFor_ReturnsExpectedModel(LlmProviderType provider, string expectedModel)
  {
    // Act
    var model = ProviderDefaults.DefaultModelFor(provider);

    // Assert
    model.Should().Be(expectedModel);
  }

  [Fact]
  public void DefaultModelFor_InvalidProvider_ThrowsArgumentOutOfRangeException()
  {
    // Act
    var act = () => ProviderDefaults.DefaultModelFor((LlmProviderType)999);

    // Assert
    act.Should().Throw<ArgumentOutOfRangeException>();
  }
}
