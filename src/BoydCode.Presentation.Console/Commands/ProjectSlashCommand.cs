using System.Globalization;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.SlashCommands;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed class ProjectSlashCommand : ISlashCommand
{
  private static readonly string[] ConfigureSections = ["Directories", "System prompt", "Container settings"];

  private readonly IProjectRepository _projectRepository;
  private readonly DirectoryResolver _directoryResolver;
  private readonly DirectoryGuard _directoryGuard;
  private readonly ActiveProject _activeProject;
  private readonly ActiveSession _activeSession;
  private readonly ActiveExecutionEngine _activeEngine;
  private readonly IUserInterface _ui;

  public ProjectSlashCommand(
      IProjectRepository projectRepository,
      DirectoryResolver directoryResolver,
      DirectoryGuard directoryGuard,
      ActiveProject activeProject,
      ActiveSession activeSession,
      ActiveExecutionEngine activeEngine,
      IUserInterface ui)
  {
    _projectRepository = projectRepository;
    _directoryResolver = directoryResolver;
    _directoryGuard = directoryGuard;
    _activeProject = activeProject;
    _activeSession = activeSession;
    _activeEngine = activeEngine;
    _ui = ui;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/project",
      "Manage projects",
      [
          new("create [name]", "Create a new project"),
            new("list", "List all projects"),
            new("show [name]", "Show project details"),
            new("edit [name]", "Edit project settings"),
            new("delete [name]", "Delete a project"),
      ]);

  public async Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (tokens.Length == 0 || !tokens[0].Equals("/project", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    var subcommand = tokens.Length > 1 ? tokens[1].ToLowerInvariant() : string.Empty;

    switch (subcommand)
    {
      case "create":
        await HandleCreateAsync(tokens, ct);
        break;
      case "list":
        await HandleListAsync(ct);
        break;
      case "show":
        await HandleShowAsync(tokens, ct);
        break;
      case "edit":
        await HandleEditAsync(tokens, ct);
        break;
      case "delete":
        await HandleDeleteAsync(tokens, ct);
        break;
      default:
        SpectreHelpers.Usage("/project create|list|show|edit|delete");
        break;
    }

    return true;
  }

  // ──────────────────────────────────────────────
  //  CREATE
  // ──────────────────────────────────────────────

  private async Task HandleCreateAsync(string[] tokens, CancellationToken ct)
  {
    if (tokens.Length <= 2 && !_ui.IsInteractive)
    {
      SpectreHelpers.Usage("/project create <name>");
      return;
    }

    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : SpectreHelpers.PromptNonEmpty("Project [green]name[/]:");

    var existing = await _projectRepository.LoadAsync(name, ct);
    if (existing is not null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Project [bold]{Markup.Escape(name)}[/] already exists.");
      return;
    }

    var project = new Project(name);
    await _projectRepository.SaveAsync(project, ct);
    AnsiConsole.MarkupLine($"[green]v[/] Project [bold]{Markup.Escape(name)}[/] created.");
    AnsiConsole.WriteLine();

    if (!_ui.IsInteractive)
    {
      return;
    }

    var wantConfigure = SpectreHelpers.Confirm("Configure project settings now?", defaultValue: false);
    if (!wantConfigure)
    {
      AnsiConsole.MarkupLine($"[dim]Tip: Use /project edit {Markup.Escape(name)} to configure later.[/]");
      return;
    }

    AnsiConsole.WriteLine();

    var sections = SpectreHelpers.MultiSelect("Which settings would you like to configure?", ConfigureSections);

    if (sections.Contains("Directories"))
    {
      SpectreHelpers.Section("Directories");
      AddDirectoriesLoop(project);
    }

    if (sections.Contains("System prompt"))
    {
      SpectreHelpers.Section("System prompt");
      PromptSystemPrompt(project);
    }

    if (sections.Contains("Container settings"))
    {
      SpectreHelpers.Section("Container settings");
      ConfigureContainer(project);
    }

    await _projectRepository.SaveAsync(project, ct);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[green]v[/] Project [bold]{Markup.Escape(name)}[/] saved.");
  }

  // ──────────────────────────────────────────────
  //  LIST
  // ──────────────────────────────────────────────

  private async Task HandleListAsync(CancellationToken ct)
  {
    var names = await _projectRepository.ListNamesAsync(ct);

    if (names.Count == 0)
    {
      AnsiConsole.MarkupLine("No projects found.");
      SpectreHelpers.Dim("Create one with /project create <name>");
      return;
    }

    var table = SpectreHelpers.SimpleTable("Name", "Dirs", "Docker", "Last used");
    table.Columns[1].RightAligned();

    foreach (var name in names)
    {
      var project = await _projectRepository.LoadAsync(name, ct);
      if (project is null)
      {
        continue;
      }

      var resolvedDirs = _directoryResolver.Resolve(project.Directories);
      var gitCount = resolvedDirs.Count(d => d.IsGitRepository);
      var dirCount = gitCount > 0
          ? $"{project.Directories.Count.ToString(CultureInfo.InvariantCulture)} ({gitCount.ToString(CultureInfo.InvariantCulture)} git)"
          : project.Directories.Count.ToString(CultureInfo.InvariantCulture);

      var execLabel = project.DockerImage is not null
          ? project.RequireContainer
              ? $"{Markup.Escape(project.DockerImage)} [dim](required)[/]"
              : Markup.Escape(project.DockerImage)
          : "[dim]--[/]";

      var lastUsed = project.LastAccessedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

      var isAmbient = name.Equals(Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase);
      var displayName = isAmbient
          ? $"{Markup.Escape(name)} [dim](ambient)[/]"
          : Markup.Escape(name);

      table.AddRow(
          displayName,
          dirCount,
          execLabel,
          lastUsed);
    }

    AnsiConsole.Write(table);
  }

  // ──────────────────────────────────────────────
  //  SHOW
  // ──────────────────────────────────────────────

  private async Task HandleShowAsync(string[] tokens, CancellationToken ct)
  {
    if (tokens.Length <= 2 && _activeProject.Name is null && !_ui.IsInteractive)
    {
      SpectreHelpers.Usage("/project show <name>");
      return;
    }

    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : _activeProject.Name ?? SpectreHelpers.Ask<string>("Project name:");

    var project = await _projectRepository.LoadAsync(name, ct);
    if (project is null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Project [bold]{Markup.Escape(name)}[/] not found.");
      return;
    }

    var isMinimal = project.Directories.Count == 0
        && project.SystemPrompt is null
        && project.DockerImage is null
        && !project.RequireContainer
        && project.Execution is null;

    AnsiConsole.WriteLine();

    // Grid section
    var grid = SpectreHelpers.InfoGrid();

    var isAmbient = name.Equals(Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase);
    var displayName = isAmbient ? $"{project.Name} (ambient)" : project.Name;
    SpectreHelpers.AddInfoRow(grid, "Project", displayName);

    SpectreHelpers.AddInfoRow(grid,
        "Created", project.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        "Last used", project.LastAccessedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

    if (project.Execution is not null)
    {
      SpectreHelpers.AddInfoRow(grid, "Engine", project.Execution.Mode.ToString());
    }

    if (project.DockerImage is not null)
    {
      SpectreHelpers.AddInfoRow(grid, "Docker", project.DockerImage);
    }

    if (project.DockerImage is not null || project.RequireContainer)
    {
      var containerMarkup = project.RequireContainer
          ? new Markup("[green]Required[/]")
          : new Markup("[yellow]Optional[/]");
      grid.AddRow(
          new Markup("[dim]Container[/]"),
          containerMarkup,
          new Markup(""),
          new Markup(""));
    }

    AnsiConsole.Write(grid);

    // Directories
    if (project.Directories.Count > 0)
    {
      AnsiConsole.WriteLine();
      AnsiConsole.MarkupLine("  [dim]Directories[/]");

      var resolvedDirs = _directoryResolver.Resolve(project.Directories);

      var dirTable = new Table()
          .Border(TableBorder.None)
          .HideHeaders()
          .AddColumn(new TableColumn("Path"))
          .AddColumn(new TableColumn("Access").RightAligned())
          .AddColumn(new TableColumn("Git"));

      foreach (var dir in resolvedDirs)
      {
        var accessStyle = dir.AccessLevel == DirectoryAccessLevel.ReadOnly
            ? "[yellow]ReadOnly[/]"
            : "[green]ReadWrite[/]";

        var gitInfo = dir switch
        {
          { Exists: false } => "[red]missing[/]",
          { IsGitRepository: true, GitBranch: not null } => $"[cyan]{Markup.Escape(dir.GitBranch)}[/]",
          { IsGitRepository: true } => "[dim]git[/]",
          _ => "[dim]--[/]",
        };

        dirTable.AddRow($"- {Markup.Escape(dir.Path)}", accessStyle, gitInfo);
      }

      AnsiConsole.Write(new Padder(dirTable).PadLeft(4));
    }

    // Meta prompt
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("  [dim]Meta prompt[/]");
    AnsiConsole.WriteLine();
    var fullMetaPrompt = MetaPrompt.Build(_activeEngine.Mode, _activeEngine.Engine?.GetAvailableCommands() ?? []);
    foreach (var line in fullMetaPrompt.Trim().Split('\n'))
    {
      var trimmed = line.TrimEnd('\r');
      AnsiConsole.MarkupLine(trimmed.Length > 0
          ? $"    {Markup.Escape(trimmed.TrimStart())}"
          : "");
    }

    // System prompt
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("  [dim]System prompt[/]");
    AnsiConsole.WriteLine();

    if (project.SystemPrompt is not null)
    {
      AnsiConsole.MarkupLine($"    {Markup.Escape(project.SystemPrompt)}");
    }
    else
    {
      AnsiConsole.MarkupLine($"    [dim](default)[/] {Markup.Escape(Project.DefaultSystemPrompt)}");
    }

    // JEA profiles
    if (project.Execution?.JeaProfiles is { Count: > 0 })
    {
      AnsiConsole.WriteLine();
      AnsiConsole.MarkupLine("  [dim]JEA profiles[/]");
      var profiles = string.Join(", ", project.Execution.JeaProfiles.Select(p => $"[cyan]{Markup.Escape(p)}[/]"));
      AnsiConsole.MarkupLine($"  {profiles}");
    }

    AnsiConsole.WriteLine();

    // Stale settings footer
    if (string.Equals(project.Name, _activeProject.Name, StringComparison.OrdinalIgnoreCase)
        && _ui.StaleSettingsWarning is not null)
    {
      AnsiConsole.MarkupLine($"  [yellow]* {Markup.Escape(_ui.StaleSettingsWarning)}[/]");
      AnsiConsole.WriteLine();
    }

    if (isMinimal)
    {
      AnsiConsole.MarkupLine($"  [dim]Tip: Use /project edit {Markup.Escape(project.Name)} to configure settings.[/]");
    }
  }

  // ──────────────────────────────────────────────
  //  EDIT
  // ──────────────────────────────────────────────

  private async Task HandleEditAsync(string[] tokens, CancellationToken ct)
  {
    if (!_ui.IsInteractive)
    {
      SpectreHelpers.Error("/project edit requires an interactive terminal.");
      return;
    }

    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : _activeProject.Name ?? SpectreHelpers.Ask<string>("Project name:");

    var project = await _projectRepository.LoadAsync(name, ct);
    if (project is null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Project [bold]{Markup.Escape(name)}[/] not found.");
      return;
    }

    var lastIndex = 0;
    while (true)
    {
      var dirSummary = project.Directories.Count > 0
          ? $"{project.Directories.Count} configured"
          : "[dim]none[/]";

      var promptSummary = project.SystemPrompt is not null
          ? "custom"
          : "[dim]default[/]";

      var dockerSummary = project.DockerImage is not null
          ? Markup.Escape(project.DockerImage)
          : "[dim]none[/]";

      var requireSummary = project.RequireContainer
          ? "[green]Yes[/]"
          : "[dim]No[/]";

      var choices = new List<string>
            {
                FormatEditChoice("Directories", dirSummary),
                FormatEditChoice("System prompt", promptSummary),
                FormatEditChoice("Docker image", dockerSummary),
                FormatEditChoice("Require container", requireSummary),
                "Done",
            };

      var choice = SpectreHelpers.Select($"Edit [bold]{Markup.Escape(project.Name)}[/]:", choices, lastIndex);

      if (choice == "Done")
      {
        break;
      }

      lastIndex = choices.IndexOf(choice);

      var section = choice.Split("  ", StringSplitOptions.RemoveEmptyEntries)[0].Trim();

      switch (section)
      {
        case "Directories":
          EditDirectories(project);
          break;
        case "System prompt":
          EditSystemPrompt(project);
          break;
        case "Docker image":
          EditDockerImage(project);
          break;
        case "Require container":
          EditRequireContainer(project);
          break;
      }

      await _projectRepository.SaveAsync(project, ct);
      RefreshSessionContext(project);

      if (section is "Docker image" or "Require container"
          && string.Equals(project.Name, _activeProject.Name, StringComparison.OrdinalIgnoreCase))
      {
        _ui.StaleSettingsWarning = "Project settings changed. Run /refresh to apply.";
      }

      SpectreHelpers.Success("Project saved.");
      AnsiConsole.WriteLine();
    }
  }

  /// <summary>
  /// After editing the active project, re-resolve directories and refresh
  /// both the DirectoryGuard and the session's system prompt so the LLM
  /// sees the current directory list on the next turn.
  /// </summary>
  private void RefreshSessionContext(Project project)
  {
    if (_activeSession.Session is null) return;
    if (!string.Equals(project.Name, _activeProject.Name, StringComparison.OrdinalIgnoreCase)) return;

    var resolved = _directoryResolver.Resolve(project.Directories);
    _directoryGuard.ConfigureResolved(resolved);
    _activeSession.Session.SystemPrompt = ChatCommand.BuildSystemPrompt(project, resolved, _activeEngine.Engine?.PathMappings);
  }

  // ──────────────────────────────────────────────
  //  DELETE
  // ──────────────────────────────────────────────

  private async Task HandleDeleteAsync(string[] tokens, CancellationToken ct)
  {
    if (tokens.Length <= 2 && !_ui.IsInteractive)
    {
      SpectreHelpers.Usage("/project delete <name>");
      return;
    }

    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : SpectreHelpers.Ask<string>("Project name:");

    if (name.Equals(Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase))
    {
      AnsiConsole.MarkupLine("[red]Error:[/] Cannot delete the ambient project [bold]_default[/].");
      return;
    }

    var project = await _projectRepository.LoadAsync(name, ct);
    if (project is null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Project [bold]{Markup.Escape(name)}[/] not found.");
      return;
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"  This will delete project [bold]{Markup.Escape(name)}[/]:");

    var details = new List<string>();

    if (project.Directories.Count > 0)
    {
      details.Add($"{project.Directories.Count} directory mapping(s)");
    }

    if (project.SystemPrompt is not null)
    {
      details.Add("Custom system prompt");
    }

    if (project.DockerImage is not null)
    {
      details.Add($"Docker image ({Markup.Escape(project.DockerImage)})");
    }

    if (project.RequireContainer)
    {
      details.Add("Container execution required");
    }

    if (details.Count > 0)
    {
      foreach (var detail in details)
      {
        AnsiConsole.MarkupLine($"    [dim]-[/] {detail}");
      }
    }
    else
    {
      AnsiConsole.MarkupLine("    [dim]No custom configuration.[/]");
    }

    AnsiConsole.WriteLine();

    if (!_ui.IsInteractive || !SpectreHelpers.Confirm($"Delete project [bold]{Markup.Escape(name)}[/]?", defaultValue: false))
    {
      SpectreHelpers.Cancelled();
      return;
    }

    await _projectRepository.DeleteAsync(name, ct);
    AnsiConsole.MarkupLine($"[green]v[/] Project [bold]{Markup.Escape(name)}[/] deleted.");
  }

  // ──────────────────────────────────────────────
  //  SHARED CREATE HELPERS
  // ──────────────────────────────────────────────

  private static void AddDirectoriesLoop(Project project)
  {
    while (true)
    {
      var path = SpectreHelpers.PromptOptional("  Directory path [dim](Enter to finish)[/]:");

      if (string.IsNullOrWhiteSpace(path))
      {
        break;
      }

      var accessLevel = SpectreHelpers.Select("  Access level:", [DirectoryAccessLevel.ReadWrite, DirectoryAccessLevel.ReadOnly]);

      project.Directories.Add(new ProjectDirectory(path, accessLevel));
      AnsiConsole.MarkupLine($"  [green]v[/] Added [bold]{Markup.Escape(path)}[/] ({accessLevel})");
      AnsiConsole.WriteLine();
    }
  }

  private static void PromptSystemPrompt(Project project)
  {
    var prompt = SpectreHelpers.PromptWithDefault("  Custom system prompt [dim](Enter for default)[/]:", Project.DefaultSystemPrompt);

    if (prompt == Project.DefaultSystemPrompt)
    {
      project.SystemPrompt = null;
    }
    else
    {
      project.SystemPrompt = prompt;
      SpectreHelpers.Success("System prompt set.");
    }
  }

  private static void ConfigureContainer(Project project)
  {
    var image = SpectreHelpers.PromptOptional("  Docker image [dim](Enter to skip)[/]:");

    if (!string.IsNullOrWhiteSpace(image))
    {
      project.DockerImage = image;
      AnsiConsole.MarkupLine($"  [green]v[/] Docker image set to [bold]{Markup.Escape(image)}[/].");

      project.RequireContainer = SpectreHelpers.Confirm("  Require container execution?", defaultValue: true);
      AnsiConsole.MarkupLine($"  [green]v[/] Require container: [bold]{project.RequireContainer}[/].");
    }
    else
    {
      SpectreHelpers.Dim("  Skipped container configuration.");
    }
  }

  // ──────────────────────────────────────────────
  //  EDIT SECTION HELPERS
  // ──────────────────────────────────────────────

  private static void EditDirectories(Project project)
  {
    if (project.Directories.Count > 0)
    {
      AnsiConsole.WriteLine();
      var dirTable = SpectreHelpers.SimpleTable("#", "Path", "Access");
      dirTable.Columns[0].RightAligned();

      for (var i = 0; i < project.Directories.Count; i++)
      {
        var dir = project.Directories[i];
        var accessStyle = dir.AccessLevel == DirectoryAccessLevel.ReadOnly
            ? "[yellow]ReadOnly[/]"
            : "[green]ReadWrite[/]";
        dirTable.AddRow(
            (i + 1).ToString(CultureInfo.InvariantCulture),
            Markup.Escape(dir.Path),
            accessStyle);
      }

      AnsiConsole.Write(dirTable);
    }
    else
    {
      SpectreHelpers.Dim("  No directories configured.");
    }

    var action = SpectreHelpers.Select("  Directory action:", ["Add directory", "Remove directory", "Change access level", "Back"]);

    switch (action)
    {
      case "Add directory":
        {
          var path = SpectreHelpers.PromptNonEmpty("  Directory path:");

          var accessLevel = SpectreHelpers.Select("  Access level:", [DirectoryAccessLevel.ReadWrite, DirectoryAccessLevel.ReadOnly]);

          project.Directories.Add(new ProjectDirectory(path, accessLevel));
          AnsiConsole.MarkupLine($"  [green]v[/] Added [bold]{Markup.Escape(path)}[/] ({accessLevel})");
          break;
        }
      case "Remove directory":
        {
          if (project.Directories.Count == 0)
          {
            AnsiConsole.MarkupLine("  [yellow]No directories to remove.[/]");
            break;
          }

          var pathToRemove = SpectreHelpers.Select("  Select directory to remove:", project.Directories.Select(d => d.Path));

          project.Directories.RemoveAll(d => d.Path == pathToRemove);
          AnsiConsole.MarkupLine($"  [green]v[/] Removed [bold]{Markup.Escape(pathToRemove)}[/]");
          break;
        }
      case "Change access level":
        {
          if (project.Directories.Count == 0)
          {
            AnsiConsole.MarkupLine("  [yellow]No directories to modify.[/]");
            break;
          }

          var pathToChange = SpectreHelpers.Select("  Select directory:", project.Directories.Select(d => d.Path));

          var newLevel = SpectreHelpers.Select("  New access level:", [DirectoryAccessLevel.ReadWrite, DirectoryAccessLevel.ReadOnly]);

          var index = project.Directories.FindIndex(d => d.Path == pathToChange);
          if (index >= 0)
          {
            project.Directories[index] = new ProjectDirectory(pathToChange, newLevel);
            AnsiConsole.MarkupLine($"  [green]v[/] Changed [bold]{Markup.Escape(pathToChange)}[/] to {newLevel}");
          }

          break;
        }
    }
  }

  private static void EditSystemPrompt(Project project)
  {
    SpectreHelpers.Dim("  The system prompt is always prefixed with the project name.");

    var currentCustom = project.SystemPrompt ?? Project.DefaultSystemPrompt;
    var isDefault = project.SystemPrompt is null;
    var label = isDefault ? "[dim](default)[/] " : "";
    AnsiConsole.MarkupLine($"  [bold]Current:[/] {label}{Markup.Escape(currentCustom)}");
    AnsiConsole.WriteLine();

    var choices = new List<string> { "Set new prompt", "Back" };
    if (!isDefault)
    {
      choices.Insert(1, "Reset to default");
    }

    var action = SpectreHelpers.Select("  System prompt:", choices);

    switch (action)
    {
      case "Set new prompt":
        project.SystemPrompt = SpectreHelpers.PromptNonEmpty("  New system prompt:");
        SpectreHelpers.Success("System prompt updated.");
        break;
      case "Reset to default":
        project.SystemPrompt = null;
        SpectreHelpers.Success("System prompt reset to default.");
        break;
    }
  }

  private static void EditDockerImage(Project project)
  {
    if (project.DockerImage is not null)
    {
      AnsiConsole.MarkupLine($"  [bold]Current:[/] {Markup.Escape(project.DockerImage)}");
    }
    else
    {
      AnsiConsole.MarkupLine("  [bold]Current:[/] [dim](not set)[/]");
    }

    AnsiConsole.WriteLine();

    var image = SpectreHelpers.PromptOptional("  Docker image [dim](Enter to clear)[/]:");

    if (string.IsNullOrWhiteSpace(image))
    {
      project.DockerImage = null;
      SpectreHelpers.Success("Docker image cleared.");
    }
    else
    {
      project.DockerImage = image;
      AnsiConsole.MarkupLine($"  [green]v[/] Docker image set to [bold]{Markup.Escape(image)}[/].");
    }
  }

  private static void EditRequireContainer(Project project)
  {
    AnsiConsole.MarkupLine($"  [bold]Current:[/] {(project.RequireContainer ? "[green]Yes[/]" : "No")}");

    if (project.DockerImage is null)
    {
      AnsiConsole.MarkupLine("  [yellow]Warning:[/] No Docker image is configured.");
    }

    AnsiConsole.WriteLine();

    project.RequireContainer = SpectreHelpers.Confirm("  Require container execution?", defaultValue: project.RequireContainer);
    AnsiConsole.MarkupLine($"  [green]v[/] Require container set to [bold]{project.RequireContainer}[/].");
  }

  // ──────────────────────────────────────────────
  //  FORMATTING HELPERS
  // ──────────────────────────────────────────────

  private static string FormatEditChoice(string label, string summary)
  {
    var padded = label.PadRight(20);
    return $"{padded}{summary}";
  }

}
