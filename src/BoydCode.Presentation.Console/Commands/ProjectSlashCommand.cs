using System.Globalization;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.SlashCommands;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed class ProjectSlashCommand : ISlashCommand
{
  private readonly IProjectRepository _projectRepository;
  private readonly DirectoryResolver _directoryResolver;
  private readonly ActiveProject _activeProject;

  public ProjectSlashCommand(IProjectRepository projectRepository, DirectoryResolver directoryResolver, ActiveProject activeProject)
  {
    _projectRepository = projectRepository;
    _directoryResolver = directoryResolver;
    _activeProject = activeProject;
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
        AnsiConsole.MarkupLine("[yellow]Usage:[/] /project create|list|show|edit|delete");
        break;
    }

    return true;
  }

  // ──────────────────────────────────────────────
  //  CREATE
  // ──────────────────────────────────────────────

  private async Task HandleCreateAsync(string[] tokens, CancellationToken ct)
  {
    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : AnsiConsole.Prompt(
            new TextPrompt<string>("Project [green]name[/]:")
                .Validate(n => !string.IsNullOrWhiteSpace(n)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Name cannot be empty")));

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

    var wantConfigure = AnsiConsole.Confirm("Configure project settings now?", defaultValue: false);
    if (!wantConfigure)
    {
      AnsiConsole.MarkupLine($"[dim]Tip: Use /project edit {Markup.Escape(name)} to configure later.[/]");
      return;
    }

    AnsiConsole.WriteLine();

    var sections = AnsiConsole.Prompt(
        new MultiSelectionPrompt<string>()
            .Title("Which settings would you like to configure?")
            .NotRequired()
            .InstructionsText("[dim](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]")
            .AddChoices("Directories", "System prompt", "Permission mode", "Container settings"));

    if (sections.Contains("Directories"))
    {
      AnsiConsole.WriteLine();
      AnsiConsole.Write(new Rule("[bold]Directories[/]").LeftJustified().RuleStyle("dim"));
      AddDirectoriesLoop(project);
    }

    if (sections.Contains("System prompt"))
    {
      AnsiConsole.WriteLine();
      AnsiConsole.Write(new Rule("[bold]System prompt[/]").LeftJustified().RuleStyle("dim"));
      PromptSystemPrompt(project);
    }

    if (sections.Contains("Permission mode"))
    {
      AnsiConsole.WriteLine();
      AnsiConsole.Write(new Rule("[bold]Permission mode[/]").LeftJustified().RuleStyle("dim"));
      PromptPermissionMode(project);
    }

    if (sections.Contains("Container settings"))
    {
      AnsiConsole.WriteLine();
      AnsiConsole.Write(new Rule("[bold]Container settings[/]").LeftJustified().RuleStyle("dim"));
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
      AnsiConsole.MarkupLine("[dim]Create one with[/] /project create <name>");
      return;
    }

    var table = new Table()
        .Border(TableBorder.Simple)
        .AddColumn(new TableColumn("[bold]Name[/]"))
        .AddColumn(new TableColumn("[bold]Dirs[/]").RightAligned())
        .AddColumn(new TableColumn("[bold]Permissions[/]"))
        .AddColumn(new TableColumn("[bold]Docker[/]"))
        .AddColumn(new TableColumn("[bold]Last used[/]"));

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

      var permLabel = project.PermissionMode is not null
          ? project.PermissionMode.Value.ToString()
          : "[dim](default)[/]";

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
          permLabel,
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
    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : _activeProject.Name ?? AnsiConsole.Ask<string>("Project name:");

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
        && project.PermissionMode is null
        && project.Execution is null;

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"  [bold]Project:[/]    {Markup.Escape(project.Name)}");
    AnsiConsole.MarkupLine($"  [bold]Created:[/]    {project.CreatedAt:yyyy-MM-dd HH:mm:ss}");
    AnsiConsole.MarkupLine($"  [bold]Last used:[/]  {project.LastAccessedAt:yyyy-MM-dd HH:mm:ss}");

    if (project.Directories.Count > 0)
    {
      AnsiConsole.WriteLine();
      AnsiConsole.Write(new Rule("[bold]Directories[/]").LeftJustified().RuleStyle("dim"));

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

        dirTable.AddRow(Markup.Escape(dir.Path), accessStyle, gitInfo);
      }

      AnsiConsole.Write(dirTable);
    }

    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule("[bold]System prompt[/]").LeftJustified().RuleStyle("dim"));

    if (project.SystemPrompt is not null)
    {
      AnsiConsole.MarkupLine($"  {Markup.Escape(project.SystemPrompt)}");
    }
    else
    {
      AnsiConsole.MarkupLine($"  [dim](default)[/] {Markup.Escape(Project.DefaultSystemPrompt)}");
    }

    if (project.DockerImage is not null || project.RequireContainer)
    {
      AnsiConsole.WriteLine();
      AnsiConsole.Write(new Rule("[bold]Container[/]").LeftJustified().RuleStyle("dim"));
      AnsiConsole.MarkupLine($"  [bold]Docker image:[/]      {(project.DockerImage is not null ? Markup.Escape(project.DockerImage) : "[dim](not set)[/]")}");
      AnsiConsole.MarkupLine($"  [bold]Require container:[/] {(project.RequireContainer ? "[green]Yes[/]" : "[yellow]No[/]")}");
    }

    if (project.PermissionMode is not null || project.Execution?.JeaProfiles is { Count: > 0 })
    {
      AnsiConsole.WriteLine();
      AnsiConsole.Write(new Rule("[bold]Security[/]").LeftJustified().RuleStyle("dim"));

      if (project.PermissionMode is not null)
      {
        AnsiConsole.MarkupLine($"  [bold]Permission mode:[/]     {project.PermissionMode}");
      }

      if (project.Execution?.JeaProfiles is { Count: > 0 })
      {
        var profiles = string.Join(", ", project.Execution.JeaProfiles.Select(Markup.Escape));
        AnsiConsole.MarkupLine($"  [bold]JEA profiles:[/]      {profiles}");
      }
    }

    AnsiConsole.WriteLine();

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
    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : _activeProject.Name ?? AnsiConsole.Ask<string>("Project name:");

    var project = await _projectRepository.LoadAsync(name, ct);
    if (project is null)
    {
      AnsiConsole.MarkupLine($"[red]Error:[/] Project [bold]{Markup.Escape(name)}[/] not found.");
      return;
    }

    while (true)
    {
      var dirSummary = project.Directories.Count > 0
          ? $"{project.Directories.Count} configured"
          : "[dim]none[/]";

      var promptSummary = project.SystemPrompt is not null
          ? "custom"
          : "[dim]default[/]";

      var permSummary = project.PermissionMode is not null
          ? project.PermissionMode.Value.ToString()
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
                FormatEditChoice("Permission mode", permSummary),
                FormatEditChoice("Docker image", dockerSummary),
                FormatEditChoice("Require container", requireSummary),
                "Done",
            };

      var choice = AnsiConsole.Prompt(
          new SelectionPrompt<string>()
              .Title($"Edit [bold]{Markup.Escape(project.Name)}[/]:")
              .AddChoices(choices)
              .HighlightStyle(new Style(Color.Green)));

      if (choice == "Done")
      {
        break;
      }

      var section = choice.Split("  ", StringSplitOptions.RemoveEmptyEntries)[0].Trim();

      switch (section)
      {
        case "Directories":
          EditDirectories(project);
          break;
        case "System":
          EditSystemPrompt(project);
          break;
        case "Permission":
          EditPermissionMode(project);
          break;
        case "Docker":
          EditDockerImage(project);
          break;
        case "Require":
          EditRequireContainer(project);
          break;
      }

      await _projectRepository.SaveAsync(project, ct);
      AnsiConsole.MarkupLine("[green]v[/] Project saved.");
      AnsiConsole.WriteLine();
    }
  }

  // ──────────────────────────────────────────────
  //  DELETE
  // ──────────────────────────────────────────────

  private async Task HandleDeleteAsync(string[] tokens, CancellationToken ct)
  {
    var name = tokens.Length > 2
        ? string.Join(' ', tokens.Skip(2))
        : AnsiConsole.Ask<string>("Project name:");

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

    if (project.PermissionMode is not null)
    {
      details.Add($"Permission mode ({project.PermissionMode})");
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

    if (!AnsiConsole.Confirm($"Delete project [bold]{Markup.Escape(name)}[/]?", defaultValue: false))
    {
      AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
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
      var path = AnsiConsole.Prompt(
          new TextPrompt<string>("  Directory path [dim](Enter to finish)[/]:")
              .AllowEmpty());

      if (string.IsNullOrWhiteSpace(path))
      {
        break;
      }

      var accessLevel = AnsiConsole.Prompt(
          new SelectionPrompt<DirectoryAccessLevel>()
              .Title("  Access level:")
              .AddChoices(DirectoryAccessLevel.ReadWrite, DirectoryAccessLevel.ReadOnly));

      project.Directories.Add(new ProjectDirectory(path, accessLevel));
      AnsiConsole.MarkupLine($"  [green]v[/] Added [bold]{Markup.Escape(path)}[/] ({accessLevel})");
      AnsiConsole.WriteLine();
    }
  }

  private static void PromptSystemPrompt(Project project)
  {
    var prompt = AnsiConsole.Prompt(
        new TextPrompt<string>("  Custom system prompt [dim](Enter for default)[/]:")
            .DefaultValue(Project.DefaultSystemPrompt)
            .ShowDefaultValue());

    if (prompt == Project.DefaultSystemPrompt)
    {
      project.SystemPrompt = null;
    }
    else
    {
      project.SystemPrompt = prompt;
      AnsiConsole.MarkupLine("  [green]v[/] System prompt set.");
    }
  }

  private static void PromptPermissionMode(Project project)
  {
    var mode = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("  Permission mode:")
            .AddChoices(
                "Default",
                "AcceptEdits",
                "Plan",
                "DontAsk",
                "BypassPermissions"));

    if (mode is "DontAsk" or "BypassPermissions")
    {
      AnsiConsole.MarkupLine($"  [yellow]Warning:[/] [bold]{mode}[/] reduces safety checks.");

      if (!AnsiConsole.Confirm("  Continue?", defaultValue: false))
      {
        return;
      }
    }

    project.PermissionMode = Enum.Parse<PermissionMode>(mode);
    AnsiConsole.MarkupLine($"  [green]v[/] Permission mode set to [bold]{mode}[/].");
  }

  private static void ConfigureContainer(Project project)
  {
    var image = AnsiConsole.Prompt(
        new TextPrompt<string>("  Docker image [dim](Enter to skip)[/]:")
            .AllowEmpty());

    if (!string.IsNullOrWhiteSpace(image))
    {
      project.DockerImage = image;
      AnsiConsole.MarkupLine($"  [green]v[/] Docker image set to [bold]{Markup.Escape(image)}[/].");

      project.RequireContainer = AnsiConsole.Confirm("  Require container execution?", defaultValue: true);
      AnsiConsole.MarkupLine($"  [green]v[/] Require container: [bold]{project.RequireContainer}[/].");
    }
    else
    {
      AnsiConsole.MarkupLine("  [dim]Skipped container configuration.[/]");
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
      var dirTable = new Table()
          .Border(TableBorder.Simple)
          .AddColumn(new TableColumn("[bold]#[/]").RightAligned())
          .AddColumn("[bold]Path[/]")
          .AddColumn("[bold]Access[/]");

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
      AnsiConsole.MarkupLine("  [dim]No directories configured.[/]");
    }

    var action = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("  Directory action:")
            .AddChoices("Add directory", "Remove directory", "Change access level", "Back"));

    switch (action)
    {
      case "Add directory":
        {
          var path = AnsiConsole.Prompt(
              new TextPrompt<string>("  Directory path:")
                  .Validate(p => !string.IsNullOrWhiteSpace(p)
                      ? ValidationResult.Success()
                      : ValidationResult.Error("Path cannot be empty")));

          var accessLevel = AnsiConsole.Prompt(
              new SelectionPrompt<DirectoryAccessLevel>()
                  .Title("  Access level:")
                  .AddChoices(DirectoryAccessLevel.ReadWrite, DirectoryAccessLevel.ReadOnly));

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

          var pathToRemove = AnsiConsole.Prompt(
              new SelectionPrompt<string>()
                  .Title("  Select directory to remove:")
                  .AddChoices(project.Directories.Select(d => d.Path)));

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

          var pathToChange = AnsiConsole.Prompt(
              new SelectionPrompt<string>()
                  .Title("  Select directory:")
                  .AddChoices(project.Directories.Select(d => d.Path)));

          var newLevel = AnsiConsole.Prompt(
              new SelectionPrompt<DirectoryAccessLevel>()
                  .Title("  New access level:")
                  .AddChoices(DirectoryAccessLevel.ReadWrite, DirectoryAccessLevel.ReadOnly));

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
    AnsiConsole.MarkupLine("  [dim]The system prompt is always prefixed with the project name.[/]");

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

    var action = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("  System prompt:")
            .AddChoices(choices));

    switch (action)
    {
      case "Set new prompt":
        project.SystemPrompt = AnsiConsole.Prompt(
            new TextPrompt<string>("  New system prompt:")
                .Validate(p => !string.IsNullOrWhiteSpace(p)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Prompt cannot be empty")));
        AnsiConsole.MarkupLine("  [green]v[/] System prompt updated.");
        break;
      case "Reset to default":
        project.SystemPrompt = null;
        AnsiConsole.MarkupLine("  [green]v[/] System prompt reset to default.");
        break;
    }
  }

  private static void EditPermissionMode(Project project)
  {
    if (project.PermissionMode is not null)
    {
      AnsiConsole.MarkupLine($"  [bold]Current:[/] {project.PermissionMode}");
    }
    else
    {
      AnsiConsole.MarkupLine("  [bold]Current:[/] [dim](using global default)[/]");
    }

    AnsiConsole.WriteLine();

    var selection = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("  Permission mode:")
            .AddChoices(
                "Default",
                "AcceptEdits",
                "Plan",
                "DontAsk",
                "BypassPermissions",
                "Clear (use global default)",
                "Back"));

    if (selection == "Back")
    {
      return;
    }

    if (selection is "DontAsk" or "BypassPermissions")
    {
      AnsiConsole.MarkupLine($"  [yellow]Warning:[/] [bold]{selection}[/] reduces safety checks.");

      if (!AnsiConsole.Confirm("  Are you sure?", defaultValue: false))
      {
        return;
      }
    }

    if (selection == "Clear (use global default)")
    {
      project.PermissionMode = null;
      AnsiConsole.MarkupLine("  [green]v[/] Permission mode cleared (will use global default).");
    }
    else
    {
      project.PermissionMode = Enum.Parse<PermissionMode>(selection);
      AnsiConsole.MarkupLine($"  [green]v[/] Permission mode set to [bold]{selection}[/].");
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

    var image = AnsiConsole.Prompt(
        new TextPrompt<string>("  Docker image [dim](Enter to clear)[/]:")
            .AllowEmpty());

    if (string.IsNullOrWhiteSpace(image))
    {
      project.DockerImage = null;
      AnsiConsole.MarkupLine("  [green]v[/] Docker image cleared.");
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

    project.RequireContainer = AnsiConsole.Confirm("  Require container execution?", defaultValue: project.RequireContainer);
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
