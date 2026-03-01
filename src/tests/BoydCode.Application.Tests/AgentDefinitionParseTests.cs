using BoydCode.Domain.Enums;
using BoydCode.Infrastructure.Persistence.Agents;
using FluentAssertions;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class AgentDefinitionParseTests
{
  // ---------------------------------------------------------------------------
  // Full frontmatter
  // ---------------------------------------------------------------------------

  [Fact]
  public void Parse_FullFrontmatter_MapsAllFields()
  {
    // Arrange
    var content = """
        ---
        description: Reviews code for quality and correctness
        model: claude-opus-4-20250514
        max_turns: 15
        ---
        You are a senior code reviewer.
        Review the code for bugs, style, and correctness.
        """;

    // Act
    var agent = FileAgentDefinitionStore.Parse("code-reviewer", content, AgentScope.User, "/agents/code-reviewer.md");

    // Assert
    agent.Name.Should().Be("code-reviewer");
    agent.Description.Should().Be("Reviews code for quality and correctness");
    agent.ModelOverride.Should().Be("claude-opus-4-20250514");
    agent.MaxTurns.Should().Be(15);
    agent.Scope.Should().Be(AgentScope.User);
    agent.SourcePath.Should().Be("/agents/code-reviewer.md");
    agent.Instructions.Should().Contain("senior code reviewer");
    agent.Instructions.Should().Contain("bugs, style, and correctness");
  }

  // ---------------------------------------------------------------------------
  // Missing frontmatter
  // ---------------------------------------------------------------------------

  [Fact]
  public void Parse_NoFrontmatter_EntireContentIsInstructions()
  {
    // Arrange
    var content = """
        You are a helpful agent.
        Do the task the user asks for.
        """;

    // Act
    var agent = FileAgentDefinitionStore.Parse("simple-agent", content, AgentScope.Project);

    // Assert
    agent.Name.Should().Be("simple-agent");
    agent.Description.Should().Be("");
    agent.ModelOverride.Should().BeNull();
    agent.MaxTurns.Should().BeNull();
    agent.Scope.Should().Be(AgentScope.Project);
    agent.SourcePath.Should().Be("");
    agent.Instructions.Should().Contain("helpful agent");
    agent.Instructions.Should().Contain("task the user asks for");
  }

  // ---------------------------------------------------------------------------
  // Partial frontmatter
  // ---------------------------------------------------------------------------

  [Fact]
  public void Parse_PartialFrontmatter_OnlyDescriptionSet()
  {
    // Arrange
    var content = """
        ---
        description: Hunts bugs in code
        ---
        Find and fix bugs.
        """;

    // Act
    var agent = FileAgentDefinitionStore.Parse("bug-hunter", content, AgentScope.User);

    // Assert
    agent.Description.Should().Be("Hunts bugs in code");
    agent.ModelOverride.Should().BeNull();
    agent.MaxTurns.Should().BeNull();
    agent.Instructions.Should().Contain("Find and fix bugs");
  }

  // ---------------------------------------------------------------------------
  // Invalid max_turns
  // ---------------------------------------------------------------------------

  [Fact]
  public void Parse_InvalidMaxTurns_MaxTurnsIsNull()
  {
    // Arrange
    var content = """
        ---
        description: Agent with bad max_turns
        max_turns: not-a-number
        ---
        Do things.
        """;

    // Act
    var agent = FileAgentDefinitionStore.Parse("bad-turns", content, AgentScope.User);

    // Assert
    agent.MaxTurns.Should().BeNull();
    agent.Description.Should().Be("Agent with bad max_turns");
  }

  // ---------------------------------------------------------------------------
  // Empty body after frontmatter
  // ---------------------------------------------------------------------------

  [Fact]
  public void Parse_EmptyBodyAfterFrontmatter_InstructionsEmpty()
  {
    // Arrange
    var content = "---\ndescription: No body\n---\n";

    // Act
    var agent = FileAgentDefinitionStore.Parse("no-body", content, AgentScope.User);

    // Assert
    agent.Description.Should().Be("No body");
    agent.Instructions.Should().Be("");
  }

  // ---------------------------------------------------------------------------
  // Unclosed frontmatter
  // ---------------------------------------------------------------------------

  [Fact]
  public void Parse_UnclosedFrontmatter_TreatedAsNoFrontmatter()
  {
    // Arrange — single --- but no closing ---, so the entire content becomes instructions
    var content = "---\ndescription: This is not real frontmatter\nSome instructions here.";

    // Act
    var agent = FileAgentDefinitionStore.Parse("unclosed", content, AgentScope.User);

    // Assert — because frontmatter was not closed, lineIndex resets to 0
    // and all partially parsed metadata is discarded.
    agent.Description.Should().Be("");
    agent.ModelOverride.Should().BeNull();
    agent.MaxTurns.Should().BeNull();
    agent.Instructions.Should().Contain("---");
    agent.Instructions.Should().Contain("description: This is not real frontmatter");
    agent.Instructions.Should().Contain("Some instructions here.");
  }

  // ---------------------------------------------------------------------------
  // Edge cases
  // ---------------------------------------------------------------------------

  [Fact]
  public void Parse_EmptyModelValue_ModelOverrideIsNull()
  {
    // Arrange
    var content = "---\nmodel: \n---\nInstructions here.";

    // Act
    var agent = FileAgentDefinitionStore.Parse("empty-model", content, AgentScope.User);

    // Assert
    agent.ModelOverride.Should().BeNull();
  }

  [Fact]
  public void Parse_FrontmatterKeysCaseInsensitive()
  {
    // Arrange — keys use mixed case
    var content = "---\nDescription: Mixed case description\nModel: some-model\nMax_Turns: 42\n---\nBody.";

    // Act
    var agent = FileAgentDefinitionStore.Parse("case-test", content, AgentScope.User);

    // Assert
    agent.Description.Should().Be("Mixed case description");
    agent.ModelOverride.Should().Be("some-model");
    agent.MaxTurns.Should().Be(42);
  }

  [Fact]
  public void Parse_EmptyContent_InstructionsEmpty()
  {
    // Arrange
    var content = "";

    // Act
    var agent = FileAgentDefinitionStore.Parse("empty", content, AgentScope.User);

    // Assert
    agent.Name.Should().Be("empty");
    agent.Description.Should().Be("");
    agent.Instructions.Should().Be("");
  }

  [Fact]
  public void Parse_UnknownFrontmatterKeys_Ignored()
  {
    // Arrange
    var content = "---\ndescription: Real field\nauthor: John\nversion: 1.0\n---\nInstructions.";

    // Act
    var agent = FileAgentDefinitionStore.Parse("unknown-keys", content, AgentScope.User);

    // Assert
    agent.Description.Should().Be("Real field");
    agent.Instructions.Should().Be("Instructions.");
  }
}
