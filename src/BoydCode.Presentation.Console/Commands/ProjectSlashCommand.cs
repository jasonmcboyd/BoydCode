using System.Globalization;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.SlashCommands;
using BoydCode.Presentation.Console.Terminal;
using Spectre.Console;
using Terminal.Gui.Input;
using TguiApp = Terminal.Gui.App.Application;

#pragma warning disable CS0618 // Application.Invoke - using legacy static API during Terminal.Gui migration

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
      SpectreHelpers.Error($"Project '{name}' already exists.");
      return;
    }

    var project = new Project(name);
    await _projectRepository.SaveAsync(project, ct);
    SpectreHelpers.Success($"Project '{name}' created.");
    SpectreHelpers.OutputLine();

    if (!_ui.IsInteractive)
    {
      return;
    }

    var wantConfigure = SpectreHelpers.Confirm("Configure project settings now?", defaultValue: false);
    if (!wantConfigure)
    {
      SpectreHelpers.OutputMarkup($"[dim]Tip: Use /project edit {Markup.Escape(name)} to configure later.[/]");
      return;
    }

    SpectreHelpers.OutputLine();

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
    SpectreHelpers.OutputLine();
    SpectreHelpers.Success($"Project '{name}' saved.");
  }

  // ──────────────────────────────────────────────
  //  LIST
  // ──────────────────────────────────────────────

  private async Task HandleListAsync(CancellationToken ct)
  {
    var names = await _projectRepository.ListNamesAsync(ct);

    var projects = new List<Project>();
    foreach (var name in names)
    {
      var project = await _projectRepository.LoadAsync(name, ct);
      if (project is not null)
      {
        projects.Add(project);
      }
    }

    var spectreUi = _ui as SpectreUserInterface;
    if (spectreUi?.Toplevel is not null)
    {
      ShowInteractiveList(spectreUi, projects);
      return;
    }

    // Fallback: inline text output for non-interactive mode
    if (projects.Count == 0)
    {
      SpectreHelpers.OutputMarkup("No projects found.");
      SpectreHelpers.Dim("Create one with /project create <name>");
      return;
    }

    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup($"{"Name",-22}{"Dirs",6}  {"Docker",-28}{"Last used"}");
    SpectreHelpers.OutputMarkup(new string('\u2500', 75));

    foreach (var project in projects)
    {
      FormatProjectInlineRow(project);
    }

    SpectreHelpers.OutputLine();
  }

  private void FormatProjectInlineRow(Project project)
  {
    var resolvedDirs = _directoryResolver.Resolve(project.Directories);
    var gitCount = resolvedDirs.Count(d => d.IsGitRepository);
    var dirCount = gitCount > 0
        ? $"{project.Directories.Count.ToString(CultureInfo.InvariantCulture)} ({gitCount.ToString(CultureInfo.InvariantCulture)} git)"
        : project.Directories.Count.ToString(CultureInfo.InvariantCulture);

    var execLabel = project.DockerImage is not null
        ? project.RequireContainer
            ? $"{project.DockerImage} (required)"
            : project.DockerImage
        : "--";

    var lastUsed = project.LastAccessedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    var isAmbient = project.Name.Equals(Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase);
    var displayName = isAmbient ? $"{project.Name} (ambient)" : project.Name;

    SpectreHelpers.OutputMarkup(
      $"{Markup.Escape(displayName),-22}" +
      $"{dirCount,6}  " +
      $"{Markup.Escape(execLabel),-28}" +
      $"{lastUsed}");
  }

  private void ShowInteractiveList(SpectreUserInterface spectreUi, List<Project> projects)
  {
    var toplevel = spectreUi.Toplevel!;

    var actions = new List<ActionDefinition<Project>>
    {
      new(
        Key.Enter, "Show",
        item => { if (item is not null) _ = HandleShowAsync(["/project", "show", item.Name], default); },
        IsPrimary: true),
      new(
        Key.E, "Edit",
        item => { if (item is not null) _ = HandleEditAsync(["/project", "edit", item.Name], default); },
        HotkeyDisplay: "e"),
      new(
        Key.D, "Delete",
        item => { if (item is not null) _ = HandleDeleteAsync(["/project", "delete", item.Name], default); },
        HotkeyDisplay: "d"),
      new(
        Key.N, "New",
        _ => { var __ = HandleCreateAsync(["/project", "create"], default); },
        HotkeyDisplay: "n",
        RequiresSelection: false),
    };

    var window = new InteractiveListWindow<Project>(
      "Projects",
      projects,
      FormatProjectRow,
      actions,
      columnHeader: "Name              Dirs  Docker       Last used",
      emptyMessage: "No projects found.",
      emptyHint: "Use /project create to add one.");

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

  private string FormatProjectRow(Project project, int rowWidth)
  {
    var isAmbient = project.Name.Equals(Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase);
    var displayName = isAmbient ? $"{project.Name} (ambient)" : project.Name;

    if (displayName.Length > 18)
    {
      displayName = string.Concat(displayName.AsSpan(0, 15), "...");
    }

    var dirCount = project.Directories.Count.ToString(CultureInfo.InvariantCulture);

    var execLabel = project.DockerImage is not null
        ? project.RequireContainer
            ? $"{project.DockerImage} (req)"
            : project.DockerImage
        : "--";

    if (execLabel.Length > 13)
    {
      execLabel = string.Concat(execLabel.AsSpan(0, 10), "...");
    }

    var lastUsed = project.LastAccessedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    return $"{displayName,-18}{dirCount,4}  {execLabel,-13}{lastUsed}";
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
      SpectreHelpers.Error($"Project '{name}' not found.");
      return;
    }

    var isMinimal = project.Directories.Count == 0
        && project.SystemPrompt is null
        && project.DockerImage is null
        && !project.RequireContainer
        && project.Execution is null;

    SpectreHelpers.OutputLine();

    // Info rows
    var isAmbient = name.Equals(Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase);
    var displayName = isAmbient ? $"{project.Name} (ambient)" : project.Name;
    SpectreHelpers.OutputMarkup($"  [dim]{"Project",-14}[/][cyan]{Markup.Escape(displayName)}[/]");
    SpectreHelpers.OutputMarkup(
      $"  [dim]{"Created",-14}[/][cyan]{project.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}[/]" +
      $"    [dim]{"Last used",-12}[/][cyan]{project.LastAccessedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}[/]");

    if (project.Execution is not null)
    {
      SpectreHelpers.OutputMarkup($"  [dim]{"Engine",-14}[/][cyan]{Markup.Escape(project.Execution.Mode.ToString())}[/]");
    }

    if (project.DockerImage is not null)
    {
      SpectreHelpers.OutputMarkup($"  [dim]{"Docker",-14}[/][cyan]{Markup.Escape(project.DockerImage)}[/]");
    }

    if (project.DockerImage is not null || project.RequireContainer)
    {
      var containerStatus = project.RequireContainer ? "Required" : "Optional";
      SpectreHelpers.OutputMarkup($"  [dim]{"Container",-14}[/]{containerStatus}");
    }

    // Directories
    if (project.Directories.Count > 0)
    {
      SpectreHelpers.OutputLine();
      SpectreHelpers.OutputMarkup("  [dim]Directories[/]");

      var resolvedDirs = _directoryResolver.Resolve(project.Directories);

      foreach (var dir in resolvedDirs)
      {
        var accessLabel = dir.AccessLevel == DirectoryAccessLevel.ReadOnly
            ? "ReadOnly"
            : "ReadWrite";

        var gitInfo = dir switch
        {
          { Exists: false } => "missing",
          { IsGitRepository: true, GitBranch: not null } => dir.GitBranch,
          { IsGitRepository: true } => "git",
          _ => "--",
        };

        SpectreHelpers.OutputMarkup($"    - {Markup.Escape(dir.Path),-40} {accessLabel,-12} {gitInfo}");
      }
    }

    // Meta prompt
    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup("  [dim]Meta prompt[/]");
    SpectreHelpers.OutputLine();
    var fullMetaPrompt = MetaPrompt.Build(_activeEngine.Mode, _activeEngine.Engine?.GetAvailableCommands() ?? []);
    foreach (var line in fullMetaPrompt.Trim().Split('\n'))
    {
      var trimmed = line.TrimEnd('\r');
      SpectreHelpers.OutputMarkup(trimmed.Length > 0
          ? $"    {Markup.Escape(trimmed.TrimStart())}"
          : "");
    }

    // System prompt
    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup("  [dim]System prompt[/]");
    SpectreHelpers.OutputLine();

    if (project.SystemPrompt is not null)
    {
      SpectreHelpers.OutputMarkup($"    {Markup.Escape(project.SystemPrompt)}");
    }
    else
    {
      SpectreHelpers.OutputMarkup($"    [dim](default)[/] {Markup.Escape(Project.DefaultSystemPrompt)}");
    }

    // JEA profiles
    if (project.Execution?.JeaProfiles is { Count: > 0 })
    {
      SpectreHelpers.OutputLine();
      SpectreHelpers.OutputMarkup("  [dim]JEA profiles[/]");
      var profiles = string.Join(", ", project.Execution.JeaProfiles.Select(p => $"[cyan]{Markup.Escape(p)}[/]"));
      SpectreHelpers.OutputMarkup($"  {profiles}");
    }

    SpectreHelpers.OutputLine();

    // Stale settings footer
    if (string.Equals(project.Name, _activeProject.Name, StringComparison.OrdinalIgnoreCase)
        && _ui.StaleSettingsWarning is not null)
    {
      SpectreHelpers.OutputMarkup($"  [yellow]* {Markup.Escape(_ui.StaleSettingsWarning)}[/]");
      SpectreHelpers.OutputLine();
    }

    if (isMinimal)
    {
      SpectreHelpers.OutputMarkup($"  [dim]Tip: Use /project edit {Markup.Escape(project.Name)} to configure settings.[/]");
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
      SpectreHelpers.Error($"Project '{name}' not found.");
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
        _ui.StaleSettingsWarning = "Project settings changed. Run /context refresh to apply.";
      }

      SpectreHelpers.Success("Project saved.");
      SpectreHelpers.OutputLine();
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
      SpectreHelpers.Error("Cannot delete the ambient project '_default'.");
      return;
    }

    var project = await _projectRepository.LoadAsync(name, ct);
    if (project is null)
    {
      SpectreHelpers.Error($"Project '{name}' not found.");
      return;
    }

    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup($"  This will delete project [bold]{Markup.Escape(name)}[/]:");

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
        SpectreHelpers.OutputMarkup($"    [dim]-[/] {detail}");
      }
    }
    else
    {
      SpectreHelpers.OutputMarkup("    [dim]No custom configuration.[/]");
    }

    SpectreHelpers.OutputLine();

    if (!_ui.IsInteractive || !SpectreHelpers.Confirm($"Delete project [bold]{Markup.Escape(name)}[/]?", defaultValue: false))
    {
      SpectreHelpers.Cancelled();
      return;
    }

    await _projectRepository.DeleteAsync(name, ct);
    SpectreHelpers.Success($"Project '{name}' deleted.");
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
      SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Added [bold]{Markup.Escape(path)}[/] ({accessLevel})");
      SpectreHelpers.OutputLine();
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
      SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Docker image set to [bold]{Markup.Escape(image)}[/].");

      project.RequireContainer = SpectreHelpers.Confirm("  Require container execution?", defaultValue: true);
      SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Require container: [bold]{project.RequireContainer}[/].");
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
      SpectreHelpers.OutputLine();
      SpectreHelpers.OutputMarkup($"  {"#",3}  {"Path",-40}  {"Access"}");
      SpectreHelpers.OutputMarkup($"  {new string('\u2500', 55)}");

      for (var i = 0; i < project.Directories.Count; i++)
      {
        var dir = project.Directories[i];
        var accessLabel = dir.AccessLevel == DirectoryAccessLevel.ReadOnly
            ? "ReadOnly"
            : "ReadWrite";
        SpectreHelpers.OutputMarkup(
          $"  {(i + 1).ToString(CultureInfo.InvariantCulture),3}  {Markup.Escape(dir.Path),-40}  {accessLabel}");
      }

      SpectreHelpers.OutputLine();
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
          SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Added [bold]{Markup.Escape(path)}[/] ({accessLevel})");
          break;
        }
      case "Remove directory":
        {
          if (project.Directories.Count == 0)
          {
            SpectreHelpers.OutputMarkup("  [yellow]No directories to remove.[/]");
            break;
          }

          var pathToRemove = SpectreHelpers.Select("  Select directory to remove:", project.Directories.Select(d => d.Path));

          project.Directories.RemoveAll(d => d.Path == pathToRemove);
          SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Removed [bold]{Markup.Escape(pathToRemove)}[/]");
          break;
        }
      case "Change access level":
        {
          if (project.Directories.Count == 0)
          {
            SpectreHelpers.OutputMarkup("  [yellow]No directories to modify.[/]");
            break;
          }

          var pathToChange = SpectreHelpers.Select("  Select directory:", project.Directories.Select(d => d.Path));

          var newLevel = SpectreHelpers.Select("  New access level:", [DirectoryAccessLevel.ReadWrite, DirectoryAccessLevel.ReadOnly]);

          var index = project.Directories.FindIndex(d => d.Path == pathToChange);
          if (index >= 0)
          {
            project.Directories[index] = new ProjectDirectory(pathToChange, newLevel);
            SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Changed [bold]{Markup.Escape(pathToChange)}[/] to {newLevel}");
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
    SpectreHelpers.OutputMarkup($"  [bold]Current:[/] {label}{Markup.Escape(currentCustom)}");
    SpectreHelpers.OutputLine();

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
      SpectreHelpers.OutputMarkup($"  [bold]Current:[/] {Markup.Escape(project.DockerImage)}");
    }
    else
    {
      SpectreHelpers.OutputMarkup("  [bold]Current:[/] [dim](not set)[/]");
    }

    SpectreHelpers.OutputLine();

    var image = SpectreHelpers.PromptOptional("  Docker image [dim](Enter to clear)[/]:");

    if (string.IsNullOrWhiteSpace(image))
    {
      project.DockerImage = null;
      SpectreHelpers.Success("Docker image cleared.");
    }
    else
    {
      project.DockerImage = image;
      SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Docker image set to [bold]{Markup.Escape(image)}[/].");
    }
  }

  private static void EditRequireContainer(Project project)
  {
    SpectreHelpers.OutputMarkup($"  [bold]Current:[/] {(project.RequireContainer ? "[green]Yes[/]" : "No")}");

    if (project.DockerImage is null)
    {
      SpectreHelpers.Warning("No Docker image is configured.");
    }

    SpectreHelpers.OutputLine();

    project.RequireContainer = SpectreHelpers.Confirm("  Require container execution?", defaultValue: project.RequireContainer);
    SpectreHelpers.OutputMarkup($"  [green]\u2713[/] Require container set to [bold]{project.RequireContainer}[/].");
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
