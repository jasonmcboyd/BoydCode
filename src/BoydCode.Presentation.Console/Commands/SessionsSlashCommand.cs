using System.Globalization;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Enums;
using BoydCode.Domain.SlashCommands;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed class SessionsSlashCommand : ISlashCommand
{
  private readonly ISessionRepository _sessionRepository;
  private readonly ActiveSession _activeSession;
  private readonly IUserInterface _ui;

  public SessionsSlashCommand(
      ISessionRepository sessionRepository,
      ActiveSession activeSession,
      IUserInterface ui)
  {
    _sessionRepository = sessionRepository;
    _activeSession = activeSession;
    _ui = ui;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/sessions",
      "Manage saved sessions",
      [
          new("list", "List recent sessions"),
          new("show [id]", "Show session details"),
          new("delete [id]", "Delete a saved session"),
      ]);

  public async Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (tokens.Length == 0 || !tokens[0].Equals("/sessions", StringComparison.OrdinalIgnoreCase))
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
      case "delete":
        await HandleDeleteAsync(tokens, ct);
        break;
      default:
        SpectreHelpers.Usage("/sessions list|show|delete");
        break;
    }

    return true;
  }

  private async Task HandleListAsync(CancellationToken ct)
  {
    var sessions = await _sessionRepository.ListAsync(ct);

    if (sessions.Count == 0)
    {
      SpectreHelpers.OutputMarkup("No saved sessions found.");
      return;
    }

    var sorted = sessions
        .OrderByDescending(s => s.LastAccessedAt)
        .Take(20)
        .ToList();

    var table = SpectreHelpers.SimpleTable("ID", "Project", "Messages", "Last accessed", "Preview");
    table.Columns[2].RightAligned();

    foreach (var session in sorted)
    {
      var isCurrent = _activeSession.Session?.Id == session.Id;
      var idDisplay = isCurrent
          ? $"[green]{Markup.Escape(session.Id)}[/] [dim]*[/]"
          : Markup.Escape(session.Id);

      var projectDisplay = session.ProjectName is not null
          ? Markup.Escape(session.ProjectName)
          : "[dim]--[/]";

      var messageCount = session.Conversation.Messages.Count
          .ToString(CultureInfo.InvariantCulture);

      var lastAccessed = session.LastAccessedAt.LocalDateTime
          .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

      var preview = GetFirstMessagePreview(session, 60);

      table.AddRow(idDisplay, projectDisplay, messageCount, lastAccessed, preview);
    }

    SpectreHelpers.OutputRenderable(table);

    if (_activeSession.Session is not null)
    {
      SpectreHelpers.OutputLine();
      SpectreHelpers.Dim("  * = current session");
    }
  }

  private async Task HandleShowAsync(string[] tokens, CancellationToken ct)
  {
    if (tokens.Length <= 2)
    {
      SpectreHelpers.Usage("/sessions show <id>");
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

    var grid = SpectreHelpers.InfoGrid();
    SpectreHelpers.AddInfoRow(grid, "Session", session.Id);
    SpectreHelpers.AddInfoRow(grid,
        "Created", session.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        "Last used", session.LastAccessedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
    SpectreHelpers.AddInfoRow(grid,
        "Project", session.ProjectName ?? "(none)",
        "Messages", session.Conversation.Messages.Count.ToString(CultureInfo.InvariantCulture));
    SpectreHelpers.AddInfoRow(grid, "Directory", session.WorkingDirectory);
    SpectreHelpers.OutputRenderable(grid);

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

  private async Task HandleDeleteAsync(string[] tokens, CancellationToken ct)
  {
    if (tokens.Length <= 2)
    {
      SpectreHelpers.Usage("/sessions delete <id>");
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
    SpectreHelpers.Success($"Session '{sessionId}' deleted.");
  }

  private static string GetFirstMessagePreview(Domain.Entities.Session session, int maxLength)
  {
    var firstUserMessage = session.Conversation.Messages
        .FirstOrDefault(m => m.Role == MessageRole.User);

    if (firstUserMessage is null)
    {
      return "[dim]--[/]";
    }

    var text = GetMessageText(firstUserMessage, maxLength);
    return $"[dim]{Markup.Escape(text)}[/]";
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
