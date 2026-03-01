using System.Globalization;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.SlashCommands;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed class AgentSlashCommand : ISlashCommand
{
  private readonly IAgentRegistry _agentRegistry;

  public AgentSlashCommand(IAgentRegistry agentRegistry)
  {
    _agentRegistry = agentRegistry;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/agent",
      "Manage agent definitions",
      [
          new("list", "List available agents"),
          new("show <name>", "Show agent details"),
      ]);

  public Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    if (!input.StartsWith("/agent", StringComparison.OrdinalIgnoreCase))
    {
      return Task.FromResult(false);
    }

    var parts = input["/agent".Length..].Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    var subcommand = parts.Length > 0 ? parts[0].ToLowerInvariant() : "list";
    var argument = parts.Length > 1 ? parts[1].Trim() : null;

    switch (subcommand)
    {
      case "list":
        HandleList();
        break;
      case "show":
        HandleShow(argument);
        break;
      default:
        SpectreHelpers.OutputMarkup($"[red]Unknown subcommand '{Markup.Escape(subcommand)}'. Use /agent list or /agent show <name>.[/]");
        break;
    }

    return Task.FromResult(true);
  }

  private void HandleList()
  {
    var agents = _agentRegistry.GetAll();
    if (agents.Count == 0)
    {
      SpectreHelpers.OutputMarkup("[yellow]No agents found.[/]");
      SpectreHelpers.OutputMarkup("[dim]Add agent definitions as markdown files:[/]");
      SpectreHelpers.OutputMarkup("[dim]  User:    ~/.boydcode/agents/<name>.md[/]");
      SpectreHelpers.OutputMarkup("[dim]  Project: .boydcode/agents/<name>.md[/]");
      return;
    }

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Name")
        .AddColumn("Description")
        .AddColumn("Scope")
        .AddColumn("Model");

    foreach (var agent in agents)
    {
      table.AddRow(
          Markup.Escape(agent.Name),
          Markup.Escape(agent.Description),
          agent.Scope.ToString(),
          agent.ModelOverride ?? "[dim]default[/]");
    }

    SpectreHelpers.OutputRenderable(table);
  }

  private void HandleShow(string? name)
  {
    if (string.IsNullOrEmpty(name))
    {
      SpectreHelpers.OutputMarkup("[red]Usage: /agent show <name>[/]");
      return;
    }

    var agent = _agentRegistry.GetByName(name);
    if (agent is null)
    {
      SpectreHelpers.OutputMarkup($"[red]Agent '{Markup.Escape(name)}' not found.[/]");
      return;
    }

    var instructions = agent.Instructions.Length > 500
        ? string.Concat(agent.Instructions.AsSpan(0, 500), "...")
        : agent.Instructions;

    var grid = new Grid().AddColumn().AddColumn();
    grid.AddRow("[bold]Name[/]", Markup.Escape(agent.Name));
    grid.AddRow("[bold]Description[/]", Markup.Escape(agent.Description));
    grid.AddRow("[bold]Scope[/]", agent.Scope.ToString());
    grid.AddRow("[bold]Model[/]", agent.ModelOverride ?? "default");
    grid.AddRow("[bold]Max Turns[/]", agent.MaxTurns?.ToString(CultureInfo.InvariantCulture) ?? "default (25)");
    grid.AddRow("[bold]Source[/]", Markup.Escape(agent.SourcePath));
    grid.AddRow("[bold]Instructions[/]", Markup.Escape(instructions));

    var panel = new Panel(grid)
        .Header($"[bold]Agent: {Markup.Escape(agent.Name)}[/]")
        .Border(BoxBorder.Rounded);

    SpectreHelpers.OutputRenderable(panel);
  }
}
