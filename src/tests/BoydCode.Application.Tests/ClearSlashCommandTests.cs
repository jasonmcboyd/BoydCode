using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Entities;
using BoydCode.Presentation.Console.Commands;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class ClearSlashCommandTests
{
  private static ClearSlashCommand CreateSut(
    ActiveSession? activeSession = null,
    IConversationLogger? conversationLogger = null,
    ISessionRepository? sessionRepository = null)
  {
    activeSession ??= new ActiveSession();
    conversationLogger ??= Substitute.For<IConversationLogger>();
    sessionRepository ??= Substitute.For<ISessionRepository>();
    return new ClearSlashCommand(activeSession, conversationLogger, sessionRepository);
  }

  [Fact]
  public async Task TryHandleAsync_NonMatchingInput_ReturnsFalse()
  {
    // Arrange
    var sut = CreateSut();

    // Act
    var result = await sut.TryHandleAsync("/help");

    // Assert
    result.Should().BeFalse();
  }

  [Fact]
  public async Task TryHandleAsync_NoSession_ShowsError_ReturnsTrue()
  {
    // Arrange -- no session set on ActiveSession
    var sut = CreateSut();

    // Act
    var result = await sut.TryHandleAsync("/clear");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task TryHandleAsync_ClearsConversation_SavesSession_ReturnsTrue()
  {
    // Arrange
    var activeSession = new ActiveSession();
    var session = new Session(".");
    session.Conversation.AddUserMessage("Hello");
    session.Conversation.AddAssistantMessage("Hi there");
    session.Conversation.AddUserMessage("How are you?");
    activeSession.Set(session);

    var sessionRepository = Substitute.For<ISessionRepository>();
    var sut = CreateSut(
      activeSession: activeSession,
      sessionRepository: sessionRepository);

    // Act
    var result = await sut.TryHandleAsync("/clear");

    // Assert
    result.Should().BeTrue();
    session.Conversation.Messages.Should().BeEmpty();
    await sessionRepository.Received(1).SaveAsync(session, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task TryHandleAsync_LogsContextClear()
  {
    // Arrange
    var activeSession = new ActiveSession();
    var session = new Session(".");
    session.Conversation.AddUserMessage("Message 1");
    session.Conversation.AddAssistantMessage("Message 2");
    activeSession.Set(session);

    var conversationLogger = Substitute.For<IConversationLogger>();
    var sut = CreateSut(
      activeSession: activeSession,
      conversationLogger: conversationLogger);

    // Act
    await sut.TryHandleAsync("/clear");

    // Assert -- LogContextClearAsync should be called with the count of cleared messages
    await conversationLogger.Received(1)
      .LogContextClearAsync(2, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task TryHandleAsync_EmptyConversation_ClearsZero()
  {
    // Arrange -- session with no messages
    var activeSession = new ActiveSession();
    var session = new Session(".");
    activeSession.Set(session);

    var conversationLogger = Substitute.For<IConversationLogger>();
    var sessionRepository = Substitute.For<ISessionRepository>();
    var sut = CreateSut(
      activeSession: activeSession,
      conversationLogger: conversationLogger,
      sessionRepository: sessionRepository);

    // Act
    var result = await sut.TryHandleAsync("/clear");

    // Assert
    result.Should().BeTrue();
    session.Conversation.Messages.Should().BeEmpty();
    await conversationLogger.Received(1)
      .LogContextClearAsync(0, Arg.Any<CancellationToken>());
    await sessionRepository.Received(1).SaveAsync(session, Arg.Any<CancellationToken>());
  }
}
