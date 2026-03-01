using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.Tools;
using FluentAssertions;
using Xunit;

namespace BoydCode.Domain.Tests;

public sealed class LlmRequestTests
{
  [Fact]
  public void Defaults_AreCorrect()
  {
    // Arrange & Act
    var request = new LlmRequest { Model = "test-model" };

    // Assert
    request.ToolChoice.Should().Be(ToolChoiceStrategy.Auto);
    request.Tools.Should().BeEmpty();
    request.Directories.Should().BeEmpty();
    request.Messages.Should().BeEmpty();
    request.Stream.Should().BeFalse();
    request.SystemPrompt.Should().BeNull();
    request.Sampling.Should().BeNull();
    request.Thinking.Should().BeNull();
    request.Metadata.Should().BeNull();
  }

  [Fact]
  public void WithExpression_ChangingMessages_PreservesTier1Fields()
  {
    // Arrange
    var tools = new List<ToolDefinition>
        {
            new("read_file", "Reads a file", []),
        };
    var directories = new List<ResolvedDirectory>
        {
            new("/src", DirectoryAccessLevel.ReadWrite, Exists: true, IsGitRepository: true, GitBranch: "main"),
        };
    var original = new LlmRequest
    {
      Model = "gemini-2.5-pro",
      SystemPrompt = "You are a helpful assistant.",
      Tools = tools,
      ToolChoice = ToolChoiceStrategy.Any,
      Directories = directories,
    };

    var newMessages = new List<ConversationMessage>
        {
            new(MessageRole.User, "Hello!"),
        };

    // Act
    var updated = original with { Messages = newMessages };

    // Assert -- tier-1 fields are preserved
    updated.Model.Should().Be("gemini-2.5-pro");
    updated.SystemPrompt.Should().Be("You are a helpful assistant.");
    updated.Tools.Should().BeSameAs(tools);
    updated.ToolChoice.Should().Be(ToolChoiceStrategy.Any);
    updated.Directories.Should().BeSameAs(directories);

    // Assert -- messages are changed
    updated.Messages.Should().HaveCount(1);
    updated.Messages[0].Role.Should().Be(MessageRole.User);
  }

  [Fact]
  public void WithExpression_ChangingStream_PreservesAllOtherFields()
  {
    // Arrange
    var tools = new List<ToolDefinition>
        {
            new("grep", "Searches files", []),
        };
    var directories = new List<ResolvedDirectory>
        {
            new("/project", DirectoryAccessLevel.ReadOnly, Exists: true, IsGitRepository: false),
        };
    var messages = new List<ConversationMessage>
        {
            new(MessageRole.User, "Find TODOs"),
            new(MessageRole.Assistant, "Searching..."),
        };
    var sampling = new SamplingOptions { Temperature = 0.7f, MaxOutputTokens = 4096 };
    var thinking = new ThinkingConfig { Enabled = true, BudgetTokens = 1024 };
    var metadata = new RequestMetadata { UserId = "user-42" };

    var original = new LlmRequest
    {
      Model = "claude-sonnet-4-20250514",
      SystemPrompt = "You are a code reviewer.",
      Tools = tools,
      ToolChoice = ToolChoiceStrategy.None,
      Directories = directories,
      Sampling = sampling,
      Thinking = thinking,
      Metadata = metadata,
      Messages = messages,
      Stream = false,
    };

    // Act
    var updated = original with { Stream = true };

    // Assert -- every field except Stream is preserved
    updated.Model.Should().Be("claude-sonnet-4-20250514");
    updated.SystemPrompt.Should().Be("You are a code reviewer.");
    updated.Tools.Should().BeSameAs(tools);
    updated.ToolChoice.Should().Be(ToolChoiceStrategy.None);
    updated.Directories.Should().BeSameAs(directories);
    updated.Sampling.Should().Be(sampling);
    updated.Thinking.Should().Be(thinking);
    updated.Metadata.Should().Be(metadata);
    updated.Messages.Should().BeSameAs(messages);

    // Assert -- Stream is changed
    updated.Stream.Should().BeTrue();
  }
}
