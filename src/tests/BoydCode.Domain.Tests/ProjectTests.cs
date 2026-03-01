using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BoydCode.Domain.Tests;

public sealed class ProjectTests
{
  [Fact]
  public void BuildExecutionConfig_RequireContainer_True_Sets_ContainerMode()
  {
    // Arrange
    var project = new Project("test-project")
    {
      RequireContainer = true,
    };

    // Act
    var config = project.BuildExecutionConfig();

    // Assert
    config.Mode.Should().Be(ExecutionMode.Container);
    config.AllowInProcess.Should().BeFalse();
  }

  [Fact]
  public void BuildExecutionConfig_RequireContainer_False_Sets_InProcessMode()
  {
    // Arrange
    var project = new Project("test-project")
    {
      RequireContainer = false,
    };

    // Act
    var config = project.BuildExecutionConfig();

    // Assert
    config.Mode.Should().Be(ExecutionMode.InProcess);
    config.AllowInProcess.Should().BeTrue();
  }

  [Fact]
  public void BuildExecutionConfig_Preserves_JeaProfiles()
  {
    // Arrange
    var profiles = new List<string> { "_global", "custom-profile" };
    var project = new Project("test-project")
    {
      Execution = new ExecutionConfig
      {
        JeaProfiles = profiles,
      },
    };

    // Act
    var config = project.BuildExecutionConfig();

    // Assert
    config.JeaProfiles.Should().BeEquivalentTo(profiles);
    config.JeaProfiles.Should().NotBeSameAs(profiles, "profiles should be copied, not referenced");
  }

  [Fact]
  public void BuildExecutionConfig_DockerImage_Sets_ContainerConfig()
  {
    // Arrange
    var projectWithImage = new Project("with-image")
    {
      DockerImage = "mcr.microsoft.com/powershell:latest",
    };

    var projectWithoutImage = new Project("without-image")
    {
      DockerImage = null,
    };

    // Act
    var configWithImage = projectWithImage.BuildExecutionConfig();
    var configWithoutImage = projectWithoutImage.BuildExecutionConfig();

    // Assert
    configWithImage.Container.Should().NotBeNull();
    configWithImage.Container!.Image.Should().Be("mcr.microsoft.com/powershell:latest");

    configWithoutImage.Container.Should().BeNull();
  }
}
