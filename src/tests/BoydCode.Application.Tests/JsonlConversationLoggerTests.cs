using System.Text.Json;
using BoydCode.Domain.Enums;
using BoydCode.Infrastructure.Persistence.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class JsonlConversationLoggerTests : IAsyncLifetime
{
  private static readonly string LogDirectory =
    Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      ".boydcode",
      "logs");

  private readonly string _sessionId = $"test_{Guid.NewGuid():N}";
  private string LogFilePath => Path.Combine(LogDirectory, $"{_sessionId}.jsonl");

  public Task InitializeAsync() => Task.CompletedTask;

  public Task DisposeAsync()
  {
    // Clean up the test log file if it exists
    if (File.Exists(LogFilePath))
    {
      File.Delete(LogFilePath);
    }
    return Task.CompletedTask;
  }

  private static JsonlConversationLogger CreateLogger()
  {
    var loggerFactory = NullLoggerFactory.Instance;
    var logger = loggerFactory.CreateLogger<JsonlConversationLogger>();
    return new JsonlConversationLogger(logger);
  }

  [Fact]
  public async Task InitializeAsync_CreatesLogFile()
  {
    // Arrange
    await using var sut = CreateLogger();

    // Act
    await sut.InitializeAsync(_sessionId);

    // Assert
    File.Exists(LogFilePath).Should().BeTrue(
      "InitializeAsync should create the JSONL log file at the expected path");
  }

  [Fact]
  public async Task LogSessionStartAsync_WritesJsonlEvent()
  {
    // Arrange
    await using var sut = CreateLogger();
    await sut.InitializeAsync(_sessionId);

    // Act
    await sut.LogSessionStartAsync(
      LlmProviderType.Anthropic,
      "claude-sonnet-4-20250514",
      "test-project",
      ExecutionMode.InProcess,
      "/tmp/test");

    // Flush by disposing
    await sut.DisposeAsync();

    // Assert -- read the file and verify it contains valid JSONL with the expected event type
    var lines = await File.ReadAllLinesAsync(LogFilePath);
    lines.Should().HaveCount(1);

    using var doc = JsonDocument.Parse(lines[0]);
    var root = doc.RootElement;
    root.GetProperty("type").GetString().Should().Be("session_start");
    root.GetProperty("session_id").GetString().Should().Be(_sessionId);
    root.TryGetProperty("timestamp", out _).Should().BeTrue();

    var data = root.GetProperty("data");
    data.GetProperty("provider").GetString().Should().Be("Anthropic");
    data.GetProperty("model").GetString().Should().Be("claude-sonnet-4-20250514");
    data.GetProperty("project").GetString().Should().Be("test-project");
    data.GetProperty("engine_mode").GetString().Should().Be("InProcess");
    data.GetProperty("working_directory").GetString().Should().Be("/tmp/test");
  }

  [Fact]
  public async Task LogToolResultAsync_TruncatesLongOutput()
  {
    // Arrange
    await using var sut = CreateLogger();
    await sut.InitializeAsync(_sessionId);

    // Build a string that exceeds the 10,000-char truncation limit
    var longOutput = new string('x', 20_000);

    // Act
    await sut.LogToolResultAsync(
      "Shell",
      longOutput,
      isError: false,
      TimeSpan.FromMilliseconds(150));

    // Flush by disposing
    await sut.DisposeAsync();

    // Assert
    var lines = await File.ReadAllLinesAsync(LogFilePath);
    lines.Should().HaveCount(1);

    using var doc = JsonDocument.Parse(lines[0]);
    var data = doc.RootElement.GetProperty("data");
    var output = data.GetProperty("output").GetString()!;

    // The output should be truncated to 10,000 chars + "...[truncated]" suffix
    output.Should().EndWith("...[truncated]");
    output.Length.Should().BeLessThan(longOutput.Length,
      "the output should be truncated to well under the original 20,000 characters");
    output.Length.Should().Be(10_000 + "...[truncated]".Length);
  }

  [Fact]
  public async Task WriteFailure_DoesNotThrow()
  {
    // Arrange -- initialize, then dispose the writer to simulate a broken state
    var sut = CreateLogger();
    await sut.InitializeAsync(_sessionId);

    // Dispose the logger to close the underlying writer
    await sut.DisposeAsync();

    // Act -- calling a log method after disposal should not throw because
    // the writer is null and WriteEventAsync returns early
    var act = () => sut.LogUserMessageAsync("This should not throw");

    // Assert
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task DisposeAsync_FlushesAndCloses()
  {
    // Arrange
    var sut = CreateLogger();
    await sut.InitializeAsync(_sessionId);
    await sut.LogUserMessageAsync("Test message for dispose verification");

    // Act
    await sut.DisposeAsync();

    // Assert -- the file should exist and contain the logged event
    File.Exists(LogFilePath).Should().BeTrue();
    var content = await File.ReadAllTextAsync(LogFilePath);
    content.Should().NotBeNullOrWhiteSpace("the file should contain the flushed event data");

    using var doc = JsonDocument.Parse(content.Trim());
    doc.RootElement.GetProperty("type").GetString().Should().Be("user_message");
  }
}
