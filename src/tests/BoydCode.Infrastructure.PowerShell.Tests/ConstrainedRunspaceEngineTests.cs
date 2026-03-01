using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BoydCode.Infrastructure.PowerShell.Tests;

public sealed class ConstrainedRunspaceEngineTests : IAsyncLifetime
{
  private readonly ConstrainedRunspaceEngine _engine;

  public ConstrainedRunspaceEngineTests()
  {
    var config = new ExecutionConfig();
    var options = Substitute.For<IOptions<ExecutionConfig>>();
    options.Value.Returns(config);

    var store = Substitute.For<IJeaProfileStore>();
    var composer = new JeaProfileComposer(store, NullLogger<JeaProfileComposer>.Instance);

    _engine = new ConstrainedRunspaceEngine(options, composer, NullLogger<ConstrainedRunspaceEngine>.Instance);
  }

  public async Task InitializeAsync()
  {
    await _engine.InitializeAsync();
  }

  public async Task DisposeAsync()
  {
    await _engine.DisposeAsync();
  }

  [Fact]
  public void Initialize_ShouldPopulateAvailableCommands()
  {
    // Assert -- InitializeAsync was called in InitializeAsync above
    var commands = _engine.GetAvailableCommands();
    commands.Should().NotBeEmpty();
  }
}
