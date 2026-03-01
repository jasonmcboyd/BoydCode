using BoydCode.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BoydCode.Domain.Tests;

public sealed class SessionTests
{
  [Fact]
  public void Constructor_SetsWorkingDirectory()
  {
    // Arrange & Act
    var session = new Session("/home/user/project");

    // Assert
    session.WorkingDirectory.Should().Be("/home/user/project");
  }

  [Fact]
  public void Constructor_GeneratesNonEmptyId()
  {
    // Arrange & Act
    var session = new Session(".");

    // Assert
    session.Id.Should().NotBeNullOrEmpty();
    session.Id.Should().HaveLength(12);
  }

  [Fact]
  public void Constructor_InitializesConversation()
  {
    // Arrange & Act
    var session = new Session(".");

    // Assert
    session.Conversation.Should().NotBeNull();
    session.Conversation.Messages.Should().BeEmpty();
  }

  [Fact]
  public void Constructor_SetsCreatedAtAndLastAccessedAt()
  {
    // Arrange
    var before = DateTimeOffset.UtcNow;

    // Act
    var session = new Session(".");

    // Assert
    var after = DateTimeOffset.UtcNow;
    session.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    session.LastAccessedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
  }

  // ---------------------------------------------------------------------------
  // Name property
  // ---------------------------------------------------------------------------

  [Fact]
  public void Name_DefaultsToNull()
  {
    // Arrange & Act
    var session = new Session(".");

    // Assert
    session.Name.Should().BeNull();
  }

  [Fact]
  public void Name_CanBeSet()
  {
    // Arrange
    var session = new Session(".");

    // Act
    session.Name = "My Session";

    // Assert
    session.Name.Should().Be("My Session");
  }

  [Fact]
  public void Name_CanBeSetToNull()
  {
    // Arrange
    var session = new Session(".");
    session.Name = "Some Name";

    // Act
    session.Name = null;

    // Assert
    session.Name.Should().BeNull();
  }

  // ---------------------------------------------------------------------------
  // Other settable properties
  // ---------------------------------------------------------------------------

  [Fact]
  public void ProjectName_DefaultsToNull()
  {
    // Arrange & Act
    var session = new Session(".");

    // Assert
    session.ProjectName.Should().BeNull();
  }

  [Fact]
  public void ProjectName_CanBeSet()
  {
    // Arrange
    var session = new Session(".");

    // Act
    session.ProjectName = "my-project";

    // Assert
    session.ProjectName.Should().Be("my-project");
  }

  [Fact]
  public void SystemPrompt_DefaultsToNull()
  {
    // Arrange & Act
    var session = new Session(".");

    // Assert
    session.SystemPrompt.Should().BeNull();
  }

  [Fact]
  public void SystemPrompt_CanBeSet()
  {
    // Arrange
    var session = new Session(".");

    // Act
    session.SystemPrompt = "You are a helpful assistant.";

    // Assert
    session.SystemPrompt.Should().Be("You are a helpful assistant.");
  }

  // ---------------------------------------------------------------------------
  // Deserialization constructor
  // ---------------------------------------------------------------------------

  [Fact]
  public void DeserializationConstructor_SetsAllProperties()
  {
    // Arrange
    var id = "abc123def456";
    var workingDir = "/some/path";
    var conversation = new Conversation();
    conversation.AddUserMessage("test");
    var createdAt = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

    // Act
    var session = new Session(id, workingDir, conversation, createdAt);

    // Assert
    session.Id.Should().Be(id);
    session.WorkingDirectory.Should().Be(workingDir);
    session.Conversation.Should().BeSameAs(conversation);
    session.CreatedAt.Should().Be(createdAt);
    session.Conversation.Messages.Should().HaveCount(1);
  }
}
