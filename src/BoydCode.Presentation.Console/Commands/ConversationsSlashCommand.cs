using System.Globalization;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Enums;
using BoydCode.Domain.SlashCommands;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed class ConversationsSlashCommand : ISlashCommand
{
  private readonly ISessionRepository _sessionRepository;
  private readonly ActiveSession _activeSession;
  private readonly IUserInterface _ui;
  private readonly IConversationLogger _conversationLogger;

  public ConversationsSlashCommand(
      ISessionRepository sessionRepository,
      ActiveSession activeSession,
      IUserInterface ui,
      IConversationLogger conversationLogger)
  {
    _sessionRepository = sessionRepository;
    _activeSession = activeSession;
    _ui = ui;
    _conversationLogger = conversationLogger;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/conversations",
      "Manage conversations and sessions",
      [
          new("list", "List recent conversations"),
          new("show [id]", "Show conversation details"),
          new("rename [id] [name]", "Rename a conversation"),
          new("delete [id]", "Delete a saved conversation"),
          new("clear", "Clear conversation history"),
      ]);

  public async Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (tokens.Length == 0 || !tokens[0].Equals("/conversations", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    var subcommand = tokens.Length > 1 ? tokens[1].ToLowerInvariant() : string.Empty;

    switch (subcommand)
    {
      case "list":
        await HandleListAsync(ct);
        break;
      case "show":
        await HandleShowAsync(tokens, ct);
        break;
      case "rename":
        await HandleRenameAsync(tokens, ct);
        break;
      case "delete":
        await HandleDeleteAsync(tokens, ct);
        break;
      case "clear":
        await HandleClearAsync(ct);
        break;
      default:
        SpectreHelpers.Usage("/conversations list|show|rename|delete|clear");
        break;
    }

    return true;
  }

  private async Task HandleListAsync(CancellationToken ct)
  {
    var sessions = await _sessionRepository.ListAsync(ct);

    if (sessions.Count == 0)
    {
      SpectreHelpers.OutputMarkup("No saved conversations found.");
      return;
    }

    var sorted = sessions
        .OrderByDescending(s => s.LastAccessedAt)
        .Take(20)
        .ToList();

    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup($"{"ID",-12}{"Name",-18}{"Project",-14}{"Msgs",5}  {"Last accessed",-18}{"Preview"}");
    SpectreHelpers.OutputMarkup(new string('\u2500', 90));

    foreach (var session in sorted)
    {
      var isCurrent = _activeSession.Session?.Id == session.Id;
      var idDisplay = isCurrent
          ? $"{session.Id} *"
          : session.Id;

      var nameDisplay = session.Name ?? "--";
      var projectDisplay = session.ProjectName ?? "--";

      var messageCount = session.Conversation.Messages.Count
          .ToString(CultureInfo.InvariantCulture);

      var lastAccessed = session.LastAccessedAt.LocalDateTime
          .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

      var preview = session.Name is not null
          ? "--"
          : GetFirstMessagePreviewPlain(session, 30);

      SpectreHelpers.OutputMarkup(
        $"{Markup.Escape(idDisplay),-12}" +
        $"{Markup.Escape(nameDisplay),-18}" +
        $"{Markup.Escape(projectDisplay),-14}" +
        $"{messageCount,5}  " +
        $"{lastAccessed,-18}" +
        $"{Markup.Escape(preview)}");
    }

    if (_activeSession.Session is not null)
    {
      SpectreHelpers.OutputLine();
      SpectreHelpers.Dim("  * = current session");
    }

    SpectreHelpers.OutputLine();
  }

  private async Task HandleShowAsync(string[] tokens, CancellationToken ct)
  {
    if (tokens.Length <= 2)
    {
      SpectreHelpers.Usage("/conversations show <id>");
      return;
    }

    var sessionId = tokens[2];
    var session = await _sessionRepository.LoadAsync(sessionId, ct);

    if (session is null)
    {
      SpectreHelpers.Error($"Session '{sessionId}' not found.");
      return;
    }

    SpectreHelpers.OutputLine();

    // Info rows
    SpectreHelpers.OutputMarkup($"  [dim]{"Session",-14}[/][cyan]{Markup.Escape(session.Id)}[/]");
    if (session.Name is not null)
    {
      SpectreHelpers.OutputMarkup($"  [dim]{"Name",-14}[/][cyan]{Markup.Escape(session.Name)}[/]");
    }
    SpectreHelpers.OutputMarkup(
      $"  [dim]{"Created",-14}[/][cyan]{session.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}[/]" +
      $"    [dim]{"Last used",-12}[/][cyan]{session.LastAccessedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}[/]");
    SpectreHelpers.OutputMarkup(
      $"  [dim]{"Project",-14}[/][cyan]{Markup.Escape(session.ProjectName ?? "(none)")}[/]" +
      $"    [dim]{"Messages",-12}[/][cyan]{session.Conversation.Messages.Count.ToString(CultureInfo.InvariantCulture)}[/]");
    SpectreHelpers.OutputMarkup($"  [dim]{"Directory",-14}[/][cyan]{Markup.Escape(session.WorkingDirectory)}[/]");

    // Show first 5 messages as preview
    var messages = session.Conversation.Messages;
    if (messages.Count > 0)
    {
      SpectreHelpers.OutputLine();
      SpectreHelpers.OutputMarkup("  [dim]Recent messages[/]");
      SpectreHelpers.OutputLine();

      var previewMessages = messages.Take(5);
      foreach (var msg in previewMessages)
      {
        var roleLabel = msg.Role switch
        {
          MessageRole.User => "[blue]user[/]",
          MessageRole.Assistant => "[green]assistant[/]",
          _ => "[dim]system[/]",
        };

        var text = GetMessageText(msg, 120);
        SpectreHelpers.OutputMarkup($"    {roleLabel}: {Markup.Escape(text)}");
      }

      if (messages.Count > 5)
      {
        var remaining = messages.Count - 5;
        SpectreHelpers.OutputMarkup($"    [dim]... {remaining.ToString(CultureInfo.InvariantCulture)} more message(s)[/]");
      }
    }

    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup($"  [dim]Resume with:[/] boydcode --resume {Markup.Escape(session.Id)}");
    SpectreHelpers.OutputLine();
  }

  private async Task HandleRenameAsync(string[] tokens, CancellationToken ct)
  {
    if (tokens.Length <= 2)
    {
      SpectreHelpers.Usage("/conversations rename <id> [name]");
      return;
    }

    var sessionId = tokens[2];
    var session = await _sessionRepository.LoadAsync(sessionId, ct);

    if (session is null)
    {
      SpectreHelpers.Error($"Session '{sessionId}' not found.");
      return;
    }

    string name;
    if (tokens.Length > 3)
    {
      name = string.Join(' ', tokens.Skip(3));
    }
    else if (_ui.IsInteractive)
    {
      name = SpectreHelpers.PromptNonEmpty("  Name: ");
    }
    else
    {
      SpectreHelpers.Usage("/conversations rename <id> <name>");
      return;
    }

    session.Name = name;
    await _sessionRepository.SaveAsync(session, ct);
    SpectreHelpers.Success($"Session '{Markup.Escape(sessionId)}' renamed to '{Markup.Escape(name)}'.");
  }

  private async Task HandleDeleteAsync(string[] tokens, CancellationToken ct)
  {
    if (tokens.Length <= 2)
    {
      SpectreHelpers.Usage("/conversations delete <id>");
      return;
    }

    var sessionId = tokens[2];

    if (_activeSession.Session?.Id == sessionId)
    {
      SpectreHelpers.Error("Cannot delete the current active session.");
      return;
    }

    var session = await _sessionRepository.LoadAsync(sessionId, ct);
    if (session is null)
    {
      SpectreHelpers.Error($"Session '{sessionId}' not found.");
      return;
    }

    if (_ui.IsInteractive)
    {
      var messageCount = session.Conversation.Messages.Count;
      SpectreHelpers.OutputMarkup(
          $"  Delete session [bold]{Markup.Escape(sessionId)}[/] " +
          $"({messageCount.ToString(CultureInfo.InvariantCulture)} messages, " +
          $"project: {Markup.Escape(session.ProjectName ?? "(none)")})?");

      if (!SpectreHelpers.Confirm("  Delete?", defaultValue: false))
      {
        SpectreHelpers.Cancelled();
        return;
      }
    }

    await _sessionRepository.DeleteAsync(sessionId, ct);
    SpectreHelpers.Success($"Session '{Markup.Escape(sessionId)}' deleted.");
  }

  private async Task HandleClearAsync(CancellationToken ct)
  {
    var session = _activeSession.Session;
    if (session is null)
    {
      SpectreHelpers.Error("No active session.");
      return;
    }

    var count = session.Conversation.Clear();
    await _conversationLogger.LogContextClearAsync(count, ct);
    await _sessionRepository.SaveAsync(session, ct);

    SpectreHelpers.Success($"Cleared {count} message(s) from conversation history.");
  }

  private static string GetFirstMessagePreviewPlain(Domain.Entities.Session session, int maxLength)
  {
    var firstUserMessage = session.Conversation.Messages
        .FirstOrDefault(m => m.Role == MessageRole.User);

    if (firstUserMessage is null)
    {
      return "--";
    }

    return GetMessageText(firstUserMessage, maxLength);
  }

  private static string GetMessageText(Domain.Entities.ConversationMessage message, int maxLength)
  {
    var textBlock = message.Content.OfType<TextBlock>().FirstOrDefault();
    if (textBlock is null)
    {
      var toolUse = message.Content.OfType<ToolUseBlock>().FirstOrDefault();
      if (toolUse is not null)
      {
        return $"[tool: {toolUse.Name}]";
      }

      var toolResult = message.Content.OfType<ToolResultBlock>().FirstOrDefault();
      if (toolResult is not null)
      {
        return "[tool result]";
      }

      return "--";
    }

    var text = textBlock.Text.ReplaceLineEndings(" ").Trim();
    if (text.Length > maxLength)
    {
      text = string.Concat(text.AsSpan(0, maxLength - 3), "...");
    }

    return text;
  }
}
