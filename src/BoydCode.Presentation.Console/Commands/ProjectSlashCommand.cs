using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.SlashCommands;
using BoydCode.Presentation.Console.Terminal;
using Spectre.Console;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TguiApp = Terminal.Gui.App.Application;
using WizardDialog = BoydCode.Presentation.Console.Terminal.WizardDialog;
using WizardStep = BoydCode.Presentation.Console.Terminal.WizardStep;

#pragma warning disable CS0618 // Application.Invoke/Run/RequestStop - using legacy static API during Terminal.Gui migration

namespace BoydCode.Presentation.Console.Commands;

public sealed partial class ProjectSlashCommand : ISlashCommand
{
  private static readonly string[] ConfigureSections = ["Directories", "System prompt", "Container settings"];
  private static readonly string[] YesNoOptions = ["No", "Yes"];

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
    if (!_ui.IsInteractive)
    {
      if (tokens.Length <= 2)
      {
        SpectreHelpers.Usage("/project create <name>");
        return;
      }

      // Non-interactive: create bare project
      var bareName = string.Join(' ', tokens.Skip(2));
      var bareExisting = await _projectRepository.LoadAsync(bareName, ct);
      if (bareExisting is not null)
      {
        SpectreHelpers.Error($"Project '{bareName}' already exists.");
        return;
      }

      var bareProject = new Project(bareName);
      await _projectRepository.SaveAsync(bareProject, ct);
      SpectreHelpers.Success($"Project '{bareName}' created.");
      return;
    }

    if (SpectreUserInterface.Current?.Toplevel is not null)
    {
      await RunProjectCreateWizard(tokens, ct);
    }
    else
    {
      await RunProjectCreateSpectre(tokens, ct);
    }
  }

  private async Task RunProjectCreateSpectre(string[] tokens, CancellationToken ct)
  {
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

  private async Task RunProjectCreateWizard(string[] tokens, CancellationToken ct)
  {
    var existingNames = (await _projectRepository.ListNamesAsync(ct))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var projectName = tokens.Length > 2 ? string.Join(' ', tokens.Skip(2)) : string.Empty;
    var directories = new List<ProjectDirectory>();
    var systemPrompt = string.Empty;
    var dockerImage = string.Empty;
    var requireContainer = false;
    Label? nameError = null;

    var steps = new List<WizardStep>
    {
      // Step 1: Name + Directories
      new WizardStep(
        "Name & Directories",
        () =>
        {
          var container = new View
          {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
          };

          var nameLabel = new Label
          {
            Text = "Project name:",
            X = 0,
            Y = 0,
          };

          var nameField = new TextField
          {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Text = projectName,
          };

          nameError = new Label
          {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Visible = false,
          };
          nameError.SetScheme(new Scheme(Theme.Semantic.Error));

          nameField.TextChanged += (_, _) =>
          {
            projectName = nameField.Text ?? string.Empty;
            if (nameError is not null)
            {
              nameError.Visible = false;
            }
          };

          var dirLabel = new Label
          {
            Text = "Directories:",
            X = 0,
            Y = 4,
          };

          var dirListView = new View
          {
            X = 0,
            Y = 5,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
          };

          RebuildDirectoryList(dirListView, directories);

          var addDirButton = new Button
          {
            Text = "Add Directory",
            X = 0,
            Y = Pos.AnchorEnd(1),
          };

          addDirButton.Accepting += (_, args) =>
          {
            args.Handled = true;
            ShowAddDirectoryDialog(directories);
            RebuildDirectoryList(dirListView, directories);
          };

          container.Add(nameLabel, nameField, nameError, dirLabel, dirListView, addDirButton);
          return container;
        },
        () =>
        {
          // Validation
          if (string.IsNullOrWhiteSpace(projectName))
          {
            if (nameError is not null)
            {
              nameError.Text = "Project name cannot be empty.";
              nameError.Visible = true;
            }

            return false;
          }

          if (!ProjectNameRegex().IsMatch(projectName))
          {
            if (nameError is not null)
            {
              nameError.Text = "Name must contain only letters, numbers, hyphens, and underscores.";
              nameError.Visible = true;
            }

            return false;
          }

          if (existingNames.Contains(projectName))
          {
            if (nameError is not null)
            {
              nameError.Text = $"Project '{projectName}' already exists.";
              nameError.Visible = true;
            }

            return false;
          }

          return true;
        }),

      // Step 2: System Prompt
      new WizardStep(
        "System Prompt",
        () =>
        {
          var container = new View
          {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
          };

          var hintLabel = new Label
          {
            Text = "Custom system prompt (optional, leave empty for default):",
            X = 0,
            Y = 0,
          };
          hintLabel.SetScheme(new Scheme(Theme.Semantic.Muted));

          var promptView = new TextView
          {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = 5,
            WordWrap = true,
            Text = systemPrompt,
          };

          promptView.TextChanged += (_, _) =>
          {
            systemPrompt = promptView.Text ?? string.Empty;
          };

          container.Add(hintLabel, promptView);
          return container;
        }),

      // Step 3: Container Settings
      new WizardStep(
        "Container Settings",
        () =>
        {
          var container = new View
          {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
          };

          var imageLabel = new Label
          {
            Text = "Docker image (optional):",
            X = 0,
            Y = 0,
          };

          var imageField = new TextField
          {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Text = dockerImage,
          };

          imageField.TextChanged += (_, _) =>
          {
            dockerImage = imageField.Text ?? string.Empty;
          };

          var requireLabel = new Label
          {
            Text = "Require container execution?",
            X = 0,
            Y = 3,
          };

          var requireOptions = YesNoOptions;
          var requireListView = new ListView
          {
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
            Height = 2,
          };
          requireListView.SetSource(new ObservableCollection<string>(requireOptions));
          requireListView.SelectedItem = requireContainer ? 1 : 0;

          requireListView.ValueChanged += (_, args) =>
          {
            requireContainer = args.NewValue == 1;
          };

          container.Add(imageLabel, imageField, requireLabel, requireListView);
          return container;
        }),

      // Step 4: Review
      new WizardStep(
        "Review",
        () =>
        {
          var container = new View
          {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
          };

          var y = 0;

          var nameLabel = new Label
          {
            Text = $"Name:        {projectName}",
            X = 0,
            Y = y++,
          };
          container.Add(nameLabel);

          var dirSummary = directories.Count == 0
              ? "none"
              : $"{directories.Count} configured";
          var dirLabel = new Label
          {
            Text = $"Directories: {dirSummary}",
            X = 0,
            Y = y++,
          };
          container.Add(dirLabel);

          foreach (var dir in directories)
          {
            var dirEntry = new Label
            {
              Text = $"  {dir.Path} ({dir.AccessLevel})",
              X = 0,
              Y = y++,
            };
            dirEntry.SetScheme(new Scheme(Theme.Semantic.Muted));
            container.Add(dirEntry);
          }

          var promptSummary = string.IsNullOrWhiteSpace(systemPrompt)
              ? "(default)"
              : systemPrompt.Length > 50
                  ? systemPrompt[..50] + "..."
                  : systemPrompt;
          var promptLabel = new Label
          {
            Text = $"Prompt:      {promptSummary}",
            X = 0,
            Y = y++,
          };
          container.Add(promptLabel);

          var dockerSummary = string.IsNullOrWhiteSpace(dockerImage)
              ? "(not set)"
              : dockerImage;
          var dockerLabel = new Label
          {
            Text = $"Docker:      {dockerSummary}",
            X = 0,
            Y = y++,
          };
          container.Add(dockerLabel);

          var requireSummary = requireContainer ? "Yes" : "No";
          var requireLabel = new Label
          {
            Text = $"Require:     {requireSummary}",
            X = 0,
            Y = y++,
          };
          container.Add(requireLabel);

          return container;
        }),
    };

    using var wizard = new WizardDialog(
      "Create Project",
      steps,
      doneButtonText: "Create",
      hasUnsavedData: () =>
        !string.IsNullOrWhiteSpace(projectName) ||
        directories.Count > 0 ||
        !string.IsNullOrWhiteSpace(systemPrompt) ||
        !string.IsNullOrWhiteSpace(dockerImage));

    var result = wizard.Show();

    if (!result.Completed)
    {
      SpectreHelpers.Cancelled();
      return;
    }

    var project = new Project(projectName);
    project.Directories.AddRange(directories);

    if (!string.IsNullOrWhiteSpace(systemPrompt))
    {
      project.SystemPrompt = systemPrompt;
    }

    if (!string.IsNullOrWhiteSpace(dockerImage))
    {
      project.DockerImage = dockerImage;
    }

    project.RequireContainer = requireContainer;

    await _projectRepository.SaveAsync(project, ct);
    SpectreHelpers.Success($"Project '{projectName}' created.");
  }

  private static void RebuildDirectoryList(View dirListView, List<ProjectDirectory> directories)
  {
    dirListView.RemoveAll();

    if (directories.Count == 0)
    {
      var emptyLabel = new Label
      {
        Text = "(no directories added)",
        X = 0,
        Y = 0,
      };
      emptyLabel.SetScheme(new Scheme(Theme.Semantic.Muted));
      dirListView.Add(emptyLabel);
    }
    else
    {
      for (var i = 0; i < directories.Count; i++)
      {
        var dir = directories[i];
        var dirLabel = new Label
        {
          Text = $"  {dir.Path}  ({dir.AccessLevel})",
          X = 0,
          Y = i,
        };
        dirListView.Add(dirLabel);
      }
    }

    dirListView.SetNeedsDraw();
  }

  private static void ShowAddDirectoryDialog(List<ProjectDirectory> directories)
  {
    var path = string.Empty;
    var accessLevel = DirectoryAccessLevel.ReadWrite;

    var dialog = new Dialog
    {
      Title = "Add Directory",
      Width = Dim.Percent(50),
      Height = 12,
      BorderStyle = LineStyle.Rounded,
    };
    dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

    var pathLabel = new Label
    {
      Text = "Path:",
      X = 2,
      Y = 1,
    };

    var pathField = new TextField
    {
      X = 2,
      Y = 2,
      Width = Dim.Fill(2),
    };

    pathField.TextChanged += (_, _) =>
    {
      path = pathField.Text ?? string.Empty;
    };

    var accessLabel = new Label
    {
      Text = "Access level:",
      X = 2,
      Y = 4,
    };

    var accessOptions = new[] { "ReadWrite", "ReadOnly" };
    var accessListView = new ListView
    {
      X = 2,
      Y = 5,
      Width = Dim.Fill(2),
      Height = 2,
    };
    accessListView.SetSource(new ObservableCollection<string>(accessOptions));
    accessListView.SelectedItem = 0;

    accessListView.ValueChanged += (_, args) =>
    {
      accessLevel = args.NewValue == 1
          ? DirectoryAccessLevel.ReadOnly
          : DirectoryAccessLevel.ReadWrite;
    };

    dialog.Add(pathLabel, pathField, accessLabel, accessListView);

    var cancelButton = new Button { Text = "Cancel" };
    var addButton = new Button { Text = "Add", IsDefault = true };

    cancelButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      TguiApp.RequestStop();
    };

    addButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      if (!string.IsNullOrWhiteSpace(path))
      {
        directories.Add(new ProjectDirectory(path, accessLevel));
      }

      TguiApp.RequestStop();
    };

    dialog.AddButton(cancelButton);
    dialog.AddButton(addButton);

    TguiApp.Run(dialog);
    dialog.Dispose();
  }

  [GeneratedRegex(@"^[a-zA-Z0-9_-]+$")]
  private static partial Regex ProjectNameRegex();

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

    // TUI path: use the complex edit dialog
    if (SpectreUserInterface.Current?.Toplevel is not null)
    {
      await HandleEditTuiAsync(project, ct);
      return;
    }

    // Spectre fallback: inline edit loop
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

  private async Task HandleEditTuiAsync(Project project, CancellationToken ct)
  {
    using var dialog = new ProjectEditDialog(project);
    if (!dialog.ShowDialog())
    {
      SpectreHelpers.Cancelled();
      return;
    }

    dialog.ApplyChanges(project);
    await _projectRepository.SaveAsync(project, ct);
    RefreshSessionContext(project);

    if (dialog.ContainerSettingsChanged
        && string.Equals(project.Name, _activeProject.Name, StringComparison.OrdinalIgnoreCase))
    {
      _ui.StaleSettingsWarning = "Project settings changed. Run /context refresh to apply.";
    }

    SpectreHelpers.Success($"Project '{project.Name}' saved.");
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

    var detailBullets = new List<string>();

    if (project.Directories.Count > 0)
    {
      detailBullets.Add($"{project.Directories.Count} directory mapping(s)");
    }

    if (project.SystemPrompt is not null)
    {
      detailBullets.Add("Custom system prompt");
    }

    if (project.DockerImage is not null)
    {
      detailBullets.Add($"Docker image ({project.DockerImage})");
    }

    if (project.RequireContainer)
    {
      detailBullets.Add("Container execution required");
    }

    if (SpectreUserInterface.Current?.Toplevel is not null)
    {
      var messageParts = new List<string> { $"Delete project '{name}'?" };
      if (detailBullets.Count > 0)
      {
        messageParts.Add("");
        foreach (var bullet in detailBullets)
        {
          messageParts.Add($"  * {bullet}");
        }
      }

      var deleteResult = MessageBox.Query(TguiApp.Instance, "Delete Project", string.Join("\n", messageParts), "Cancel", "Delete");
      if (deleteResult != 1)
      {
        SpectreHelpers.Cancelled();
        return;
      }
    }
    else if (_ui.IsInteractive)
    {
      SpectreHelpers.OutputLine();
      SpectreHelpers.OutputMarkup($"  This will delete project [bold]{Markup.Escape(name)}[/]:");

      if (detailBullets.Count > 0)
      {
        foreach (var detail in detailBullets)
        {
          SpectreHelpers.OutputMarkup($"    [dim]-[/] {detail}");
        }
      }
      else
      {
        SpectreHelpers.OutputMarkup("    [dim]No custom configuration.[/]");
      }

      SpectreHelpers.OutputLine();

      if (!SpectreHelpers.Confirm($"Delete project [bold]{Markup.Escape(name)}[/]?", defaultValue: false))
      {
        SpectreHelpers.Cancelled();
        return;
      }
    }
    else
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
