using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Entities;
using BoydCode.Presentation.Console.Commands;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class ConversationsSlashCommandTests
{
  private static ConversationsSlashCommand CreateSut(
    ISessionRepository? sessionRepository = null,
    ActiveSession? activeSession = null,
    IUserInterface? ui = null,
    IConversationLogger? conversationLogger = null)
  {
    sessionRepository ??= Substitute.For<ISessionRepository>();
    activeSession ??= new ActiveSession();
    ui ??= Substitute.For<IUserInterface>();
    conversationLogger ??= Substitute.For<IConversationLogger>();
    return new ConversationsSlashCommand(
      sessionRepository,
      activeSession,
      ui,
      conversationLogger);
  }

  // ---------------------------------------------------------------------------
  // TryHandleAsync routing tests
  // ---------------------------------------------------------------------------

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
  public async Task TryHandleAsync_BareConversations_ShowsUsage_ReturnsTrue()
  {
    // Arrange -- bare /conversations with no subcommand shows usage
    var sut = CreateSut();

    // Act
    var result = await sut.TryHandleAsync("/conversations");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task TryHandleAsync_UnknownSubcommand_ShowsUsage_ReturnsTrue()
  {
    // Arrange
    var sut = CreateSut();

    // Act
    var result = await sut.TryHandleAsync("/conversations foo");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task TryHandleAsync_ListSubcommand_ReturnsTrue()
  {
    // Arrange
    var sessionRepository = Substitute.For<ISessionRepository>();
    sessionRepository.ListAsync(Arg.Any<CancellationToken>())
      .Returns(new List<Session>());
    var sut = CreateSut(sessionRepository: sessionRepository);

    // Act
    var result = await sut.TryHandleAsync("/conversations list");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task TryHandleAsync_ShowSubcommand_ReturnsTrue()
  {
    // Arrange
    var sut = CreateSut();

    // Act -- missing id shows usage but still returns true
    var result = await sut.TryHandleAsync("/conversations show");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task TryHandleAsync_DeleteSubcommand_ReturnsTrue()
  {
    // Arrange
    var sut = CreateSut();

    // Act -- missing id shows usage but still returns true
    var result = await sut.TryHandleAsync("/conversations delete");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task TryHandleAsync_RenameSubcommand_ReturnsTrue()
  {
    // Arrange
    var sut = CreateSut();

    // Act -- missing id shows usage but still returns true
    var result = await sut.TryHandleAsync("/conversations rename");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task TryHandleAsync_ClearSubcommand_ReturnsTrue()
  {
    // Arrange -- no session set, shows error but returns true
    var sut = CreateSut();

    // Act
    var result = await sut.TryHandleAsync("/conversations clear");

    // Assert
    result.Should().BeTrue();
  }

  // ---------------------------------------------------------------------------
  // HandleClear tests (migrated from ClearSlashCommandTests)
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task HandleClear_NoSession_ShowsError_ReturnsTrue()
  {
    // Arrange -- no session set on ActiveSession
    var sut = CreateSut();

    // Act
    var result = await sut.TryHandleAsync("/conversations clear");

    // Assert
    result.Should().BeTrue();
  }

  [Fact]
  public async Task HandleClear_ClearsConversation_SavesSession()
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
    var result = await sut.TryHandleAsync("/conversations clear");

    // Assert
    result.Should().BeTrue();
    session.Conversation.Messages.Should().BeEmpty();
    await sessionRepository.Received(1).SaveAsync(session, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task HandleClear_LogsContextClear()
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
    await sut.TryHandleAsync("/conversations clear");

    // Assert -- LogContextClearAsync should be called with the count of cleared messages
    await conversationLogger.Received(1)
      .LogContextClearAsync(2, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task HandleClear_EmptyConversation_ClearsZero()
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
    var result = await sut.TryHandleAsync("/conversations clear");

    // Assert
    result.Should().BeTrue();
    session.Conversation.Messages.Should().BeEmpty();
    await conversationLogger.Received(1)
      .LogContextClearAsync(0, Arg.Any<CancellationToken>());
    await sessionRepository.Received(1).SaveAsync(session, Arg.Any<CancellationToken>());
  }

  // ---------------------------------------------------------------------------
  // HandleList tests
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task HandleList_NoSessions_DoesNotThrow()
  {
    // Arrange
    var sessionRepository = Substitute.For<ISessionRepository>();
    sessionRepository.ListAsync(Arg.Any<CancellationToken>())
      .Returns(new List<Session>());
    var sut = CreateSut(sessionRepository: sessionRepository);

    // Act
    var act = () => sut.TryHandleAsync("/conversations list");

    // Assert
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task HandleList_CallsListAsync()
  {
    // Arrange
    var sessionRepository = Substitute.For<ISessionRepository>();
    sessionRepository.ListAsync(Arg.Any<CancellationToken>())
      .Returns(new List<Session>());
    var sut = CreateSut(sessionRepository: sessionRepository);

    // Act
    await sut.TryHandleAsync("/conversations list");

    // Assert
    await sessionRepository.Received(1).ListAsync(Arg.Any<CancellationToken>());
  }

  // ---------------------------------------------------------------------------
  // HandleShow tests
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task HandleShow_MissingId_DoesNotThrow()
  {
    // Arrange -- no session id argument
    var sut = CreateSut();

    // Act
    var act = () => sut.TryHandleAsync("/conversations show");

    // Assert
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task HandleShow_SessionNotFound_DoesNotThrow()
  {
    // Arrange
    var sessionRepository = Substitute.For<ISessionRepository>();
    sessionRepository.LoadAsync("nonexistent", Arg.Any<CancellationToken>())
      .Returns((Session?)null);
    var sut = CreateSut(sessionRepository: sessionRepository);

    // Act
    var act = () => sut.TryHandleAsync("/conversations show nonexistent");

    // Assert
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task HandleShow_ValidSession_LoadsSession()
  {
    // Arrange
    var session = new Session(".");
    session.Conversation.AddUserMessage("Hello");
    var sessionRepository = Substitute.For<ISessionRepository>();
    sessionRepository.LoadAsync(session.Id, Arg.Any<CancellationToken>())
      .Returns(session);
    var sut = CreateSut(sessionRepository: sessionRepository);

    // Act
    await sut.TryHandleAsync($"/conversations show {session.Id}");

    // Assert
    await sessionRepository.Received(1).LoadAsync(session.Id, Arg.Any<CancellationToken>());
  }

  // ---------------------------------------------------------------------------
  // HandleRename tests
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task HandleRename_MissingId_DoesNotThrow()
  {
    // Arrange
    var sut = CreateSut();

    // Act
    var act = () => sut.TryHandleAsync("/conversations rename");

    // Assert
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task HandleRename_SessionNotFound_DoesNotThrow()
  {
    // Arrange
    var sessionRepository = Substitute.For<ISessionRepository>();
    sessionRepository.LoadAsync("bad", Arg.Any<CancellationToken>())
      .Returns((Session?)null);
    var sut = CreateSut(sessionRepository: sessionRepository);

    // Act
    var act = () => sut.TryHandleAsync("/conversations rename bad newname");

    // Assert
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task HandleRename_WithInlineName_SetsNameAndSaves()
  {
    // Arrange
    var session = new Session(".");
    var sessionRepository = Substitute.For<ISessionRepository>();
    sessionRepository.LoadAsync(session.Id, Arg.Any<CancellationToken>())
      .Returns(session);
    var sut = CreateSut(sessionRepository: sessionRepository);

    // Act
    await sut.TryHandleAsync($"/conversations rename {session.Id} My Session Name");

    // Assert
    session.Name.Should().Be("My Session Name");
    await sessionRepository.Received(1).SaveAsync(session, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task HandleRename_NoInlineName_NonInteractive_ShowsUsage()
  {
    // Arrange -- non-interactive UI should show usage when no name provided
    var session = new Session(".");
    var sessionRepository = Substitute.For<ISessionRepository>();
    sessionRepository.LoadAsync(session.Id, Arg.Any<CancellationToken>())
      .Returns(session);
    var ui = Substitute.For<IUserInterface>();
    ui.IsInteractive.Returns(false);
    var sut = CreateSut(sessionRepository: sessionRepository, ui: ui);

    // Act
    await sut.TryHandleAsync($"/conversations rename {session.Id}");

    // Assert -- should not have saved since no name was provided in non-interactive mode
    await sessionRepository.DidNotReceive().SaveAsync(session, Arg.Any<CancellationToken>());
  }

  // ---------------------------------------------------------------------------
  // HandleDelete tests
  // ---------------------------------------------------------------------------

  [Fact]
  public async Task HandleDelete_MissingId_DoesNotThrow()
  {
    // Arrange
    var sut = CreateSut();

    // Act
    var act = () => sut.TryHandleAsync("/conversations delete");

    // Assert
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task HandleDelete_ActiveSession_DoesNotDelete()
  {
    // Arrange -- attempting to delete the current active session should be blocked
    var activeSession = new ActiveSession();
    var session = new Session(".");
    activeSession.Set(session);

    var sessionRepository = Substitute.For<ISessionRepository>();
    var sut = CreateSut(
      activeSession: activeSession,
      sessionRepository: sessionRepository);

    // Act
    await sut.TryHandleAsync($"/conversations delete {session.Id}");

    // Assert -- should not attempt to delete
    await sessionRepository.DidNotReceive().DeleteAsync(session.Id, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task HandleDelete_SessionNotFound_DoesNotDelete()
  {
    // Arrange
    var sessionRepository = Substitute.For<ISessionRepository>();
    sessionRepository.LoadAsync("nonexistent", Arg.Any<CancellationToken>())
      .Returns((Session?)null);
    var sut = CreateSut(sessionRepository: sessionRepository);

    // Act
    await sut.TryHandleAsync("/conversations delete nonexistent");

    // Assert
    await sessionRepository.DidNotReceive().DeleteAsync("nonexistent", Arg.Any<CancellationToken>());
  }

  // ---------------------------------------------------------------------------
  // Descriptor tests
  // ---------------------------------------------------------------------------

  [Fact]
  public void Descriptor_HasCorrectPrefix()
  {
    // Arrange
    var sut = CreateSut();

    // Act & Assert
    sut.Descriptor.Prefix.Should().Be("/conversations");
  }

  [Fact]
  public void Descriptor_HasAllSubcommands()
  {
    // Arrange
    var sut = CreateSut();

    // Act
    var subcommandNames = sut.Descriptor.Subcommands
      .Select(s => s.Usage.Split(' ')[0])
      .ToList();

    // Assert
    subcommandNames.Should().Contain("list");
    subcommandNames.Should().Contain("show");
    subcommandNames.Should().Contain("rename");
    subcommandNames.Should().Contain("delete");
    subcommandNames.Should().Contain("clear");
  }
}
