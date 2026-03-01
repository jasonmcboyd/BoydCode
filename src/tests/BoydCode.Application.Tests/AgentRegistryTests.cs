using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class AgentRegistryTests
{
  private readonly IAgentDefinitionStore _store = Substitute.For<IAgentDefinitionStore>();

  private AgentRegistry CreateSut() => new(_store);

  [Fact]
  public async Task InitializeAsync_LoadsFromStore()
  {
    // Arrange
    var agents = new List<AgentDefinition>
    {
      CreateAgent("code-reviewer", AgentScope.User),
      CreateAgent("bug-hunter", AgentScope.User),
    };
    _store.LoadAllAsync(null, Arg.Any<CancellationToken>())
        .Returns(agents.AsReadOnly());
    var sut = CreateSut();

    // Act
    await sut.InitializeAsync();

    // Assert
    sut.GetAll().Should().HaveCount(2);
    sut.GetByName("code-reviewer").Should().NotBeNull();
    sut.GetByName("bug-hunter").Should().NotBeNull();
  }

  [Fact]
  public async Task GetByName_CaseInsensitive_FindsAgent()
  {
    // Arrange
    var agents = new List<AgentDefinition>
    {
      CreateAgent("code-reviewer", AgentScope.User),
    };
    _store.LoadAllAsync(null, Arg.Any<CancellationToken>())
        .Returns(agents.AsReadOnly());
    var sut = CreateSut();
    await sut.InitializeAsync();

    // Act
    var result = sut.GetByName("Code-Reviewer");

    // Assert
    result.Should().NotBeNull();
    result!.Name.Should().Be("code-reviewer");
  }

  [Fact]
  public async Task GetByName_UnknownName_ReturnsNull()
  {
    // Arrange
    var agents = new List<AgentDefinition>
    {
      CreateAgent("code-reviewer", AgentScope.User),
    };
    _store.LoadAllAsync(null, Arg.Any<CancellationToken>())
        .Returns(agents.AsReadOnly());
    var sut = CreateSut();
    await sut.InitializeAsync();

    // Act
    var result = sut.GetByName("nonexistent-agent");

    // Assert
    result.Should().BeNull();
  }

  [Fact]
  public async Task GetAll_ReturnsAllAgents()
  {
    // Arrange
    var agents = new List<AgentDefinition>
    {
      CreateAgent("agent-a", AgentScope.User),
      CreateAgent("agent-b", AgentScope.User),
      CreateAgent("agent-c", AgentScope.Project),
    };
    _store.LoadAllAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
        .Returns(agents.AsReadOnly());
    var sut = CreateSut();
    await sut.InitializeAsync();

    // Act
    var all = sut.GetAll();

    // Assert
    all.Should().HaveCount(3);
    all.Select(a => a.Name).Should().Contain(["agent-a", "agent-b", "agent-c"]);
  }

  [Fact]
  public async Task ProjectScope_OverridesUserScope()
  {
    // Arrange — store returns user-scoped first, then project-scoped with the same name.
    // AgentRegistry iterates in order and later entries override earlier ones by name.
    var agents = new List<AgentDefinition>
    {
      CreateAgent("foo", AgentScope.User, description: "user version"),
      CreateAgent("foo", AgentScope.Project, description: "project version"),
    };
    _store.LoadAllAsync("/project/dir", Arg.Any<CancellationToken>())
        .Returns(agents.AsReadOnly());
    var sut = CreateSut();
    await sut.InitializeAsync("/project/dir");

    // Act
    var result = sut.GetByName("foo");

    // Assert
    result.Should().NotBeNull();
    result!.Scope.Should().Be(AgentScope.Project);
    result.Description.Should().Be("project version");
  }

  [Fact]
  public async Task InitializeAsync_ClearsPreviousAgents()
  {
    // Arrange — initialize once with agents, then reinitialize with empty list
    var agents = new List<AgentDefinition>
    {
      CreateAgent("old-agent", AgentScope.User),
    };
    _store.LoadAllAsync(null, Arg.Any<CancellationToken>())
        .Returns(agents.AsReadOnly());
    var sut = CreateSut();
    await sut.InitializeAsync();
    sut.GetAll().Should().HaveCount(1);

    // Reinitialize with no agents
    _store.LoadAllAsync(null, Arg.Any<CancellationToken>())
        .Returns(new List<AgentDefinition>().AsReadOnly());

    // Act
    await sut.InitializeAsync();

    // Assert
    sut.GetAll().Should().BeEmpty();
    sut.GetByName("old-agent").Should().BeNull();
  }

  [Fact]
  public async Task InitializeAsync_PassesProjectDirectoryToStore()
  {
    // Arrange
    _store.LoadAllAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
        .Returns(new List<AgentDefinition>().AsReadOnly());
    var sut = CreateSut();

    // Act
    await sut.InitializeAsync("/my/project");

    // Assert
    await _store.Received(1).LoadAllAsync("/my/project", Arg.Any<CancellationToken>());
  }

  private static AgentDefinition CreateAgent(
      string name, AgentScope scope, string description = "A test agent") =>
      new()
      {
        Name = name,
        Description = description,
        Instructions = $"Instructions for {name}",
        Scope = scope,
      };
}
