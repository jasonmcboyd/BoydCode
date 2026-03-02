using System.Globalization;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.SlashCommands;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed class AgentSlashCommand : ISlashCommand
{
  private readonly IAgentRegistry _agentRegistry;
  private readonly IUserInterface _ui;

  public AgentSlashCommand(IAgentRegistry agentRegistry, IUserInterface ui)
  {
    _agentRegistry = agentRegistry;
    _ui = ui;
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

    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup($"{"Name",-16}{"Description",-25}{"Scope",-10}{"Model"}");
    SpectreHelpers.OutputMarkup(new string('\u2500', 60));

    foreach (var agent in agents)
    {
      var model = agent.ModelOverride ?? "-";
      SpectreHelpers.OutputMarkup(
        $"{Markup.Escape(agent.Name),-16}" +
        $"{Markup.Escape(agent.Description),-25}" +
        $"{agent.Scope,-10}" +
        $"{Markup.Escape(model)}");
    }

    SpectreHelpers.OutputLine();
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

    var content =
      $"Name:         {agent.Name}\n" +
      $"Description:  {agent.Description}\n" +
      $"Scope:        {agent.Scope}\n" +
      $"Model:        {agent.ModelOverride ?? "default"}\n" +
      $"Max Turns:    {agent.MaxTurns?.ToString(CultureInfo.InvariantCulture) ?? "default (25)"}\n" +
      $"Source:       {agent.SourcePath}\n\n" +
      $"Instructions:\n{instructions}";

    _ui.ShowModal($"Agent: {agent.Name}", content);
  }
}
