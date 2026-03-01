using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class ActiveProviderTests
{
  private readonly ILlmProviderFactory _factory = Substitute.For<ILlmProviderFactory>();

  private ActiveProvider CreateSut() => new(_factory);

  [Fact]
  public void IsConfigured_WhenFresh_ReturnsFalse()
  {
    // Arrange
    var sut = CreateSut();

    // Assert
    sut.IsConfigured.Should().BeFalse();
    sut.Provider.Should().BeNull();
    sut.Config.Should().BeNull();
  }

  [Fact]
  public void Activate_SetsProviderAndConfig()
  {
    // Arrange
    var sut = CreateSut();
    var config = new LlmProviderConfig { Model = "test-model" };
    var mockProvider = Substitute.For<ILlmProvider>();
    _factory.Create(config).Returns(mockProvider);

    // Act
    sut.Activate(config);

    // Assert
    sut.IsConfigured.Should().BeTrue();
    sut.Provider.Should().BeSameAs(mockProvider);
    sut.Config.Should().BeSameAs(config);
  }

  [Fact]
  public void Activate_DisposesPreviousProvider()
  {
    // Arrange
    var sut = CreateSut();
    var firstConfig = new LlmProviderConfig { Model = "first-model" };
    var secondConfig = new LlmProviderConfig { Model = "second-model" };

    var firstProvider = Substitute.For<IDisposableLlmProvider>();
    var secondProvider = Substitute.For<ILlmProvider>();

    _factory.Create(firstConfig).Returns(firstProvider);
    _factory.Create(secondConfig).Returns(secondProvider);

    sut.Activate(firstConfig);

    // Act
    sut.Activate(secondConfig);

    // Assert — first provider should have been disposed when the second was activated
    firstProvider.Received(1).Dispose();
    sut.Provider.Should().BeSameAs(secondProvider);
  }

  [Fact]
  public void Activate_WhenFactoryThrows_Propagates()
  {
    // Arrange
    var sut = CreateSut();
    var config = new LlmProviderConfig { Model = "bad-model" };
    _factory.Create(config).Returns(_ => throw new InvalidOperationException("Factory failure"));

    // Act
    var act = () => sut.Activate(config);

    // Assert
    act.Should().Throw<InvalidOperationException>().WithMessage("Factory failure");
  }

  /// <summary>
  /// Combined interface so NSubstitute can create a proxy that is both
  /// <see cref="ILlmProvider"/> and <see cref="IDisposable"/>.
  /// </summary>
  // Internal (not private) so Castle DynamicProxy can generate a proxy for NSubstitute.
  // Requires [InternalsVisibleTo("DynamicProxyGenAssembly2")] on the test assembly.
  internal interface IDisposableLlmProvider : ILlmProvider, IDisposable { }
}
