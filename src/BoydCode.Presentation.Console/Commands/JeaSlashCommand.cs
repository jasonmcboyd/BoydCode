using System.Globalization;
using System.Text.RegularExpressions;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.SlashCommands;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed partial class JeaSlashCommand : ISlashCommand
{
  private readonly IJeaProfileStore _store;
  private readonly JeaProfileComposer _composer;
  private readonly ActiveProject _activeProject;
  private readonly IProjectRepository _projectRepository;

  public JeaSlashCommand(
      IJeaProfileStore store,
      JeaProfileComposer composer,
      ActiveProject activeProject,
      IProjectRepository projectRepository)
  {
    _store = store;
    _composer = composer;
    _activeProject = activeProject;
    _projectRepository = projectRepository;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/jea",
      "Manage JEA profiles",
      [
          new("list", "List all JEA profiles"),
            new("show [name]", "Show profile details"),
            new("create [name]", "Create a new profile"),
            new("edit [name]", "Edit an existing profile"),
            new("delete [name]", "Delete a profile"),
            new("effective", "Show effective config for current session"),
            new("assign [name]", "Assign a profile to current project"),
            new("unassign [name]", "Remove a profile from current project"),
      ]);

  public async Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (tokens.Length == 0 || !tokens[0].Equals("/jea", StringComparison.OrdinalIgnoreCase))
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
      case "create":
        await HandleCreateAsync(tokens, ct);
        break;
      case "edit":
        await HandleEditAsync(tokens, ct);
        break;
      case "delete":
        await HandleDeleteAsync(tokens, ct);
        break;
      case "effective":
        await HandleEffectiveAsync(ct);
        break;
      case "assign":
        await HandleAssignAsync(tokens, ct);
        break;
      case "unassign":
        await HandleUnassignAsync(tokens, ct);
        break;
      default:
        AnsiConsole.MarkupLine("[yellow]Usage:[/] /jea list|show|create|edit|delete|effective|assign|unassign");
        break;
    }

    return true;
  }

  // ──────────────────────────────────────────────
  //  ENSURE GLOBAL
  // ──────────────────────────────────────────────

  private async Task EnsureGlobalProfileAsync(CancellationToken ct)
  {
    var existing = await _store.LoadAsync(BuiltInJeaProfile.GlobalName, ct);
    if (existing is not null)
    {
      return;
    }

    var globalProfile = new JeaProfile(
        Name: BuiltInJeaProfile.GlobalName,
        LanguageMode: BuiltInJeaProfile.Instance.LanguageMode,
        Modules: BuiltInJeaProfile.Instance.Modules,
        Entries: BuiltInJeaProfile.Instance.Entries);

    await _store.SaveAsync(globalProfile, ct);
  }

  // ──────────────────────────────────────────────
  //  LIST
  // ──────────────────────────────────────────────

  private async Task HandleListAsync(CancellationToken ct)
  {
    await EnsureGlobalProfileAsync(ct);

    var names = await _store.ListNamesAsync(ct);

    if (names.Count == 0)
    {
      AnsiConsole.MarkupLine("No JEA profiles found.");
      AnsiConsole.MarkupLine("[dim]Create one with[/] /jea create <name>");
      return;
    }

    var table = new Table()
        .Border(TableBorder.Simple)
        .AddColumn(new TableColumn("[bold]Name[/]"))
        .AddColumn(new TableColumn("[bold]Language Mode[/]"))
        .AddColumn(new TableColumn("[bold]Commands[/]").RightAligned())
        .AddColumn(new TableColumn("[bold]Modules[/]").RightAligned());

    foreach (var name in names)
    {
      var profile = await _store.LoadAsync(name, ct);
      if (profile is null)
      {
        continue;
      }

      var isGlobal = name.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase);
      var displayName = isGlobal
          ? $"{Markup.Escape(name)} [dim](global)[/]"
          : Markup.Escape(name);

      table.AddRow(
          displayName,
          profile.LanguageMode.ToString(),
          profile.Entries.Count.ToString(CultureInfo.InvariantCulture),
          profile.Modules.Count.ToString(CultureInfo.InvariantCulture));
    }

    AnsiConsole.Write(table);
  }

  // ──────────────────────────────────────────────
  //  SHOW
  // ──────────────────────────────────────────────

  private async Task HandleShowAsync(string[] tokens, CancellationToken ct)
  {
    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : await PromptProfileSelectionAsync(ct);

    if (name is null)
    {
      return;
    }

    var profile = await _store.LoadAsync(name, ct);
    if (profile is null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Profile [bold]{Markup.Escape(name)}[/] not found.");
      return;
    }

    var filePath = GetProfileFilePath(name);

    var panel = new Panel(BuildProfileDetail(profile, filePath))
        .Header($"[bold]{Markup.Escape(profile.Name)}[/]")
        .Border(BoxBorder.Rounded)
        .Padding(1, 0);

    AnsiConsole.WriteLine();
    AnsiConsole.Write(panel);
    AnsiConsole.WriteLine();
  }

  // ──────────────────────────────────────────────
  //  CREATE
  // ──────────────────────────────────────────────

  private async Task HandleCreateAsync(string[] tokens, CancellationToken ct)
  {
    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : AnsiConsole.Prompt(
            new TextPrompt<string>("Profile [green]name[/]:")
                .Validate(n => !string.IsNullOrWhiteSpace(n)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Name cannot be empty")));

    if (!ValidateProfileName(name))
    {
      return;
    }

    var existing = await _store.LoadAsync(name, ct);
    if (existing is not null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Profile [bold]{Markup.Escape(name)}[/] already exists.");
      return;
    }

    var languageMode = AnsiConsole.Prompt(
        new SelectionPrompt<PSLanguageModeName>()
            .Title("Language mode:")
            .AddChoices(
                PSLanguageModeName.FullLanguage,
                PSLanguageModeName.ConstrainedLanguage,
                PSLanguageModeName.RestrictedLanguage,
                PSLanguageModeName.NoLanguage));

    var entries = new List<JeaProfileEntry>();
    var modules = new List<string>();

    while (true)
    {
      var action = AnsiConsole.Prompt(
          new SelectionPrompt<string>()
              .Title("Add to profile:")
              .AddChoices("Add command", "Add module", "Done")
              .HighlightStyle(new Style(Color.Green)));

      if (action == "Done")
      {
        break;
      }

      switch (action)
      {
        case "Add command":
          {
            var commandName = AnsiConsole.Prompt(
                new TextPrompt<string>("  Command name:")
                    .Validate(c => !string.IsNullOrWhiteSpace(c)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Command name cannot be empty")));

            var isDenied = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("  Action:")
                    .AddChoices("Allow", "Deny")) == "Deny";

            entries.Add(new JeaProfileEntry(commandName, isDenied));
            var marker = isDenied ? "[red]Deny[/]" : "[green]Allow[/]";
            AnsiConsole.MarkupLine($"  [green]v[/] {marker} [bold]{Markup.Escape(commandName)}[/]");
            break;
          }
        case "Add module":
          {
            var moduleName = AnsiConsole.Prompt(
                new TextPrompt<string>("  Module name:")
                    .Validate(m => !string.IsNullOrWhiteSpace(m)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Module name cannot be empty")));

            modules.Add(moduleName);
            AnsiConsole.MarkupLine($"  [green]v[/] Module [bold]{Markup.Escape(moduleName)}[/] added.");
            break;
          }
      }
    }

    var profile = new JeaProfile(name, languageMode, modules, entries);
    await _store.SaveAsync(profile, ct);

    var filePath = GetProfileFilePath(name);
    AnsiConsole.MarkupLine($"[green]v[/] Profile [bold]{Markup.Escape(name)}[/] created.");
    AnsiConsole.MarkupLine($"[dim]File: {Markup.Escape(filePath)}[/]");
  }

  // ──────────────────────────────────────────────
  //  EDIT
  // ──────────────────────────────────────────────

  private async Task HandleEditAsync(string[] tokens, CancellationToken ct)
  {
    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : await PromptProfileSelectionAsync(ct);

    if (name is null)
    {
      return;
    }

    var profile = await _store.LoadAsync(name, ct);
    if (profile is null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Profile [bold]{Markup.Escape(name)}[/] not found.");
      return;
    }

    var entries = new List<JeaProfileEntry>(profile.Entries);
    var modules = new List<string>(profile.Modules);
    var languageMode = profile.LanguageMode;

    while (true)
    {
      var choice = AnsiConsole.Prompt(
          new SelectionPrompt<string>()
              .Title($"Edit [bold]{Markup.Escape(name)}[/]:")
              .AddChoices(
                  "Change language mode",
                  "Add command",
                  "Remove command",
                  "Toggle command deny",
                  "Add module",
                  "Remove module",
                  "Done")
              .HighlightStyle(new Style(Color.Green)));

      if (choice == "Done")
      {
        break;
      }

      switch (choice)
      {
        case "Change language mode":
          {
            languageMode = AnsiConsole.Prompt(
                new SelectionPrompt<PSLanguageModeName>()
                    .Title("  Language mode:")
                    .AddChoices(
                        PSLanguageModeName.FullLanguage,
                        PSLanguageModeName.ConstrainedLanguage,
                        PSLanguageModeName.RestrictedLanguage,
                        PSLanguageModeName.NoLanguage));
            AnsiConsole.MarkupLine($"  [green]v[/] Language mode set to [bold]{languageMode}[/].");
            break;
          }
        case "Add command":
          {
            var commandName = AnsiConsole.Prompt(
                new TextPrompt<string>("  Command name:")
                    .Validate(c => !string.IsNullOrWhiteSpace(c)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Command name cannot be empty")));

            var isDenied = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("  Action:")
                    .AddChoices("Allow", "Deny")) == "Deny";

            entries.Add(new JeaProfileEntry(commandName, isDenied));
            var marker = isDenied ? "[red]Deny[/]" : "[green]Allow[/]";
            AnsiConsole.MarkupLine($"  [green]v[/] {marker} [bold]{Markup.Escape(commandName)}[/]");
            break;
          }
        case "Remove command":
          {
            if (entries.Count == 0)
            {
              AnsiConsole.MarkupLine("  [yellow]No commands to remove.[/]");
              break;
            }

            var commandToRemove = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("  Select command to remove:")
                    .AddChoices(entries.Select(e => e.CommandName)));

            entries.RemoveAll(e => e.CommandName == commandToRemove);
            AnsiConsole.MarkupLine($"  [green]v[/] Removed [bold]{Markup.Escape(commandToRemove)}[/].");
            break;
          }
        case "Toggle command deny":
          {
            if (entries.Count == 0)
            {
              AnsiConsole.MarkupLine("  [yellow]No commands to toggle.[/]");
              break;
            }

            var descriptions = entries.Select(e =>
            {
              var status = e.IsDenied ? "[red]Deny[/]" : "[green]Allow[/]";
              return $"{Markup.Escape(e.CommandName)}  {status}";
            }).ToList();

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("  Select command to toggle:")
                    .AddChoices(descriptions));

            var selectedCommand = selected.Split("  ", StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            var index = entries.FindIndex(e => Markup.Escape(e.CommandName) == selectedCommand);
            if (index >= 0)
            {
              var entry = entries[index];
              entries[index] = entry with { IsDenied = !entry.IsDenied };
              var newStatus = entries[index].IsDenied ? "[red]Deny[/]" : "[green]Allow[/]";
              AnsiConsole.MarkupLine($"  [green]v[/] [bold]{Markup.Escape(entry.CommandName)}[/] set to {newStatus}.");
            }

            break;
          }
        case "Add module":
          {
            var moduleName = AnsiConsole.Prompt(
                new TextPrompt<string>("  Module name:")
                    .Validate(m => !string.IsNullOrWhiteSpace(m)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Module name cannot be empty")));

            modules.Add(moduleName);
            AnsiConsole.MarkupLine($"  [green]v[/] Module [bold]{Markup.Escape(moduleName)}[/] added.");
            break;
          }
        case "Remove module":
          {
            if (modules.Count == 0)
            {
              AnsiConsole.MarkupLine("  [yellow]No modules to remove.[/]");
              break;
            }

            var moduleToRemove = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("  Select module to remove:")
                    .AddChoices(modules));

            modules.Remove(moduleToRemove);
            AnsiConsole.MarkupLine($"  [green]v[/] Removed module [bold]{Markup.Escape(moduleToRemove)}[/].");
            break;
          }
      }
    }

    var updatedProfile = new JeaProfile(name, languageMode, modules, entries);
    await _store.SaveAsync(updatedProfile, ct);

    var filePath = GetProfileFilePath(name);
    AnsiConsole.MarkupLine($"[green]v[/] Profile [bold]{Markup.Escape(name)}[/] saved.");
    AnsiConsole.MarkupLine($"[dim]File: {Markup.Escape(filePath)}[/]");
  }

  // ──────────────────────────────────────────────
  //  DELETE
  // ──────────────────────────────────────────────

  private async Task HandleDeleteAsync(string[] tokens, CancellationToken ct)
  {
    var names = await _store.ListNamesAsync(ct);
    var deletable = names
        .Where(n => !n.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase))
        .ToList();

    string name;
    if (tokens.Length > 2)
    {
      name = string.Join(' ', tokens.Skip(2));
    }
    else
    {
      if (deletable.Count == 0)
      {
        AnsiConsole.MarkupLine("No profiles available to delete.");
        return;
      }

      name = AnsiConsole.Prompt(
          new SelectionPrompt<string>()
              .Title("Select profile to delete:")
              .AddChoices(deletable));
    }

    if (name.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase))
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Cannot delete the global profile [bold]{BuiltInJeaProfile.GlobalName}[/].");
      return;
    }

    var profile = await _store.LoadAsync(name, ct);
    if (profile is null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Profile [bold]{Markup.Escape(name)}[/] not found.");
      return;
    }

    if (!AnsiConsole.Confirm($"Delete profile [bold]{Markup.Escape(name)}[/]?", defaultValue: false))
    {
      AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
      return;
    }

    await _store.DeleteAsync(name, ct);
    AnsiConsole.MarkupLine($"[green]v[/] Profile [bold]{Markup.Escape(name)}[/] deleted.");
  }

  // ──────────────────────────────────────────────
  //  EFFECTIVE
  // ──────────────────────────────────────────────

  private async Task HandleEffectiveAsync(CancellationToken ct)
  {
    await EnsureGlobalProfileAsync(ct);

    var projectName = _activeProject.Name;
    List<string> profileNames;
    var sourceProfiles = new List<string> { BuiltInJeaProfile.GlobalName };

    if (projectName is null || projectName.Equals(Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase))
    {
      profileNames = [];
    }
    else
    {
      var project = await _projectRepository.LoadAsync(projectName, ct);
      var projectProfiles = project?.Execution?.JeaProfiles ?? [];
      profileNames = [.. projectProfiles];
      sourceProfiles.AddRange(projectProfiles);
    }

    var effective = await _composer.ComposeAsync(profileNames, ct);

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"  [bold]Language mode:[/]  {effective.LanguageMode}");
    AnsiConsole.MarkupLine($"  [bold]Commands:[/]       {effective.AllowedCommands.Count}");

    if (effective.AllowedCommands.Count > 0)
    {
      AnsiConsole.WriteLine();
      AnsiConsole.Write(new Rule("[bold]Allowed commands[/]").LeftJustified().RuleStyle("dim"));
      foreach (var command in effective.AllowedCommands)
      {
        AnsiConsole.MarkupLine($"    [green]v[/] {Markup.Escape(command)}");
      }
    }

    if (effective.Modules.Count > 0)
    {
      AnsiConsole.WriteLine();
      AnsiConsole.Write(new Rule("[bold]Modules[/]").LeftJustified().RuleStyle("dim"));
      foreach (var module in effective.Modules)
      {
        AnsiConsole.MarkupLine($"    {Markup.Escape(module)}");
      }
    }

    AnsiConsole.WriteLine();
    var sources = string.Join(", ", sourceProfiles.Select(Markup.Escape));
    AnsiConsole.MarkupLine($"  [dim]Source profiles: {sources}[/]");
    AnsiConsole.WriteLine();
  }

  // ──────────────────────────────────────────────
  //  ASSIGN
  // ──────────────────────────────────────────────

  private async Task HandleAssignAsync(string[] tokens, CancellationToken ct)
  {
    var projectName = _activeProject.Name;
    if (projectName is null || projectName.Equals(Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase))
    {
      AnsiConsole.MarkupLine("[red]Error:[/] No project selected. Use [bold]/project create[/] or [bold]--project[/] to select a project first.");
      return;
    }

    string name;
    if (tokens.Length > 2)
    {
      name = string.Join(' ', tokens.Skip(2));
    }
    else
    {
      var allNames = await _store.ListNamesAsync(ct);
      var assignable = allNames
          .Where(n => !n.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase))
          .ToList();

      if (assignable.Count == 0)
      {
        AnsiConsole.MarkupLine("No profiles available to assign.");
        AnsiConsole.MarkupLine("[dim]Create one with[/] /jea create <name>");
        return;
      }

      name = AnsiConsole.Prompt(
          new SelectionPrompt<string>()
              .Title("Select profile to assign:")
              .AddChoices(assignable));
    }

    var profile = await _store.LoadAsync(name, ct);
    if (profile is null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Profile [bold]{Markup.Escape(name)}[/] not found.");
      return;
    }

    var project = await _projectRepository.LoadAsync(projectName, ct);
    if (project is null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Project [bold]{Markup.Escape(projectName)}[/] not found.");
      return;
    }

    project.Execution ??= new ExecutionConfig();

    if (project.Execution.JeaProfiles.Contains(name, StringComparer.OrdinalIgnoreCase))
    {
      AnsiConsole.MarkupLine($"Profile [bold]{Markup.Escape(name)}[/] is already assigned to project [bold]{Markup.Escape(projectName)}[/].");
      return;
    }

    project.Execution.JeaProfiles.Add(name);
    await _projectRepository.SaveAsync(project, ct);
    AnsiConsole.MarkupLine($"[green]v[/] Profile [bold]{Markup.Escape(name)}[/] assigned to project [bold]{Markup.Escape(projectName)}[/].");
  }

  // ──────────────────────────────────────────────
  //  UNASSIGN
  // ──────────────────────────────────────────────

  private async Task HandleUnassignAsync(string[] tokens, CancellationToken ct)
  {
    var projectName = _activeProject.Name;
    if (projectName is null || projectName.Equals(Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase))
    {
      AnsiConsole.MarkupLine("[red]Error:[/] No project selected. Use [bold]/project create[/] or [bold]--project[/] to select a project first.");
      return;
    }

    var project = await _projectRepository.LoadAsync(projectName, ct);
    if (project is null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Project [bold]{Markup.Escape(projectName)}[/] not found.");
      return;
    }

    var assigned = project.Execution?.JeaProfiles ?? [];
    if (assigned.Count == 0)
    {
      AnsiConsole.MarkupLine($"No JEA profiles assigned to project [bold]{Markup.Escape(projectName)}[/].");
      return;
    }

    string name;
    if (tokens.Length > 2)
    {
      name = string.Join(' ', tokens.Skip(2));
    }
    else
    {
      name = AnsiConsole.Prompt(
          new SelectionPrompt<string>()
              .Title("Select profile to unassign:")
              .AddChoices(assigned));
    }

    if (!assigned.Remove(name))
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Profile [bold]{Markup.Escape(name)}[/] is not assigned to project [bold]{Markup.Escape(projectName)}[/].");
      return;
    }

    await _projectRepository.SaveAsync(project, ct);
    AnsiConsole.MarkupLine($"[green]v[/] Profile [bold]{Markup.Escape(name)}[/] unassigned from project [bold]{Markup.Escape(projectName)}[/].");
  }

  // ──────────────────────────────────────────────
  //  HELPERS
  // ──────────────────────────────────────────────

  private async Task<string?> PromptProfileSelectionAsync(CancellationToken ct)
  {
    var names = await _store.ListNamesAsync(ct);
    if (names.Count == 0)
    {
      AnsiConsole.MarkupLine("No JEA profiles found.");
      AnsiConsole.MarkupLine("[dim]Create one with[/] /jea create <name>");
      return null;
    }

    return AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Select profile:")
            .AddChoices(names));
  }

  private static bool ValidateProfileName(string name)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      AnsiConsole.MarkupLine("[red]Error:[/] Profile name cannot be empty.");
      return false;
    }

    if (name.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(BuiltInJeaProfile.Name, StringComparison.OrdinalIgnoreCase))
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] [bold]{Markup.Escape(name)}[/] is a reserved profile name.");
      return false;
    }

    if (!ProfileNameRegex().IsMatch(name))
    {
      AnsiConsole.MarkupLine("[red]Error:[/] Profile name must contain only letters, numbers, hyphens, and underscores.");
      return false;
    }

    return true;
  }

  private static Markup BuildProfileDetail(JeaProfile profile, string filePath)
  {
    var lines = new List<string>
        {
            $"[bold]Language mode:[/]  {profile.LanguageMode}",
        };

    if (profile.AllowedCommands.Count > 0)
    {
      lines.Add("");
      lines.Add("[bold]Allowed commands:[/]");
      foreach (var command in profile.AllowedCommands)
      {
        lines.Add($"  [green]v[/] {Markup.Escape(command)}");
      }
    }

    if (profile.DeniedCommands.Count > 0)
    {
      lines.Add("");
      lines.Add("[bold]Denied commands:[/]");
      foreach (var command in profile.DeniedCommands)
      {
        lines.Add($"  [red]x[/] {Markup.Escape(command)}");
      }
    }

    if (profile.Modules.Count > 0)
    {
      lines.Add("");
      lines.Add("[bold]Modules:[/]");
      foreach (var module in profile.Modules)
      {
        lines.Add($"  {Markup.Escape(module)}");
      }
    }

    lines.Add("");
    lines.Add($"[dim]File: {Markup.Escape(filePath)}[/]");

    return new Markup(string.Join("\n", lines));
  }

  private static string GetProfileFilePath(string name)
  {
    return Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".boydcode",
        "jea",
        $"{name}.profile");
  }

  [GeneratedRegex(@"^[a-zA-Z0-9_-]+$")]
  private static partial Regex ProfileNameRegex();
}
