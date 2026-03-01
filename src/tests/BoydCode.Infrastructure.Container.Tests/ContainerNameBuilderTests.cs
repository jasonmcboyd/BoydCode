using FluentAssertions;
using Xunit;

namespace BoydCode.Infrastructure.Container.Tests;

public sealed class ContainerNameBuilderTests
{
  [Fact]
  public void Build_SimpleProjectName_StartsWithPrefix()
  {
    // Arrange & Act
    var name = ContainerNameBuilder.Build("webapp");

    // Assert
    name.Should().StartWith(ContainerNameBuilder.Prefix);
  }

  [Fact]
  public void Build_SimpleProjectName_ContainsProjectName()
  {
    // Arrange & Act
    var name = ContainerNameBuilder.Build("webapp");

    // Assert
    name.Should().Contain("webapp");
  }

  [Fact]
  public void Build_SimpleProjectName_EndsWithShortGuid()
  {
    // Arrange & Act
    var name = ContainerNameBuilder.Build("webapp");

    // Assert -- format is "boydcode-{sanitized}-{8hexchars}"
    // The last segment after the final hyphen should be 8 hex characters
    var lastDash = name.LastIndexOf('-');
    var suffix = name[(lastDash + 1)..];
    suffix.Should().HaveLength(8);
    suffix.Should().MatchRegex("^[0-9a-f]{8}$",
        "the trailing segment should be 8 lowercase hex characters from a Guid");
  }

  [Fact]
  public void Build_NameWithSpecialChars_SanitizesToHyphens()
  {
    // Arrange & Act
    var name = ContainerNameBuilder.Build("My Project!!");

    // Assert -- special chars replaced with hyphens, lowercased,
    // consecutive hyphens collapsed and leading/trailing hyphens trimmed
    name.Should().Contain("my-project");
  }

  [Fact]
  public void Build_NameWithSpaces_SanitizedCorrectly()
  {
    // Arrange & Act
    var name = ContainerNameBuilder.Build("hello world");

    // Assert
    name.Should().Contain("hello-world");
  }

  [Fact]
  public void Build_EmptyName_UsesProjectFallback()
  {
    // Arrange & Act
    var name = ContainerNameBuilder.Build("");

    // Assert -- empty string sanitizes to empty, falls back to "project"
    name.Should().Contain("project");
  }

  [Fact]
  public void Build_TwoCalls_ProducesDifferentNames()
  {
    // Arrange & Act
    var name1 = ContainerNameBuilder.Build("webapp");
    var name2 = ContainerNameBuilder.Build("webapp");

    // Assert -- each call generates a fresh Guid suffix
    name1.Should().NotBe(name2);
  }
}
