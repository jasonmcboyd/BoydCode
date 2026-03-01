using FluentAssertions;
using Xunit;

namespace BoydCode.Infrastructure.Container.Tests;

public sealed class ShellDialectTests
{
  [Fact]
  public void WrapWithSentinel_Bash_ContainsEchoWithMarker()
  {
    // Arrange
    var dialect = new ShellDialect("bash");
    var marker = "test123";

    // Act
    var wrapped = dialect.WrapWithSentinel("ls -la", marker);

    // Assert
    wrapped.Should().Contain("echo");
    wrapped.Should().Contain(marker);
  }

  [Fact]
  public void WrapWithSentinel_Bash_ContainsExitCodeCapture()
  {
    // Arrange
    var dialect = new ShellDialect("bash");

    // Act
    var wrapped = dialect.WrapWithSentinel("ls -la", "m1");

    // Assert
    wrapped.Should().Contain("__bc_ec=$?");
  }

  [Fact]
  public void WrapWithSentinel_Pwsh_ContainsWriteOutput()
  {
    // Arrange
    var dialect = new ShellDialect("pwsh");
    var marker = "test456";

    // Act
    var wrapped = dialect.WrapWithSentinel("Get-Process", marker);

    // Assert
    wrapped.Should().Contain("Write-Output");
    wrapped.Should().Contain(marker);
  }

  [Fact]
  public void WrapWithSentinel_Pwsh_ContainsPowerShellExitCheck()
  {
    // Arrange
    var dialect = new ShellDialect("pwsh");

    // Act
    var wrapped = dialect.WrapWithSentinel("Get-Process", "m2");

    // Assert
    wrapped.Should().Contain("if ($?)");
  }

  [Fact]
  public void IsSentinel_MatchingLine_ReturnsTrue()
  {
    // Arrange -- construct a sentinel line that matches the expected format
    var marker = "abc";
    var sentinelLine = $"___BOYDCODE_EXIT_{marker}_0___";

    // Act
    var result = ShellDialect.IsSentinel(sentinelLine, marker);

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public void IsSentinel_DifferentMarker_ReturnsFalse()
  {
    // Arrange
    var sentinelLine = "___BOYDCODE_EXIT_abc_0___";

    // Act
    var result = ShellDialect.IsSentinel(sentinelLine, "xyz");

    // Assert
    result.Should().BeFalse();
  }

  [Fact]
  public void IsSentinel_NonSentinelLine_ReturnsFalse()
  {
    // Arrange
    var regularLine = "total 42";

    // Act
    var result = ShellDialect.IsSentinel(regularLine, "m1");

    // Assert
    result.Should().BeFalse();
  }

  [Fact]
  public void ParseExitCode_Zero_ReturnsZero()
  {
    // Arrange
    var marker = "run1";
    var sentinelLine = $"___BOYDCODE_EXIT_{marker}_0___";

    // Act
    var exitCode = ShellDialect.ParseExitCode(sentinelLine, marker);

    // Assert
    exitCode.Should().Be(0);
  }

  [Fact]
  public void ParseExitCode_NonZero_ReturnsCorrectCode()
  {
    // Arrange
    var marker = "run2";
    var sentinelLine = $"___BOYDCODE_EXIT_{marker}_127___";

    // Act
    var exitCode = ShellDialect.ParseExitCode(sentinelLine, marker);

    // Assert
    exitCode.Should().Be(127);
  }

  [Fact]
  public void ParseExitCode_MalformedLine_ReturnsOne()
  {
    // Arrange -- a line that does not contain a valid exit code
    var malformedLine = "some random output without sentinel format";

    // Act
    var exitCode = ShellDialect.ParseExitCode(malformedLine, "m1");

    // Assert
    exitCode.Should().Be(1);
  }
}
