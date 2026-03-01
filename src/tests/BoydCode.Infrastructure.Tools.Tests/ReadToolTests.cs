using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Enums;
using BoydCode.Infrastructure.Tools.Tools;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BoydCode.Infrastructure.Tools.Tests;

public sealed class ReadToolTests
{
  [Fact]
  public async Task ExecuteAsync_WithNonExistentFile_ReturnsIsErrorTrue()
  {
    // Arrange
    var guard = Substitute.For<IDirectoryGuard>();
    guard.GetAccessLevel(Arg.Any<string>()).Returns(DirectoryAccessLevel.ReadWrite);
    var tool = new ReadTool(guard);
    var arguments = JsonSerializer.Serialize(new { file_path = @"C:\nonexistent\path\file.txt" });

    // Act
    var result = await tool.ExecuteAsync(arguments, Directory.GetCurrentDirectory(), CancellationToken.None);

    // Assert
    result.IsError.Should().BeTrue();
    result.Content.Should().Contain("File not found");
  }
}
