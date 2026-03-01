using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BoydCode.Domain.Tests;

public sealed class OAuthProviderRegistryTests
{
  [Fact]
  public void GetConfig_Anthropic_ReturnsAnthropicConfig()
  {
    // Arrange
    var provider = LlmProviderType.Anthropic;

    // Act
    var config = OAuthProviderRegistry.GetConfig(provider);

    // Assert
    config.Should().NotBeNull();
    config!.AuthorizationEndpoint.Should().Contain("claude.ai");
    config.TokenEndpoint.Should().Contain("anthropic.com");
    config.Scope.Should().Contain("user:inference");
    config.RequiresClientSecret.Should().BeFalse();
    config.ExtraAuthorizationParams.Should().BeNull();
  }

  [Fact]
  public void GetConfig_Gemini_ReturnsGeminiConfig()
  {
    // Arrange
    var provider = LlmProviderType.Gemini;

    // Act
    var config = OAuthProviderRegistry.GetConfig(provider);

    // Assert
    config.Should().NotBeNull();
    config!.AuthorizationEndpoint.Should().Contain("accounts.google.com");
    config.TokenEndpoint.Should().Contain("googleapis.com");
    config.Scope.Should().Contain("cloud-platform");
    config.ClientId.Should().BeEmpty();
    config.RequiresClientSecret.Should().BeTrue();
    config.ExtraAuthorizationParams.Should().NotBeNull();
    config.ExtraAuthorizationParams.Should().ContainKey("access_type").WhoseValue.Should().Be("offline");
    config.ExtraAuthorizationParams.Should().ContainKey("prompt").WhoseValue.Should().Be("consent");
  }

  [Fact]
  public void GetConfig_OpenAi_ReturnsNull()
  {
    // Arrange
    var provider = LlmProviderType.OpenAi;

    // Act
    var config = OAuthProviderRegistry.GetConfig(provider);

    // Assert
    config.Should().BeNull();
  }

  [Fact]
  public void GetConfig_Ollama_ReturnsNull()
  {
    // Arrange
    var provider = LlmProviderType.Ollama;

    // Act
    var config = OAuthProviderRegistry.GetConfig(provider);

    // Assert
    config.Should().BeNull();
  }
}
