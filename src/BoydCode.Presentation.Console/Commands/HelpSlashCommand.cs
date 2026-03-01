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

    var sb = new StringBuilder();

    // Built-in commands (not in registry -- they live in the AgentOrchestrator loop)
    AppendCommand(sb, "/quit", "Exit the session (also: /exit)");

    // Registered commands (skip /help -- rendered last)
    foreach (var descriptor in _registry.GetAllDescriptors())
    {
      if (descriptor.Prefix.Equals("/help", StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      AppendCommand(sb, descriptor.Prefix, descriptor.Description);

      foreach (var sub in descriptor.Subcommands)
      {
        AppendSubcommand(sb, sub.Usage, sub.Description);
      }
    }

    // /help always last
    AppendCommand(sb, "/help", "Show available commands");

    _ui.ShowModal("Help", sb.ToString().TrimEnd());
    return Task.FromResult(true);
  }

  private static void AppendCommand(StringBuilder sb, string command, string description)
  {
    sb.Append(command.PadRight(24));
    sb.AppendLine(description);
  }

  private static void AppendSubcommand(StringBuilder sb, string usage, string description)
  {
    sb.Append("  ");
    sb.Append(usage.PadRight(22));
    sb.AppendLine(description);
  }
}
