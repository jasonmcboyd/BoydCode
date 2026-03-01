using System.Reflection;
using BoydCode.Application.Services;
using FluentAssertions;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class AgentOrchestratorErrorFormattingTests
{
  private static readonly MethodInfo FormatProviderErrorMethod =
      typeof(AgentOrchestrator).GetMethod("FormatProviderError", BindingFlags.NonPublic | BindingFlags.Static)!;

  private static string InvokeFormatProviderError(Exception ex) =>
      (string)FormatProviderErrorMethod.Invoke(null, [ex])!;

  [Fact]
  public void FormatProviderError_AnthropicJsonBody_ExtractsMessage()
  {
    // Arrange
    var ex = new InvalidOperationException(
        "Status Code: BadRequest\n" +
        "{\"type\":\"error\",\"error\":{\"type\":\"invalid_request_error\"," +
        "\"message\":\"Your credit balance is too low to access the Anthropic API. " +
        "Please go to Plans & Billing to upgrade or purchase credits.\"},\"request_id\":\"req_123\"}");

    // Act
    var result = InvokeFormatProviderError(ex);

    // Assert
    result.Should().Be(
        "Your credit balance is too low to access the Anthropic API. " +
        "Please go to Plans & Billing to upgrade or purchase credits.");
  }

  [Fact]
  public void FormatProviderError_FlatJsonMessage_ExtractsMessage()
  {
    // Arrange
    var ex = new InvalidOperationException(
        "Something failed: {\"message\":\"Rate limit exceeded\",\"retry_after\":30}");

    // Act
    var result = InvokeFormatProviderError(ex);

    // Assert
    result.Should().Be("Rate limit exceeded");
  }

  [Fact]
  public void FormatProviderError_NoJson_ReturnsOriginalMessageWithConnectionSuggestion()
  {
    // Arrange -- "Connection" triggers the network connectivity suggestion
    var ex = new InvalidOperationException("Connection refused");

    // Act
    var result = InvokeFormatProviderError(ex);

    // Assert
    result.Should().StartWith("Connection refused");
    result.Should().Contain("Suggestion:");
    result.Should().Contain("internet connection");
  }

  [Fact]
  public void FormatProviderError_InnerException_ReturnsInnerMessageWithNetworkSuggestion()
  {
    // Arrange -- HttpRequestException triggers the network connectivity suggestion
    var inner = new InvalidOperationException("The actual problem");
    var outer = new HttpRequestException("See inner exception", inner);

    // Act
    var result = InvokeFormatProviderError(outer);

    // Assert
    result.Should().StartWith("The actual problem");
    result.Should().Contain("Suggestion:");
    result.Should().Contain("internet connection");
  }

  [Fact]
  public void FormatProviderError_InvalidJson_ReturnsOriginalMessage()
  {
    // Arrange
    var ex = new InvalidOperationException("Error: {not valid json");

    // Act
    var result = InvokeFormatProviderError(ex);

    // Assert
    result.Should().Be("Error: {not valid json");
  }

  [Fact]
  public void FormatProviderError_GoogleStyleError_ExtractsMessage()
  {
    // Arrange -- Google-style JSON has "error" with "status" and "message" siblings
    var ex = new InvalidOperationException(
        "Request failed: " +
        "{\"error\":{\"status\":\"PERMISSION_DENIED\",\"message\":\"API key expired\"}}");

    // Act
    var result = InvokeFormatProviderError(ex);

    // Assert
    result.Should().Be("API key expired");
  }

  [Fact]
  public void FormatProviderError_InnerExceptionSameMessage_ReturnsOriginalMessageWithSuggestion()
  {
    // Arrange -- When inner exception has the same message, the method should
    // fall through and keep the original message. HttpRequestException + "Connection"
    // both trigger the network connectivity suggestion.
    var inner = new InvalidOperationException("Connection refused");
    var outer = new HttpRequestException("Connection refused", inner);

    // Act
    var result = InvokeFormatProviderError(outer);

    // Assert
    result.Should().StartWith("Connection refused");
    result.Should().Contain("Suggestion:");
    result.Should().Contain("internet connection");
  }

  [Fact]
  public void FormatProviderError_JsonWithNullMessage_ReturnsOriginalMessage()
  {
    // Arrange -- The "message" property is present but null
    var ex = new InvalidOperationException(
        "Error: {\"error\":{\"type\":\"server_error\",\"message\":null}}");

    // Act
    var result = InvokeFormatProviderError(ex);

    // Assert
    result.Should().Be(
        "Error: {\"error\":{\"type\":\"server_error\",\"message\":null}}");
  }

  [Fact]
  public void FormatProviderError_AuthError_IncludesProviderSuggestion()
  {
    // Arrange
    var ex = new InvalidOperationException("Unauthorized: invalid API key");

    // Act
    var result = InvokeFormatProviderError(ex);

    // Assert
    result.Should().Contain("/provider");
  }

  [Fact]
  public void FormatProviderError_RateLimitError_IncludesWaitSuggestion()
  {
    // Arrange
    var ex = new InvalidOperationException("Rate limit exceeded, please try again later");

    // Act
    var result = InvokeFormatProviderError(ex);

    // Assert
    result.Should().Contain("Wait");
  }

  [Fact]
  public void FormatProviderError_UnknownError_HasNoSuggestion()
  {
    // Arrange
    var ex = new InvalidOperationException("Something completely unknown happened");

    // Act
    var result = InvokeFormatProviderError(ex);

    // Assert
    result.Should().NotContain("Suggestion:");
  }
}
