using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BoydCode.Domain.Tests.Entities;

public sealed class AgentDefinitionTests
{
  [Fact]
  public void Construction_WithRequiredFields_SetsProperties()
  {
    // Arrange & Act
    var agent = new AgentDefinition
    {
      Name = "test-agent",
      Description = "A test agent",
      Instructions = "Do testing things.",
    };

    // Assert
    agent.Name.Should().Be("test-agent");
    agent.Description.Should().Be("A test agent");
    agent.Instructions.Should().Be("Do testing things.");
  }

  [Fact]
  public void Construction_DefaultValues_AreCorrect()
  {
    // Arrange & Act
    var agent = new AgentDefinition
    {
      Name = "minimal",
      Description = "",
      Instructions = "Some instructions.",
    };

    // Assert
    agent.Scope.Should().Be(AgentScope.User);
    agent.ModelOverride.Should().BeNull();
    agent.MaxTurns.Should().BeNull();
    agent.SourcePath.Should().Be("");
  }

  [Fact]
  public void Construction_AllFieldsPopulated_SetsAllProperties()
  {
    // Arrange & Act
    var agent = new AgentDefinition
    {
      Name = "code-reviewer",
      Description = "Reviews code for quality",
      Instructions = "Review the code carefully.",
      Scope = AgentScope.Project,
      ModelOverride = "claude-opus-4-20250514",
      MaxTurns = 10,
      SourcePath = "/home/user/.boydcode/agents/code-reviewer.md",
    };

    // Assert
    agent.Name.Should().Be("code-reviewer");
    agent.Description.Should().Be("Reviews code for quality");
    agent.Instructions.Should().Be("Review the code carefully.");
    agent.Scope.Should().Be(AgentScope.Project);
    agent.ModelOverride.Should().Be("claude-opus-4-20250514");
    agent.MaxTurns.Should().Be(10);
    agent.SourcePath.Should().Be("/home/user/.boydcode/agents/code-reviewer.md");
  }
}
