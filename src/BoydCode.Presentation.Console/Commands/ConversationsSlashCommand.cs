using System.Globalization;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.SlashCommands;
using BoydCode.Presentation.Console.Terminal;
using Spectre.Console;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using TguiApp = Terminal.Gui.App.Application;

#pragma warning disable CS0618 // Application.Invoke - using legacy static API during Terminal.Gui migration

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

    var sorted = sessions
        .OrderByDescending(s => s.LastAccessedAt)
        .Take(20)
        .ToList();

    var spectreUi = _ui as SpectreUserInterface;
    if (spectreUi?.Toplevel is not null)
    {
      ShowInteractiveList(spectreUi, sorted);
      return;
    }

    // Fallback: inline text output for non-interactive mode
    if (sorted.Count == 0)
    {
      SpectreHelpers.OutputMarkup("No saved conversations found.");
      return;
    }

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

  private void ShowInteractiveList(SpectreUserInterface spectreUi, List<Session> sessions)
  {
    var toplevel = spectreUi.Toplevel!;

    var actions = new List<ActionDefinition<Session>>
    {
      new(
        Key.Enter, "Show",
        item => { if (item is not null) _ = HandleShowAsync(["/conversations", "show", item.Id], default); },
        IsPrimary: true),
      new(
        Key.R, "Rename",
        item => { if (item is not null) _ = HandleRenameAsync(["/conversations", "rename", item.Id], default); },
        HotkeyDisplay: "r"),
      new(
        Key.D, "Delete",
        item =>
        {
          if (item is null) return;
          _ = HandleDeleteAsync(["/conversations", "delete", item.Id], default);
        },
        HotkeyDisplay: "d"),
    };

    var window = new InteractiveListWindow<Session>(
      "Conversations",
      sessions,
      FormatSessionRow,
      actions,
      columnHeader: "Name                     Messages  Created",
      emptyMessage: "No saved conversations found.");

    window.CloseRequested += () =>
    {
      TguiApp.Invoke(() =>
      {
        toplevel.Remove(window);
        window.Dispose();
        toplevel.InputView.SetFocus();
      });
    };

    TguiApp.Invoke(() =>
    {
      toplevel.Add(window);
      window.SetFocus();
    });
  }

  private string FormatSessionRow(Session session, int rowWidth)
  {
    var nameOrId = session.Name ?? session.Id;
    var msgCount = session.Conversation.Messages.Count
        .ToString(CultureInfo.InvariantCulture);
    var created = session.CreatedAt.LocalDateTime
        .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    if (nameOrId.Length > 25)
    {
      nameOrId = string.Concat(nameOrId.AsSpan(0, 22), "...");
    }

    return $"{nameOrId,-25}{msgCount,8}  {created}";
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

    var sections = new List<DetailSection>();

    // Info section
    var infoRows = new List<DetailRow>
    {
      new("Session", session.Id),
    };

    if (session.Name is not null)
    {
      infoRows.Add(new DetailRow("Name", session.Name));
    }

    infoRows.Add(new DetailRow("Created",
      session.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
    infoRows.Add(new DetailRow("Last used",
      session.LastAccessedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));
    infoRows.Add(new DetailRow("Project", session.ProjectName ?? "(none)",
      Style: session.ProjectName is null ? DetailValueStyle.Muted : DetailValueStyle.Auto));
    infoRows.Add(new DetailRow("Messages",
      session.Conversation.Messages.Count.ToString(CultureInfo.InvariantCulture)));
    infoRows.Add(new DetailRow("Directory", session.WorkingDirectory));

    sections.Add(new DetailSection(null, infoRows));

    // Recent messages section
    var messages = session.Conversation.Messages;
    if (messages.Count > 0)
    {
      var messageRows = new List<DetailRow>();

      foreach (var msg in messages.Take(5))
      {
        var roleLabel = msg.Role switch
        {
          MessageRole.User => "user",
          MessageRole.Assistant => "assistant",
          _ => "system",
        };

        var text = GetMessageText(msg, 120);
        messageRows.Add(new DetailRow($"{roleLabel}:", text, Style: DetailValueStyle.Default));
      }

      if (messages.Count > 5)
      {
        var remaining = messages.Count - 5;
        messageRows.Add(new DetailRow("",
          $"... {remaining.ToString(CultureInfo.InvariantCulture)} more message(s)",
          Style: DetailValueStyle.Muted));
      }

      sections.Add(new DetailSection("Recent messages", messageRows));
    }

    // Resume hint section
    sections.Add(new DetailSection(null, [
      new DetailRow("Resume with", $"boydcode --resume {session.Id}", Style: DetailValueStyle.Muted),
    ]));

    _ui.ShowDetailModal($"Conversation: {sessionId}", sections);
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
      if (SpectreUserInterface.Current?.Toplevel is not null)
      {
        var result = new FormDialogBuilder()
          .SetTitle("Rename Conversation")
          .AddTextField("Name", defaultValue: session.Name, validate: v =>
            string.IsNullOrWhiteSpace(v) ? "Name cannot be empty." : null)
          .Show();

        if (result is null)
        {
          SpectreHelpers.Cancelled();
          return;
        }

        name = result.Values["Name"];
      }
      else
      {
        name = SpectreHelpers.PromptNonEmpty("  Name: ");
      }
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

    if (SpectreUserInterface.Current?.Toplevel is not null)
    {
      var messageCount = session.Conversation.Messages.Count;
      var message = string.Format(
          CultureInfo.InvariantCulture,
          "Delete conversation '{0}'?\n\n  * Messages: {1}\n  * Project: {2}",
          sessionId,
          messageCount.ToString(CultureInfo.InvariantCulture),
          session.ProjectName ?? "(none)");

      var result = MessageBox.Query(TguiApp.Instance, "Delete Conversation", message, "Cancel", "Delete");
      if (result != 1)
      {
        SpectreHelpers.Cancelled();
        return;
      }
    }
    else if (_ui.IsInteractive)
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
