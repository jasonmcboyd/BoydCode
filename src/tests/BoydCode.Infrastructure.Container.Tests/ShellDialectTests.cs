using FluentAssertions;
using Xunit;

namespace BoydCode.Infrastructure.Container.Tests;

public sealed class ShellDialectTests
{
  [Fact]
  public void WrapWithSentinel_Bash_ContainsStartAndExitSentinels()
  {
    // Arrange
    var dialect = new ShellDialect("bash");
    var marker = "test123";

    // Act
    var wrapped = dialect.WrapWithSentinel("ls -la", marker);

    // Assert
    wrapped.Should().Contain("echo");
    wrapped.Should().Contain($"___BOYDCODE_START_{marker}___");
    wrapped.Should().Contain($"___BOYDCODE_EXIT_{marker}");
  }

  [Fact]
  public void WrapWithSentinel_Bash_StartsWithStartSentinel()
  {
    // Arrange
    var dialect = new ShellDialect("bash");

    // Act
    var wrapped = dialect.WrapWithSentinel("ls -la", "m1");

    // Assert — start sentinel first, then command, then exit
    var lines = wrapped.Split('\n');
    lines[0].Should().Contain("___BOYDCODE_START_m1___");
    lines[1].Should().Be("ls -la");
    lines[2].Should().Contain("__bc_ec=$?");
  }

  [Fact]
  public void WrapWithSentinel_Pwsh_ContainsStartAndExitSentinels()
  {
    // Arrange
    var dialect = new ShellDialect("pwsh");
    var marker = "test456";

    // Act
    var wrapped = dialect.WrapWithSentinel("Get-Process", marker);

    // Assert
    wrapped.Should().Contain("Write-Output");
    wrapped.Should().Contain($"___BOYDCODE_START_{marker}___");
    wrapped.Should().Contain($"___BOYDCODE_EXIT_{marker}");
  }

  [Fact]
  public void WrapWithSentinel_Pwsh_StartsWithStartSentinel()
  {
    // Arrange
    var dialect = new ShellDialect("pwsh");

    // Act
    var wrapped = dialect.WrapWithSentinel("Get-Process", "m2");

    // Assert — start sentinel first, then command, then exit
    var lines = wrapped.Split('\n');
    lines[0].Should().Contain("___BOYDCODE_START_m2___");
    lines[1].Should().Be("Get-Process");
    lines[2].Should().Contain("if ($?)");
  }

  [Fact]
  public void BuildStartPattern_ReturnsExpectedFormat()
  {
    // Act
    var pattern = ShellDialect.BuildStartPattern("abc");

    // Assert
    pattern.Should().Be("___BOYDCODE_START_abc___");
  }

  [Fact]
  public void BuildExitPattern_ReturnsExpectedFormat()
  {
    // Act
    var pattern = ShellDialect.BuildExitPattern("abc");

    // Assert
    pattern.Should().Be("___BOYDCODE_EXIT_abc_");
  }

  [Fact]
  public void IsStartSentinel_ExactMatch_ReturnsTrue()
  {
    // Arrange
    var startPattern = ShellDialect.BuildStartPattern("abc");

    // Act & Assert
    ShellDialect.IsStartSentinel("___BOYDCODE_START_abc___", startPattern).Should().BeTrue();
  }

  [Fact]
  public void IsStartSentinel_WithShellPromptPrefix_ReturnsTrue()
  {
    // Arrange — shell may echo the sentinel with a prompt prefix
    var startPattern = ShellDialect.BuildStartPattern("abc");
    var lineWithPrompt = "bash-5.2$ ___BOYDCODE_START_abc___";

    // Act & Assert
    ShellDialect.IsStartSentinel(lineWithPrompt, startPattern).Should().BeTrue();
  }

  [Fact]
  public void IsStartSentinel_WithTrailingCarriageReturn_ReturnsTrue()
  {
    // Arrange — Docker on Windows may produce lines with trailing \r
    var startPattern = ShellDialect.BuildStartPattern("abc");
    var lineWithCr = "___BOYDCODE_START_abc___\r";

    // Act & Assert
    ShellDialect.IsStartSentinel(lineWithCr, startPattern).Should().BeTrue();
  }

  [Fact]
  public void IsStartSentinel_DifferentMarker_ReturnsFalse()
  {
    // Arrange
    var startPattern = ShellDialect.BuildStartPattern("abc");

    // Act & Assert
    ShellDialect.IsStartSentinel("___BOYDCODE_START_xyz___", startPattern).Should().BeFalse();
  }

  [Fact]
  public void IsStartSentinel_CommandEcho_Bash_ReturnsFalse()
  {
    // Arrange — shell echoes the echo command itself; ends with " not ___
    var startPattern = ShellDialect.BuildStartPattern("abc");
    var echoLine = "echo \"___BOYDCODE_START_abc___\"";

    // Act & Assert
    ShellDialect.IsStartSentinel(echoLine, startPattern).Should().BeFalse();
  }

  [Fact]
  public void IsStartSentinel_CommandEcho_Pwsh_ReturnsFalse()
  {
    // Arrange — PowerShell echoes the Write-Output command; ends with " not ___
    var startPattern = ShellDialect.BuildStartPattern("abc");
    var echoLine = "PS /> Write-Output \"___BOYDCODE_START_abc___\"";

    // Act & Assert
    ShellDialect.IsStartSentinel(echoLine, startPattern).Should().BeFalse();
  }

  [Fact]
  public void IsExitSentinel_MatchingLine_ReturnsTrue()
  {
    // Arrange
    var exitPattern = ShellDialect.BuildExitPattern("abc");

    // Act & Assert
    ShellDialect.IsExitSentinel("___BOYDCODE_EXIT_abc_0___", exitPattern).Should().BeTrue();
  }

  [Fact]
  public void IsExitSentinel_DifferentMarker_ReturnsFalse()
  {
    // Arrange
    var exitPattern = ShellDialect.BuildExitPattern("abc");

    // Act & Assert
    ShellDialect.IsExitSentinel("___BOYDCODE_EXIT_xyz_0___", exitPattern).Should().BeFalse();
  }

  [Fact]
  public void IsExitSentinel_NonSentinelLine_ReturnsFalse()
  {
    // Arrange
    var exitPattern = ShellDialect.BuildExitPattern("m1");

    // Act & Assert
    ShellDialect.IsExitSentinel("total 42", exitPattern).Should().BeFalse();
  }

  [Fact]
  public void ParseExitCode_Zero_ReturnsZero()
  {
    // Arrange
    var exitPattern = ShellDialect.BuildExitPattern("run1");

    // Act
    var exitCode = ShellDialect.ParseExitCode("___BOYDCODE_EXIT_run1_0___", exitPattern);

    // Assert
    exitCode.Should().Be(0);
  }

  [Fact]
  public void ParseExitCode_NonZero_ReturnsCorrectCode()
  {
    // Arrange
    var exitPattern = ShellDialect.BuildExitPattern("run2");

    // Act
    var exitCode = ShellDialect.ParseExitCode("___BOYDCODE_EXIT_run2_127___", exitPattern);

    // Assert
    exitCode.Should().Be(127);
  }

  [Fact]
  public void ParseExitCode_MalformedLine_ReturnsOne()
  {
    // Arrange
    var exitPattern = ShellDialect.BuildExitPattern("m1");

    // Act
    var exitCode = ShellDialect.ParseExitCode("some random output", exitPattern);

    // Assert
    exitCode.Should().Be(1);
  }
}
