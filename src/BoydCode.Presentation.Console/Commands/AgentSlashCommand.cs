using System.Globalization;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Entities;
using BoydCode.Domain.SlashCommands;
using BoydCode.Presentation.Console.Terminal;
using Spectre.Console;
using Terminal.Gui.Input;
using TguiApp = Terminal.Gui.App.Application;

#pragma warning disable CS0618 // Application.Invoke - using legacy static API during Terminal.Gui migration

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
        SpectreHelpers.Error($"Unknown subcommand '{subcommand}'.");
        SpectreHelpers.Usage("/agent list|show <name>");
        break;
    }

    return Task.FromResult(true);
  }

  private void HandleList()
  {
    var agents = _agentRegistry.GetAll();

    var spectreUi = _ui as SpectreUserInterface;
    if (spectreUi?.Toplevel is not null)
    {
      ShowInteractiveList(spectreUi, agents);
      return;
    }

    // Fallback: inline text output for non-interactive mode
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

  private void ShowInteractiveList(SpectreUserInterface spectreUi, IReadOnlyList<AgentDefinition> agents)
  {
    var toplevel = spectreUi.Toplevel!;

    var actions = new List<ActionDefinition<AgentDefinition>>
    {
      new(
        Key.Enter, "Show",
        item => { if (item is not null) HandleShow(item.Name); },
        IsPrimary: true),
    };

    var window = new InteractiveListWindow<AgentDefinition>(
      "Agents",
      agents,
      FormatAgentRow,
      actions,
      columnHeader: "Name              Description               Scope      Model",
      emptyMessage: "No agents found.",
      emptyHint: "Add agent definitions as .md files in ~/.boydcode/agents/ or .boydcode/agents/");

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

  private static string FormatAgentRow(AgentDefinition agent, int rowWidth)
  {
    var name = agent.Name;
    if (name.Length > 18)
    {
      name = string.Concat(name.AsSpan(0, 15), "...");
    }

    var description = agent.Description;
    if (description.Length > 26)
    {
      description = string.Concat(description.AsSpan(0, 23), "...");
    }

    var scope = agent.Scope.ToString();
    var model = agent.ModelOverride ?? "-";
    if (model.Length > 15)
    {
      model = string.Concat(model.AsSpan(0, 12), "...");
    }

    return $"{name,-18}{description,-26}{scope,-11}{model}";
  }

  private void HandleShow(string? name)
  {
    if (string.IsNullOrEmpty(name))
    {
      SpectreHelpers.Error("Usage: /agent show <name>");
      return;
    }

    var agent = _agentRegistry.GetByName(name);
    if (agent is null)
    {
      SpectreHelpers.Error($"Agent '{name}' not found.");
      return;
    }

    var sections = new List<DetailSection>();

    // Info section
    var infoRows = new List<DetailRow>
    {
      new("Name", agent.Name),
      new("Description", agent.Description, Style: DetailValueStyle.Default),
      new("Scope", agent.Scope.ToString()),
      new("Model", agent.ModelOverride ?? "default",
        Style: agent.ModelOverride is null ? DetailValueStyle.Muted : DetailValueStyle.Auto),
      new("Max Turns", agent.MaxTurns?.ToString(CultureInfo.InvariantCulture) ?? "default (25)",
        Style: agent.MaxTurns is null ? DetailValueStyle.Muted : DetailValueStyle.Auto),
      new("Source", agent.SourcePath),
    };

    sections.Add(new DetailSection(null, infoRows));

    // Instructions section: full text, no truncation (window scrolls)
    sections.Add(new DetailSection("Instructions", [
      new DetailRow("", agent.Instructions, IsMultiLine: true),
    ]));

    _ui.ShowDetailModal($"Agent: {agent.Name}", sections);
  }
}
