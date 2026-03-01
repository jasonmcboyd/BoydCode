using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.SlashCommands;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class SlashCommandRegistryTests
{
  private readonly SlashCommandRegistry _sut = new();

  [Fact]
  public async Task TryHandleAsync_WhenNoCommandsRegistered_ReturnsFalse()
  {
    // Act
    var result = await _sut.TryHandleAsync("/test");

    // Assert
    result.Should().BeFalse();
  }

  [Fact]
  public async Task TryHandleAsync_WhenCommandHandles_ReturnsTrue()
  {
    // Arrange
    var command = CreateMockCommand("/test", handlesInput: true);
    _sut.Register(command);

    // Act
    var result = await _sut.TryHandleAsync("/test");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task TryHandleAsync_WhenFirstDoesNotHandle_TriesSecond()
  {
    // Arrange
    var first = CreateMockCommand("/other", handlesInput: false);
    var second = CreateMockCommand("/test", handlesInput: true);
    _sut.Register(first);
    _sut.Register(second);

    // Act
    var result = await _sut.TryHandleAsync("/test");

    // Assert
    result.Should().BeTrue();
    await second.Received(1).TryHandleAsync("/test", Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task TryHandleAsync_StopsAtFirstMatch()
  {
    // Arrange
    var first = CreateMockCommand("/test", handlesInput: true);
    var second = CreateMockCommand("/test", handlesInput: true);
    _sut.Register(first);
    _sut.Register(second);

    // Act
    var result = await _sut.TryHandleAsync("/test");

    // Assert
    result.Should().BeTrue();
    await first.Received(1).TryHandleAsync("/test", Arg.Any<CancellationToken>());
    await second.DidNotReceive().TryHandleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public void GetAllDescriptors_ReturnsAllRegisteredDescriptors()
  {
    // Arrange
    var first = CreateMockCommand("/help", handlesInput: false);
    var second = CreateMockCommand("/project", handlesInput: false);
    _sut.Register(first);
    _sut.Register(second);

    // Act
    var descriptors = _sut.GetAllDescriptors();

    // Assert
    descriptors.Should().HaveCount(2);
    descriptors[0].Prefix.Should().Be("/help");
    descriptors[1].Prefix.Should().Be("/project");
  }

  private static ISlashCommand CreateMockCommand(string prefix, bool handlesInput)
  {
    var command = Substitute.For<ISlashCommand>();
    command.Descriptor.Returns(new SlashCommandDescriptor(prefix, $"{prefix} command", []));
    command.TryHandleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .Returns(Task.FromResult(handlesInput));
    return command;
  }
}
