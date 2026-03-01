using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Tools;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class PermissionEngineTests
{
  [Fact]
  public void Evaluate_WhenBypassPermissionsMode_AlwaysReturnsAllow()
  {
    // Arrange
    var settings = new AppSettings { PermissionMode = PermissionMode.BypassPermissions };
    var options = Substitute.For<IOptions<AppSettings>>();
    options.Value.Returns(settings);

    var engine = new PermissionEngine(options);

    var tool = new ToolDefinition(
        "SomeDangerousTool",
        "A tool that would normally require approval",
        ToolCategory.Shell,
        []);

    // Act
    var result = engine.Evaluate(tool, "{}");

    // Assert
    result.Should().Be(PermissionLevel.Allow);
  }
}
