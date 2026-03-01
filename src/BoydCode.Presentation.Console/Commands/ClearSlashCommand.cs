using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.SlashCommands;

namespace BoydCode.Presentation.Console.Commands;

public sealed class ClearSlashCommand : ISlashCommand
{
  private readonly ActiveSession _activeSession;
  private readonly IConversationLogger _conversationLogger;
  private readonly ISessionRepository _sessionRepository;

  public ClearSlashCommand(
      ActiveSession activeSession,
      IConversationLogger conversationLogger,
      ISessionRepository sessionRepository)
  {
    _activeSession = activeSession;
    _conversationLogger = conversationLogger;
    _sessionRepository = sessionRepository;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/clear",
      "Clear conversation history",
      []);

  public async Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    var trimmed = input.Trim();
    if (!trimmed.Equals("/clear", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    var session = _activeSession.Session;
    if (session is null)
    {
      SpectreHelpers.Error("No active session.");
      return true;
    }

    var count = session.Conversation.Clear();
    await _conversationLogger.LogContextClearAsync(count, ct);
    await _sessionRepository.SaveAsync(session, ct);

    SpectreHelpers.Success($"Cleared {count} message(s) from conversation history.");
    return true;
  }
}
