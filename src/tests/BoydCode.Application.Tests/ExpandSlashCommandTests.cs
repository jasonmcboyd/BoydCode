using BoydCode.Application.Interfaces;
using BoydCode.Presentation.Console.Commands;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class ExpandSlashCommandTests
{
  private static ExpandSlashCommand CreateSut(IUserInterface? ui = null)
  {
    ui ??= Substitute.For<IUserInterface>();
    return new ExpandSlashCommand(ui);
  }

  [Fact]
  public async Task TryHandleAsync_WithExpandCommand_CallsExpandAndReturnsTrue()
  {
    // Arrange
    var ui = Substitute.For<IUserInterface>();
    var sut = CreateSut(ui);

    // Act
    var result = await sut.TryHandleAsync("/expand");

    // Assert
    result.Should().BeTrue();
    ui.Received(1).ExpandLastToolOutput();
  }

  [Fact]
  public async Task TryHandleAsync_WithExpandCommandCaseInsensitive_ReturnsTrue()
  {
    // Arrange
    var ui = Substitute.For<IUserInterface>();
    var sut = CreateSut(ui);

    // Act
    var result = await sut.TryHandleAsync("/EXPAND");

    // Assert
    result.Should().BeTrue();
    ui.Received(1).ExpandLastToolOutput();
  }

  [Fact]
  public async Task TryHandleAsync_WithExpandCommandWithWhitespace_ReturnsTrue()
  {
    // Arrange
    var ui = Substitute.For<IUserInterface>();
    var sut = CreateSut(ui);

    // Act
    var result = await sut.TryHandleAsync("  /expand  ");

    // Assert
    result.Should().BeTrue();
    ui.Received(1).ExpandLastToolOutput();
  }

  [Fact]
  public async Task TryHandleAsync_WithUnrelatedCommand_ReturnsFalse()
  {
    // Arrange
    var ui = Substitute.For<IUserInterface>();
    var sut = CreateSut(ui);

    // Act
    var result = await sut.TryHandleAsync("/clear");

    // Assert
    result.Should().BeFalse();
    ui.DidNotReceive().ExpandLastToolOutput();
  }

  [Fact]
  public void Descriptor_HasCorrectPrefix()
  {
    // Arrange
    var sut = CreateSut();

    // Act & Assert
    sut.Descriptor.Prefix.Should().Be("/expand");
  }

  [Fact]
  public void Descriptor_HasCorrectDescription()
  {
    // Arrange
    var sut = CreateSut();

    // Act & Assert
    sut.Descriptor.Description.Should().Be("Show full output from the last tool execution");
  }
}
