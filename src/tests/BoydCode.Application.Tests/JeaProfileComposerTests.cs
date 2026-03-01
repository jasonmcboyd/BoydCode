using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class JeaProfileComposerTests
{
  // --- Compose (static, pure) tests ---

  [Fact]
  public void Compose_WithOnlyBuiltIn_ReturnsAllBuiltInCommands()
  {
    // Arrange
    var profiles = new List<JeaProfile> { BuiltInJeaProfile.Instance };

    // Act
    var result = JeaProfileComposer.Compose(profiles);

    // Assert
    result.AllowedCommands.Should().HaveCount(28);
    result.LanguageMode.Should().Be(PSLanguageModeName.ConstrainedLanguage);
    result.AllowedCommands.Should().Contain("Get-Content");
    result.AllowedCommands.Should().Contain("Write-Output");
  }

  [Fact]
  public void Compose_AdditionalProfile_AddsCommands()
  {
    // Arrange
    var extra = new JeaProfile(
        "dev-tools",
        PSLanguageModeName.ConstrainedLanguage,
        [],
        [
            new JeaProfileEntry("dotnet", IsDenied: false),
                new JeaProfileEntry("git", IsDenied: false),
        ]);

    var profiles = new List<JeaProfile> { BuiltInJeaProfile.Instance, extra };

    // Act
    var result = JeaProfileComposer.Compose(profiles);

    // Assert
    result.AllowedCommands.Should().Contain("dotnet");
    result.AllowedCommands.Should().Contain("git");
    // Built-in commands should still be present
    result.AllowedCommands.Should().Contain("Get-Content");
    result.AllowedCommands.Should().HaveCount(30);
  }

  [Fact]
  public void Compose_DenyOverridesAllow()
  {
    // Arrange
    var denyProfile = new JeaProfile(
        "restrict-read",
        PSLanguageModeName.ConstrainedLanguage,
        [],
        [
            new JeaProfileEntry("Get-Content", IsDenied: true),
        ]);

    var profiles = new List<JeaProfile> { BuiltInJeaProfile.Instance, denyProfile };

    // Act
    var result = JeaProfileComposer.Compose(profiles);

    // Assert
    result.AllowedCommands.Should().NotContain("Get-Content");
    // Other commands should still be present
    result.AllowedCommands.Should().Contain("Get-ChildItem");
  }

  [Fact]
  public void Compose_DenyInEarlierProfile_StillDeniesLaterAllow()
  {
    // Arrange
    // Profile1 denies "git", Profile2 allows "git" -- deny should always win
    var profile1 = new JeaProfile(
        "deny-git",
        PSLanguageModeName.ConstrainedLanguage,
        [],
        [
            new JeaProfileEntry("git", IsDenied: true),
        ]);

    var profile2 = new JeaProfile(
        "allow-git",
        PSLanguageModeName.ConstrainedLanguage,
        [],
        [
            new JeaProfileEntry("git", IsDenied: false),
        ]);

    var profiles = new List<JeaProfile> { BuiltInJeaProfile.Instance, profile1, profile2 };

    // Act
    var result = JeaProfileComposer.Compose(profiles);

    // Assert
    result.AllowedCommands.Should().NotContain("git");
  }

  [Fact]
  public void Compose_LanguageMode_MostRestrictiveWins()
  {
    // Arrange
    // Built-in is ConstrainedLanguage (1), this profile is NoLanguage (3)
    var noLangProfile = new JeaProfile(
        "no-lang",
        PSLanguageModeName.NoLanguage,
        [],
        []);

    var profiles = new List<JeaProfile> { BuiltInJeaProfile.Instance, noLangProfile };

    // Act
    var result = JeaProfileComposer.Compose(profiles);

    // Assert
    result.LanguageMode.Should().Be(PSLanguageModeName.NoLanguage);
  }

  [Fact]
  public void Compose_LanguageMode_LessRestrictiveIgnored()
  {
    // Arrange
    // Built-in is ConstrainedLanguage (1), this profile is FullLanguage (0) -- less restrictive
    var fullLangProfile = new JeaProfile(
        "full-lang",
        PSLanguageModeName.FullLanguage,
        [],
        []);

    var profiles = new List<JeaProfile> { BuiltInJeaProfile.Instance, fullLangProfile };

    // Act
    var result = JeaProfileComposer.Compose(profiles);

    // Assert
    result.LanguageMode.Should().Be(PSLanguageModeName.ConstrainedLanguage);
  }

  [Fact]
  public void Compose_Modules_AreUnioned()
  {
    // Arrange
    var profile1 = new JeaProfile(
        "modules-a",
        PSLanguageModeName.ConstrainedLanguage,
        ["Microsoft.PowerShell.Utility", "PSReadLine"],
        []);

    var profile2 = new JeaProfile(
        "modules-b",
        PSLanguageModeName.ConstrainedLanguage,
        ["Az.Accounts", "PSReadLine"],
        []);

    var profiles = new List<JeaProfile> { BuiltInJeaProfile.Instance, profile1, profile2 };

    // Act
    var result = JeaProfileComposer.Compose(profiles);

    // Assert
    result.Modules.Should().HaveCount(3);
    result.Modules.Should().Contain("Microsoft.PowerShell.Utility");
    result.Modules.Should().Contain("PSReadLine");
    result.Modules.Should().Contain("Az.Accounts");
  }

  [Fact]
  public void Compose_EmptyProfiles_ReturnsBuiltInOnly()
  {
    // Arrange
    var emptyProfile = new JeaProfile(
        "empty",
        PSLanguageModeName.FullLanguage,
        [],
        []);

    var profiles = new List<JeaProfile> { BuiltInJeaProfile.Instance, emptyProfile };

    // Act
    var result = JeaProfileComposer.Compose(profiles);

    // Assert
    result.AllowedCommands.Should().HaveCount(28);
    // FullLanguage (0) < ConstrainedLanguage (1), so built-in wins
    result.LanguageMode.Should().Be(PSLanguageModeName.ConstrainedLanguage);
  }

  // --- ComposeAsync tests (require mocked store) ---

  [Fact]
  public async Task ComposeAsync_MissingProfile_SkipsWithoutError()
  {
    // Arrange
    var store = Substitute.For<IJeaProfileStore>();
    store.LoadAsync(BuiltInJeaProfile.GlobalName, Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<JeaProfile?>(null));
    store.LoadAsync("nonexistent", Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<JeaProfile?>(null));

    var logger = Substitute.For<ILogger<JeaProfileComposer>>();
    var composer = new JeaProfileComposer(store, logger);

    // Act
    var result = await composer.ComposeAsync(["nonexistent"]);

    // Assert -- should not throw, and result should contain built-in commands only
    result.AllowedCommands.Should().HaveCount(28);
    result.LanguageMode.Should().Be(PSLanguageModeName.ConstrainedLanguage);
  }

  [Fact]
  public async Task ComposeAsync_NoProfileNames_ReturnsBuiltInOnly()
  {
    // Arrange
    var store = Substitute.For<IJeaProfileStore>();
    store.LoadAsync(BuiltInJeaProfile.GlobalName, Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<JeaProfile?>(null));

    var logger = Substitute.For<ILogger<JeaProfileComposer>>();
    var composer = new JeaProfileComposer(store, logger);

    // Act
    var result = await composer.ComposeAsync([]);

    // Assert
    result.AllowedCommands.Should().HaveCount(28);
    result.LanguageMode.Should().Be(PSLanguageModeName.ConstrainedLanguage);

    // Store should have been called once for the _global profile
    await store.Received(1).LoadAsync(BuiltInJeaProfile.GlobalName, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task ComposeAsync_LoadsGlobalFromStore_WhenAvailable()
  {
    // Arrange -- custom _global profile with the 28 built-in entries plus 2 extras
    var entries = BuiltInJeaProfile.Instance.Entries
        .Concat(new[]
        {
                new JeaProfileEntry("git", IsDenied: false),
                new JeaProfileEntry("dotnet", IsDenied: false),
        })
        .ToList();

    var customGlobal = new JeaProfile(
        BuiltInJeaProfile.GlobalName,
        PSLanguageModeName.ConstrainedLanguage,
        [],
        entries);

    var store = Substitute.For<IJeaProfileStore>();
    store.LoadAsync(BuiltInJeaProfile.GlobalName, Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<JeaProfile?>(customGlobal));

    var logger = Substitute.For<ILogger<JeaProfileComposer>>();
    var composer = new JeaProfileComposer(store, logger);

    // Act
    var result = await composer.ComposeAsync([]);

    // Assert
    result.AllowedCommands.Should().HaveCount(30);
    result.AllowedCommands.Should().Contain("git");
    result.AllowedCommands.Should().Contain("dotnet");
  }

  [Fact]
  public async Task ComposeAsync_FallsBackToBuiltIn_WhenGlobalNotInStore()
  {
    // Arrange
    var store = Substitute.For<IJeaProfileStore>();
    store.LoadAsync(BuiltInJeaProfile.GlobalName, Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<JeaProfile?>(null));

    var logger = Substitute.For<ILogger<JeaProfileComposer>>();
    var composer = new JeaProfileComposer(store, logger);

    // Act
    var result = await composer.ComposeAsync([]);

    // Assert
    result.AllowedCommands.Should().HaveCount(28);
    result.LanguageMode.Should().Be(PSLanguageModeName.ConstrainedLanguage);
  }

  [Fact]
  public async Task ComposeAsync_SkipsDuplicateGlobal_WhenExplicitlyListed()
  {
    // Arrange -- custom _global profile so we can verify it is not loaded twice
    var customGlobal = new JeaProfile(
        BuiltInJeaProfile.GlobalName,
        PSLanguageModeName.ConstrainedLanguage,
        [],
        BuiltInJeaProfile.Instance.Entries);

    var store = Substitute.For<IJeaProfileStore>();
    store.LoadAsync(BuiltInJeaProfile.GlobalName, Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<JeaProfile?>(customGlobal));

    var logger = Substitute.For<ILogger<JeaProfileComposer>>();
    var composer = new JeaProfileComposer(store, logger);

    // Act -- explicitly pass "_global" in the profile names list
    var result = await composer.ComposeAsync([BuiltInJeaProfile.GlobalName]);

    // Assert -- _global should have been loaded exactly once (the implicit load),
    // not a second time from the explicit list
    await store.Received(1).LoadAsync(BuiltInJeaProfile.GlobalName, Arg.Any<CancellationToken>());
    result.AllowedCommands.Should().HaveCount(28);
  }

  [Fact]
  public async Task ComposeAsync_EditedGlobalWithDeny_ReflectsInResult()
  {
    // Arrange -- _global profile with all 28 built-in entries plus a deny for Get-Content
    var entries = BuiltInJeaProfile.Instance.Entries
        .Concat(new[]
        {
                new JeaProfileEntry("Get-Content", IsDenied: true),
        })
        .ToList();

    var customGlobal = new JeaProfile(
        BuiltInJeaProfile.GlobalName,
        PSLanguageModeName.ConstrainedLanguage,
        [],
        entries);

    var store = Substitute.For<IJeaProfileStore>();
    store.LoadAsync(BuiltInJeaProfile.GlobalName, Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<JeaProfile?>(customGlobal));

    var logger = Substitute.For<ILogger<JeaProfileComposer>>();
    var composer = new JeaProfileComposer(store, logger);

    // Act
    var result = await composer.ComposeAsync([]);

    // Assert -- deny-always-wins means Get-Content should be removed
    result.AllowedCommands.Should().NotContain("Get-Content");
    result.AllowedCommands.Should().Contain("Get-ChildItem");
    result.AllowedCommands.Should().HaveCount(27);
  }
}
