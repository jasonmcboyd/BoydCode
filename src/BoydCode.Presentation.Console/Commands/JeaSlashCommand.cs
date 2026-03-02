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
  private static readonly string[] AddToProfileChoices = ["Add command", "Add module", "Done"];
  private static readonly string[] AllowDenyChoices = ["Allow", "Deny"];
  private static readonly string[] EditProfileChoices =
  [
      "Change language mode",
      "Add command",
      "Remove command",
      "Toggle command deny",
      "Add module",
      "Remove module",
      "Done",
  ];

  private readonly IJeaProfileStore _store;
  private readonly JeaProfileComposer _composer;
  private readonly ActiveProject _activeProject;
  private readonly IProjectRepository _projectRepository;
  private readonly IUserInterface _ui;

  public JeaSlashCommand(
      IJeaProfileStore store,
      JeaProfileComposer composer,
      ActiveProject activeProject,
      IProjectRepository projectRepository,
      IUserInterface ui)
  {
    _store = store;
    _composer = composer;
    _activeProject = activeProject;
    _projectRepository = projectRepository;
    _ui = ui;
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
        SpectreHelpers.Usage("/jea list|show|create|edit|delete|effective|assign|unassign");
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
      SpectreHelpers.OutputMarkup("No JEA profiles found.");
      SpectreHelpers.Dim("Create one with /jea create <name>");
      return;
    }

    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup($"{"Name",-22}{"Language Mode",-22}{"Commands",8}  {"Modules",7}");
    SpectreHelpers.OutputMarkup(new string('\u2500', 65));

    foreach (var name in names)
    {
      var profile = await _store.LoadAsync(name, ct);
      if (profile is null)
      {
        continue;
      }

      var isGlobal = name.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase);
      var displayName = isGlobal ? $"{name} (global)" : name;

      SpectreHelpers.OutputMarkup(
        $"{Markup.Escape(displayName),-22}" +
        $"{profile.LanguageMode,-22}" +
        $"{profile.Entries.Count.ToString(CultureInfo.InvariantCulture),8}  " +
        $"{profile.Modules.Count.ToString(CultureInfo.InvariantCulture),7}");
    }

    SpectreHelpers.OutputLine();
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
      SpectreHelpers.Error($"Profile '{name}' not found.");
      return;
    }

    var filePath = GetProfileFilePath(name);
    var content = BuildProfileDetailText(profile, filePath);

    _ui.ShowModal(profile.Name, content);
  }

  // ──────────────────────────────────────────────
  //  CREATE
  // ──────────────────────────────────────────────

  private async Task HandleCreateAsync(string[] tokens, CancellationToken ct)
  {
    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : SpectreHelpers.PromptNonEmpty("Profile [green]name[/]:");

    if (!ValidateProfileName(name))
    {
      return;
    }

    var existing = await _store.LoadAsync(name, ct);
    if (existing is not null)
    {
      SpectreHelpers.Error($"Profile '{name}' already exists.");
      return;
    }

    var languageMode = SpectreHelpers.Select(
        "Language mode:",
        new[]
        {
            PSLanguageModeName.FullLanguage,
            PSLanguageModeName.ConstrainedLanguage,
            PSLanguageModeName.RestrictedLanguage,
            PSLanguageModeName.NoLanguage,
        });

    var entries = new List<JeaProfileEntry>();
    var modules = new List<string>();

    while (true)
    {
      var action = SpectreHelpers.Select("Add to profile:", AddToProfileChoices);

      if (action == "Done")
      {
        break;
      }

      switch (action)
      {
        case "Add command":
          {
            var commandName = SpectreHelpers.PromptNonEmpty("  Command name:");

            var isDenied = SpectreHelpers.Select("  Action:", AllowDenyChoices) == "Deny";

            entries.Add(new JeaProfileEntry(commandName, isDenied));
            var marker = isDenied ? "[red]Deny[/]" : "[green]Allow[/]";
            SpectreHelpers.OutputMarkup($"  [green]\u2713[/] {marker} [bold]{Markup.Escape(commandName)}[/]");
            break;
          }
        case "Add module":
          {
            var moduleName = SpectreHelpers.PromptNonEmpty("  Module name:");

            modules.Add(moduleName);
            SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Module [bold]{Markup.Escape(moduleName)}[/] added.");
            break;
          }
      }
    }

    var profile = new JeaProfile(name, languageMode, modules, entries);
    await _store.SaveAsync(profile, ct);

    var filePath = GetProfileFilePath(name);
    SpectreHelpers.Success($"Profile '{name}' created.");
    SpectreHelpers.Dim($"File: {filePath}");
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
      SpectreHelpers.Error($"Profile '{name}' not found.");
      return;
    }

    var entries = new List<JeaProfileEntry>(profile.Entries);
    var modules = new List<string>(profile.Modules);
    var languageMode = profile.LanguageMode;

    var lastIndex = 0;
    while (true)
    {
      var choice = SpectreHelpers.Select(
          $"Edit [bold]{Markup.Escape(name)}[/]:",
          EditProfileChoices,
          lastIndex);

      if (choice == "Done")
      {
        break;
      }

      lastIndex = Array.IndexOf(EditProfileChoices, choice);

      switch (choice)
      {
        case "Change language mode":
          {
            languageMode = SpectreHelpers.Select(
                "  Language mode:",
                new[]
                {
                    PSLanguageModeName.FullLanguage,
                    PSLanguageModeName.ConstrainedLanguage,
                    PSLanguageModeName.RestrictedLanguage,
                    PSLanguageModeName.NoLanguage,
                });
            SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Language mode set to [bold]{languageMode}[/].");
            break;
          }
        case "Add command":
          {
            var commandName = SpectreHelpers.PromptNonEmpty("  Command name:");

            var isDenied = SpectreHelpers.Select("  Action:", AllowDenyChoices) == "Deny";

            entries.Add(new JeaProfileEntry(commandName, isDenied));
            var marker = isDenied ? "[red]Deny[/]" : "[green]Allow[/]";
            SpectreHelpers.OutputMarkup($"  [green]\u2713[/] {marker} [bold]{Markup.Escape(commandName)}[/]");
            break;
          }
        case "Remove command":
          {
            if (entries.Count == 0)
            {
              SpectreHelpers.OutputMarkup("  [yellow]No commands to remove.[/]");
              break;
            }

            var commandToRemove = SpectreHelpers.Select("  Select command to remove:", entries.Select(e => e.CommandName));

            entries.RemoveAll(e => e.CommandName == commandToRemove);
            SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Removed [bold]{Markup.Escape(commandToRemove)}[/].");
            break;
          }
        case "Toggle command deny":
          {
            if (entries.Count == 0)
            {
              SpectreHelpers.OutputMarkup("  [yellow]No commands to toggle.[/]");
              break;
            }

            var descriptions = entries.Select(e =>
            {
              var status = e.IsDenied ? "[red]Deny[/]" : "[green]Allow[/]";
              return $"{Markup.Escape(e.CommandName)}  {status}";
            }).ToList();

            var selected = SpectreHelpers.Select("  Select command to toggle:", descriptions);

            var selectedCommand = selected.Split("  ", StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            var index = entries.FindIndex(e => Markup.Escape(e.CommandName) == selectedCommand);
            if (index >= 0)
            {
              var entry = entries[index];
              entries[index] = entry with { IsDenied = !entry.IsDenied };
              var newStatus = entries[index].IsDenied ? "[red]Deny[/]" : "[green]Allow[/]";
              SpectreHelpers.OutputMarkup($"  [green]\u2713[/] [bold]{Markup.Escape(entry.CommandName)}[/] set to {newStatus}.");
            }

            break;
          }
        case "Add module":
          {
            var moduleName = SpectreHelpers.PromptNonEmpty("  Module name:");

            modules.Add(moduleName);
            SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Module [bold]{Markup.Escape(moduleName)}[/] added.");
            break;
          }
        case "Remove module":
          {
            if (modules.Count == 0)
            {
              SpectreHelpers.OutputMarkup("  [yellow]No modules to remove.[/]");
              break;
            }

            var moduleToRemove = SpectreHelpers.Select("  Select module to remove:", modules);

            modules.Remove(moduleToRemove);
            SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Removed module [bold]{Markup.Escape(moduleToRemove)}[/].");
            break;
          }
      }
    }

    var updatedProfile = new JeaProfile(name, languageMode, modules, entries);
    await _store.SaveAsync(updatedProfile, ct);

    var filePath = GetProfileFilePath(name);
    SpectreHelpers.Success($"Profile '{name}' saved.");
    SpectreHelpers.Dim($"File: {filePath}");
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
        SpectreHelpers.OutputMarkup("No profiles available to delete.");
        return;
      }

      name = SpectreHelpers.Select("Select profile to delete:", deletable);
    }

    if (name.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase))
    {
      SpectreHelpers.Error($"Cannot delete the global profile '{BuiltInJeaProfile.GlobalName}'.");
      return;
    }

    var profile = await _store.LoadAsync(name, ct);
    if (profile is null)
    {
      SpectreHelpers.Error($"Profile '{name}' not found.");
      return;
    }

    if (!SpectreHelpers.Confirm($"Delete profile [bold]{Markup.Escape(name)}[/]?", defaultValue: false))
    {
      SpectreHelpers.Cancelled();
      return;
    }

    await _store.DeleteAsync(name, ct);
    SpectreHelpers.Success($"Profile '{name}' deleted.");
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

    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup($"  [bold]Language mode:[/]  {effective.LanguageMode}");
    SpectreHelpers.OutputMarkup($"  [bold]Commands:[/]       {effective.AllowedCommands.Count}");

    if (effective.AllowedCommands.Count > 0)
    {
      SpectreHelpers.Section("Allowed commands");
      foreach (var command in effective.AllowedCommands)
      {
        SpectreHelpers.OutputMarkup($"    [green]\u2713[/] {Markup.Escape(command)}");
      }
    }

    if (effective.Modules.Count > 0)
    {
      SpectreHelpers.Section("Modules");
      foreach (var module in effective.Modules)
      {
        SpectreHelpers.OutputMarkup($"    {Markup.Escape(module)}");
      }
    }

    SpectreHelpers.OutputLine();
    var sources = string.Join(", ", sourceProfiles.Select(Markup.Escape));
    SpectreHelpers.OutputMarkup($"  [dim]Source profiles: {sources}[/]");
    SpectreHelpers.OutputLine();
  }

  // ──────────────────────────────────────────────
  //  ASSIGN
  // ──────────────────────────────────────────────

  private async Task HandleAssignAsync(string[] tokens, CancellationToken ct)
  {
    var projectName = _activeProject.Name;
    if (projectName is null || projectName.Equals(Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase))
    {
      SpectreHelpers.Error("No project selected. Use /project create or --project to select a project first.");
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
        SpectreHelpers.OutputMarkup("No profiles available to assign.");
        SpectreHelpers.Dim("Create one with /jea create <name>");
        return;
      }

      name = SpectreHelpers.Select("Select profile to assign:", assignable);
    }

    var profile = await _store.LoadAsync(name, ct);
    if (profile is null)
    {
      SpectreHelpers.Error($"Profile '{name}' not found.");
      return;
    }

    var project = await _projectRepository.LoadAsync(projectName, ct);
    if (project is null)
    {
      SpectreHelpers.Error($"Project '{projectName}' not found.");
      return;
    }

    project.Execution ??= new ExecutionConfig();

    if (project.Execution.JeaProfiles.Contains(name, StringComparer.OrdinalIgnoreCase))
    {
      SpectreHelpers.OutputMarkup($"Profile [bold]{Markup.Escape(name)}[/] is already assigned to project [bold]{Markup.Escape(projectName)}[/].");
      return;
    }

    project.Execution.JeaProfiles.Add(name);
    await _projectRepository.SaveAsync(project, ct);
    SpectreHelpers.Success($"Profile '{name}' assigned to project '{projectName}'.");
  }

  // ──────────────────────────────────────────────
  //  UNASSIGN
  // ──────────────────────────────────────────────

  private async Task HandleUnassignAsync(string[] tokens, CancellationToken ct)
  {
    var projectName = _activeProject.Name;
    if (projectName is null || projectName.Equals(Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase))
    {
      SpectreHelpers.Error("No project selected. Use /project create or --project to select a project first.");
      return;
    }

    var project = await _projectRepository.LoadAsync(projectName, ct);
    if (project is null)
    {
      SpectreHelpers.Error($"Project '{projectName}' not found.");
      return;
    }

    var assigned = project.Execution?.JeaProfiles ?? [];
    if (assigned.Count == 0)
    {
      SpectreHelpers.OutputMarkup($"No JEA profiles assigned to project [bold]{Markup.Escape(projectName)}[/].");
      return;
    }

    string name;
    if (tokens.Length > 2)
    {
      name = string.Join(' ', tokens.Skip(2));
    }
    else
    {
      name = SpectreHelpers.Select("Select profile to unassign:", assigned);
    }

    if (!assigned.Remove(name))
    {
      SpectreHelpers.Error($"Profile '{name}' is not assigned to project '{projectName}'.");
      return;
    }

    await _projectRepository.SaveAsync(project, ct);
    SpectreHelpers.Success($"Profile '{name}' unassigned from project '{projectName}'.");
  }

  // ──────────────────────────────────────────────
  //  HELPERS
  // ──────────────────────────────────────────────

  private async Task<string?> PromptProfileSelectionAsync(CancellationToken ct)
  {
    var names = await _store.ListNamesAsync(ct);
    if (names.Count == 0)
    {
      SpectreHelpers.OutputMarkup("No JEA profiles found.");
      SpectreHelpers.Dim("Create one with /jea create <name>");
      return null;
    }

    return SpectreHelpers.Select("Select profile:", names);
  }

  private static bool ValidateProfileName(string name)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      SpectreHelpers.Error("Profile name cannot be empty.");
      return false;
    }

    if (name.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(BuiltInJeaProfile.Name, StringComparison.OrdinalIgnoreCase))
    {
      SpectreHelpers.Error($"'{name}' is a reserved profile name.");
      return false;
    }

    if (!ProfileNameRegex().IsMatch(name))
    {
      SpectreHelpers.Error("Profile name must contain only letters, numbers, hyphens, and underscores.");
      return false;
    }

    return true;
  }

  private static string BuildProfileDetailText(JeaProfile profile, string filePath)
  {
    var lines = new List<string>
        {
            $"Language mode:  {profile.LanguageMode}",
        };

    if (profile.AllowedCommands.Count > 0)
    {
      lines.Add("");
      lines.Add("Allowed commands:");
      foreach (var command in profile.AllowedCommands)
      {
        lines.Add($"  \u2713 {command}");
      }
    }

    if (profile.DeniedCommands.Count > 0)
    {
      lines.Add("");
      lines.Add("Denied commands:");
      foreach (var command in profile.DeniedCommands)
      {
        lines.Add($"  x {command}");
      }
    }

    if (profile.Modules.Count > 0)
    {
      lines.Add("");
      lines.Add("Modules:");
      foreach (var module in profile.Modules)
      {
        lines.Add($"  {module}");
      }
    }

    lines.Add("");
    lines.Add($"File: {filePath}");

    return string.Join("\n", lines);
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
