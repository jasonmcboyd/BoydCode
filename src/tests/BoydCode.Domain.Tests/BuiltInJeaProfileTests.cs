using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BoydCode.Domain.Tests;

public sealed class BuiltInJeaProfileTests
{
  [Fact]
  public void Instance_NameIs_builtin()
  {
    // Arrange & Act
    var profile = BuiltInJeaProfile.Instance;

    // Assert
    profile.Name.Should().Be("_builtin");
  }

  [Fact]
  public void Instance_LanguageMode_IsConstrainedLanguage()
  {
    // Arrange & Act
    var profile = BuiltInJeaProfile.Instance;

    // Assert
    profile.LanguageMode.Should().Be(PSLanguageModeName.ConstrainedLanguage);
  }

  [Fact]
  public void Instance_HasNoWriteCommands()
  {
    // Arrange
    var writeCommands = new[]
    {
            "Set-Content",
            "New-Item",
            "Remove-Item",
            "Copy-Item",
            "Move-Item",
            "Rename-Item",
            "Add-Content",
        };

    // Act
    var allowedCommands = BuiltInJeaProfile.Instance.AllowedCommands;

    // Assert
    allowedCommands.Should().NotContain(writeCommands);
  }

  [Fact]
  public void Instance_HasNoExternalTools()
  {
    // Arrange
    var externalTools = new[] { "dotnet", "git" };

    // Act
    var allowedCommands = BuiltInJeaProfile.Instance.AllowedCommands;

    // Assert
    allowedCommands.Should().NotContain(externalTools);
  }

  [Fact]
  public void Instance_HasReadCommands()
  {
    // Arrange
    var expectedReadCommands = new[]
    {
            "Get-Content",
            "Get-ChildItem",
            "Select-String",
            "Test-Path",
        };

    // Act
    var allowedCommands = BuiltInJeaProfile.Instance.AllowedCommands;

    // Assert
    allowedCommands.Should().Contain(expectedReadCommands);
  }

  [Fact]
  public void GlobalName_Is_global()
  {
    // Arrange & Act & Assert
    BuiltInJeaProfile.GlobalName.Should().Be("_global");
  }
}
