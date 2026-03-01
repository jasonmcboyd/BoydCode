using BoydCode.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BoydCode.Domain.Tests;

public sealed class ExitCodeTests
{
  [Fact]
  public void Success_IsZero()
  {
    ((int)ExitCode.Success).Should().Be(0);
  }

  [Fact]
  public void UserCancelled_Is130()
  {
    ((int)ExitCode.UserCancelled).Should().Be(130);
  }

  [Fact]
  public void GeneralError_IsOne()
  {
    ((int)ExitCode.GeneralError).Should().Be(1);
  }
}
