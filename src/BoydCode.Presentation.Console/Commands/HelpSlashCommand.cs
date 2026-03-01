using BoydCode.Application.Interfaces;
using BoydCode.Domain.SlashCommands;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed class HelpSlashCommand : ISlashCommand
{
  private readonly ISlashCommandRegistry _registry;

  public HelpSlashCommand(ISlashCommandRegistry registry)
  {
    _registry = registry;
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

    var table = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Blue);
    table.AddColumn("[bold]Command[/]");
    table.AddColumn("[bold]Description[/]");

    // Built-in commands (not in registry — they live in the AgentOrchestrator loop)
    table.AddRow("/quit", "Exit the session");
    table.AddRow("/exit", "Exit the session");

    // Registered commands
    foreach (var descriptor in _registry.GetAllDescriptors())
    {
      table.AddRow(
          Markup.Escape(descriptor.Prefix),
          Markup.Escape(descriptor.Description));

      foreach (var sub in descriptor.Subcommands)
      {
        table.AddRow(
            $"  [dim]{Markup.Escape(descriptor.Prefix)} {Markup.Escape(sub.Usage)}[/]",
            $"[dim]{Markup.Escape(sub.Description)}[/]");
      }
    }

    AnsiConsole.Write(table);
    return Task.FromResult(true);
  }
}
