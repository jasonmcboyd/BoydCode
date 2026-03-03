using System.Text;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.SlashCommands;

namespace BoydCode.Presentation.Console.Commands;

public sealed class HelpSlashCommand : ISlashCommand
{
  private readonly ISlashCommandRegistry _registry;
  private readonly IUserInterface _ui;

  public HelpSlashCommand(ISlashCommandRegistry registry, IUserInterface ui)
  {
    _registry = registry;
    _ui = ui;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/help",
      "Show available commands",
      []);

  public Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    var trimmed = input.Trim();
    if (!trimmed.Equals("/help", StringComparison.OrdinalIgnoreCase))
    {
      return Task.FromResult(false);
    }

    var groups = BuildCommandGroups();

    if (_ui.IsInteractive)
    {
      _ui.ShowHelpModal(groups);
    }
    else
    {
      // Non-TUI fallback: build string and show plain modal
      var sb = new StringBuilder();
      foreach (var group in groups)
      {
        sb.Append(group.Prefix.PadRight(24));
        sb.AppendLine(group.Description);
        foreach (var sub in group.Subcommands)
        {
          sb.Append("  ");
          sb.Append(sub.Usage.PadRight(22));
          sb.AppendLine(sub.Description);
        }
      }

      _ui.ShowModal("Help", sb.ToString().TrimEnd());
    }

    return Task.FromResult(true);
  }

  private List<HelpCommandGroup> BuildCommandGroups()
  {
    var groups = new List<HelpCommandGroup>();

    // /quit first
    groups.Add(new HelpCommandGroup("/quit", "Exit the session (also: /exit)", []));

    // Registered commands (skip /help -- rendered last)
    foreach (var descriptor in _registry.GetAllDescriptors())
    {
      if (descriptor.Prefix.Equals("/help", StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      var subcommands = descriptor.Subcommands
          .Select(s => new HelpSubcommand(s.Usage, s.Description))
          .ToList();

      groups.Add(new HelpCommandGroup(descriptor.Prefix, descriptor.Description, subcommands));
    }

    // /help always last
    groups.Add(new HelpCommandGroup("/help", "Show available commands", []));

    return groups;
  }
}
