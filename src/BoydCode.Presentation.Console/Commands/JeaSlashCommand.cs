using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
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

    var profiles = await BuildProfileListAsync(ct);

    var spectreUi = _ui as SpectreUserInterface;
    if (spectreUi?.Toplevel is not null)
    {
      ShowJeaListWindow(spectreUi, profiles, ct);
      return;
    }

    // Fallback: inline text output for non-interactive mode
    if (profiles.Count == 0)
    {
      SpectreHelpers.OutputMarkup("No JEA profiles found.");
      SpectreHelpers.Dim("Create one with /jea create <name>");
      return;
    }

    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup($"{"Name",-22}{"Language Mode",-22}{"Commands",8}  {"Modules",7}");
    SpectreHelpers.OutputMarkup(new string('\u2500', 65));

    foreach (var profile in profiles)
    {
      var isGlobal = profile.Name.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase);
      var displayName = isGlobal ? $"{profile.Name} (global)" : profile.Name;

      SpectreHelpers.OutputMarkup(
        $"{Markup.Escape(displayName),-22}" +
        $"{profile.LanguageMode,-22}" +
        $"{profile.Entries.Count.ToString(CultureInfo.InvariantCulture),8}  " +
        $"{profile.Modules.Count.ToString(CultureInfo.InvariantCulture),7}");
    }

    SpectreHelpers.OutputLine();
  }

  private async Task<List<JeaProfile>> BuildProfileListAsync(CancellationToken ct)
  {
    var names = await _store.ListNamesAsync(ct);
    var profiles = new List<JeaProfile>();

    foreach (var name in names)
    {
      var profile = await _store.LoadAsync(name, ct);
      if (profile is not null)
      {
        profiles.Add(profile);
      }
    }

    return profiles;
  }

  private void ShowJeaListWindow(
    SpectreUserInterface spectreUi,
    List<JeaProfile> profiles,
    CancellationToken ct)
  {
    InteractiveListWindow<JeaProfile>? window = null;

    var actions = new List<ActionDefinition<JeaProfile>>
    {
      new(
        Key.Enter, "Show",
        item =>
        {
          if (item is null) return;
          var filePath = GetProfileFilePath(item.Name);
          var content = BuildProfileDetailText(item, filePath);
          _ui.ShowModal(item.Name, content);
        },
        IsPrimary: true),
      new(
        Key.E, "Edit",
        item =>
        {
          if (item is null) return;
          DismissJeaListWindow(spectreUi, ref window);
          _ = HandleEditAsync(["/jea", "edit", item.Name], ct);
        },
        HotkeyDisplay: "e"),
      new(
        Key.D, "Delete",
        item =>
        {
          if (item is null) return;
          DismissJeaListWindow(spectreUi, ref window);
          _ = HandleDeleteAsync(["/jea", "delete", item.Name], ct);
        },
        HotkeyDisplay: "d"),
      new(
        Key.N, "New",
        item =>
        {
          DismissJeaListWindow(spectreUi, ref window);
          _ = HandleCreateAsync(["/jea", "create"], ct);
        },
        HotkeyDisplay: "n",
        RequiresSelection: false),
    };

    window = new InteractiveListWindow<JeaProfile>(
      "JEA Profiles",
      profiles,
      FormatJeaProfile,
      actions,
      columnHeader: $"{"Name",-22}{"Language Mode",-22}{"Commands",8}  {"Modules",7}",
      emptyMessage: "No JEA profiles found.",
      emptyHint: "Use /jea create to add one.");

    window.CloseRequested += () => DismissJeaListWindow(spectreUi, ref window);

    TguiApp.Invoke(() =>
    {
      spectreUi.Toplevel!.Add(window);
      window.SetFocus();
    });
  }

  private static void DismissJeaListWindow(
    SpectreUserInterface spectreUi,
    ref InteractiveListWindow<JeaProfile>? window)
  {
    if (window is null) return;
    var w = window;
    window = null;

    TguiApp.Invoke(() =>
    {
      spectreUi.Toplevel?.Remove(w);
      w.Dispose();
      spectreUi.Toplevel?.InputView.SetFocus();
    });
  }

  private static string FormatJeaProfile(JeaProfile profile, int rowWidth)
  {
    var isGlobal = profile.Name.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase);
    var displayName = isGlobal ? $"{profile.Name} (global)" : profile.Name;

    return $"{displayName,-22}" +
        $"{profile.LanguageMode,-22}" +
        $"{profile.Entries.Count.ToString(CultureInfo.InvariantCulture),8}  " +
        $"{profile.Modules.Count.ToString(CultureInfo.InvariantCulture),7}";
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
    if (SpectreUserInterface.Current?.Toplevel is not null)
    {
      await RunJeaCreateWizard(tokens, ct);
    }
    else
    {
      await RunJeaCreateSpectre(tokens, ct);
    }
  }

  private async Task RunJeaCreateSpectre(string[] tokens, CancellationToken ct)
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

  private async Task RunJeaCreateWizard(string[] tokens, CancellationToken ct)
  {
    var existingProfileNames = (await _store.ListNamesAsync(ct))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var profileName = tokens.Length > 2 ? string.Join(' ', tokens.Skip(2)) : string.Empty;
    var languageMode = PSLanguageModeName.FullLanguage;
    var entries = new List<JeaProfileEntry>();
    var modules = new List<string>();
    Label? nameError = null;

    var steps = new List<WizardStep>
    {
      // Step 1: Name + Language Mode
      new WizardStep(
        "Name & Language Mode",
        () =>
        {
          var container = new View
          {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
          };

          var nameLabel = new Label
          {
            Text = "Profile name:",
            X = 0,
            Y = 0,
          };

          var nameField = new TextField
          {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Text = profileName,
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
            profileName = nameField.Text ?? string.Empty;
            if (nameError is not null)
            {
              nameError.Visible = false;
            }
          };

          var langLabel = new Label
          {
            Text = "Language mode:",
            X = 0,
            Y = 4,
          };

          var langValues = Enum.GetValues<PSLanguageModeName>();
          var langNames = langValues.Select(v => v.ToString()).ToList();

          var langListView = new ListView
          {
            X = 0,
            Y = 5,
            Width = Dim.Fill(),
            Height = 4,
          };
          langListView.SetSource(new ObservableCollection<string>(langNames));
          langListView.SelectedItem = Array.IndexOf(langValues, languageMode);

          langListView.ValueChanged += (_, args) =>
          {
            var index = args.NewValue ?? -1;
            if (index >= 0 && index < langValues.Length)
            {
              languageMode = langValues[index];
            }
          };

          container.Add(nameLabel, nameField, nameError, langLabel, langListView);
          return container;
        },
        () =>
        {
          // Validation
          if (string.IsNullOrWhiteSpace(profileName))
          {
            if (nameError is not null)
            {
              nameError.Text = "Profile name cannot be empty.";
              nameError.Visible = true;
            }

            return false;
          }

          if (!ProfileNameRegex().IsMatch(profileName))
          {
            if (nameError is not null)
            {
              nameError.Text = "Name must contain only letters, numbers, hyphens, and underscores.";
              nameError.Visible = true;
            }

            return false;
          }

          if (profileName.Equals(BuiltInJeaProfile.GlobalName, StringComparison.OrdinalIgnoreCase)
              || profileName.Equals(BuiltInJeaProfile.Name, StringComparison.OrdinalIgnoreCase))
          {
            if (nameError is not null)
            {
              nameError.Text = $"'{profileName}' is a reserved profile name.";
              nameError.Visible = true;
            }

            return false;
          }

          if (existingProfileNames.Contains(profileName))
          {
            if (nameError is not null)
            {
              nameError.Text = $"Profile '{profileName}' already exists.";
              nameError.Visible = true;
            }

            return false;
          }

          return true;
        }),

      // Step 2: Commands + Modules
      new WizardStep(
        "Commands & Modules",
        () =>
        {
          var container = new View
          {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
          };

          var commandLabel = new Label
          {
            Text = "Commands:",
            X = 0,
            Y = 0,
          };

          var commandListView = new View
          {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Auto(DimAutoStyle.Content, minimumContentDim: 1),
          };

          RebuildCommandList(commandListView, entries);

          var addCommandButton = new Button
          {
            Text = "Add Command",
            X = 0,
            Y = Pos.Bottom(commandListView) + 1,
          };

          addCommandButton.Accepting += (_, args) =>
          {
            args.Handled = true;
            ShowAddCommandDialog(entries);
            RebuildCommandList(commandListView, entries);
          };

          var moduleLabel = new Label
          {
            Text = "Modules:",
            X = 0,
            Y = Pos.Bottom(addCommandButton) + 1,
          };

          var moduleListView = new View
          {
            X = 0,
            Y = Pos.Bottom(moduleLabel),
            Width = Dim.Fill(),
            Height = Dim.Auto(DimAutoStyle.Content, minimumContentDim: 1),
          };

          RebuildModuleList(moduleListView, modules);

          var addModuleButton = new Button
          {
            Text = "Add Module",
            X = 0,
            Y = Pos.Bottom(moduleListView) + 1,
          };

          addModuleButton.Accepting += (_, args) =>
          {
            args.Handled = true;
            ShowAddModuleDialog(modules);
            RebuildModuleList(moduleListView, modules);
          };

          container.Add(commandLabel, commandListView, addCommandButton, moduleLabel, moduleListView, addModuleButton);
          return container;
        }),

      // Step 3: Review
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
            Text = $"Name:          {profileName}",
            X = 0,
            Y = y++,
          };

          var langLabel = new Label
          {
            Text = $"Language mode: {languageMode}",
            X = 0,
            Y = y++,
          };

          var allowCount = entries.Count(e => !e.IsDenied);
          var denyCount = entries.Count(e => e.IsDenied);
          var cmdLabel = new Label
          {
            Text = $"Commands:      {entries.Count} ({allowCount} allow, {denyCount} deny)",
            X = 0,
            Y = y++,
          };

          var modLabel = new Label
          {
            Text = $"Modules:       {modules.Count}",
            X = 0,
            Y = y++,
          };

          container.Add(nameLabel, langLabel, cmdLabel, modLabel);

          if (entries.Count > 0)
          {
            y++;
            var cmdHeader = new Label
            {
              Text = "Commands:",
              X = 0,
              Y = y++,
            };
            cmdHeader.SetScheme(new Scheme(Theme.Semantic.Muted));
            container.Add(cmdHeader);

            foreach (var entry in entries)
            {
              var marker = entry.IsDenied ? "x" : "\u2713";
              var entryLabel = new Label
              {
                Text = $"  {marker} {entry.CommandName}",
                X = 0,
                Y = y++,
              };
              container.Add(entryLabel);
            }
          }

          if (modules.Count > 0)
          {
            y++;
            var modHeader = new Label
            {
              Text = "Modules:",
              X = 0,
              Y = y++,
            };
            modHeader.SetScheme(new Scheme(Theme.Semantic.Muted));
            container.Add(modHeader);

            foreach (var module in modules)
            {
              var modEntryLabel = new Label
              {
                Text = $"  {module}",
                X = 0,
                Y = y++,
              };
              container.Add(modEntryLabel);
            }
          }

          return container;
        }),
    };

    using var wizard = new WizardDialog(
      "Create JEA Profile",
      steps,
      doneButtonText: "Create",
      hasUnsavedData: () =>
        !string.IsNullOrWhiteSpace(profileName) ||
        entries.Count > 0 ||
        modules.Count > 0);

    var result = wizard.Show();

    if (!result.Completed)
    {
      SpectreHelpers.Cancelled();
      return;
    }

    var profile = new JeaProfile(profileName, languageMode, modules, entries);
    await _store.SaveAsync(profile, ct);

    var filePath = GetProfileFilePath(profileName);
    SpectreHelpers.Success($"Profile '{profileName}' created.");
    SpectreHelpers.Dim($"File: {filePath}");
  }

  private static void RebuildCommandList(View commandListView, List<JeaProfileEntry> entries)
  {
    commandListView.RemoveAll();

    if (entries.Count == 0)
    {
      var emptyLabel = new Label
      {
        Text = "(no commands added)",
        X = 0,
        Y = 0,
      };
      emptyLabel.SetScheme(new Scheme(Theme.Semantic.Muted));
      commandListView.Add(emptyLabel);
    }
    else
    {
      for (var i = 0; i < entries.Count; i++)
      {
        var entry = entries[i];
        var marker = entry.IsDenied ? "Deny " : "Allow";
        var cmdLabel = new Label
        {
          Text = $"  {marker}  {entry.CommandName}",
          X = 0,
          Y = i,
        };
        commandListView.Add(cmdLabel);
      }
    }

    commandListView.SetNeedsDraw();
  }

  private static void RebuildModuleList(View moduleListView, List<string> modules)
  {
    moduleListView.RemoveAll();

    if (modules.Count == 0)
    {
      var emptyLabel = new Label
      {
        Text = "(no modules added)",
        X = 0,
        Y = 0,
      };
      emptyLabel.SetScheme(new Scheme(Theme.Semantic.Muted));
      moduleListView.Add(emptyLabel);
    }
    else
    {
      for (var i = 0; i < modules.Count; i++)
      {
        var modLabel = new Label
        {
          Text = $"  {modules[i]}",
          X = 0,
          Y = i,
        };
        moduleListView.Add(modLabel);
      }
    }

    moduleListView.SetNeedsDraw();
  }

  private static void ShowAddCommandDialog(List<JeaProfileEntry> entries)
  {
    var commandName = string.Empty;
    var isDenied = false;

    var dialog = new Dialog
    {
      Title = "Add Command",
      Width = Dim.Percent(50),
      Height = 11,
      BorderStyle = LineStyle.Rounded,
    };
    dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

    var nameLabel = new Label
    {
      Text = "Command name:",
      X = 2,
      Y = 1,
    };

    var nameField = new TextField
    {
      X = 2,
      Y = 2,
      Width = Dim.Fill(2),
    };

    nameField.TextChanged += (_, _) =>
    {
      commandName = nameField.Text ?? string.Empty;
    };

    var actionLabel = new Label
    {
      Text = "Action:",
      X = 2,
      Y = 4,
    };

    var actionOptions = new[] { "Allow", "Deny" };
    var actionListView = new ListView
    {
      X = 2,
      Y = 5,
      Width = Dim.Fill(2),
      Height = 2,
    };
    actionListView.SetSource(new ObservableCollection<string>(actionOptions));
    actionListView.SelectedItem = 0;

    actionListView.ValueChanged += (_, args) =>
    {
      isDenied = args.NewValue == 1;
    };

    dialog.Add(nameLabel, nameField, actionLabel, actionListView);

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
      if (!string.IsNullOrWhiteSpace(commandName))
      {
        entries.Add(new JeaProfileEntry(commandName, isDenied));
      }

      TguiApp.RequestStop();
    };

    dialog.AddButton(cancelButton);
    dialog.AddButton(addButton);

    TguiApp.Run(dialog);
    dialog.Dispose();
  }

  private static void ShowAddModuleDialog(List<string> modules)
  {
    var moduleName = string.Empty;

    var dialog = new Dialog
    {
      Title = "Add Module",
      Width = Dim.Percent(50),
      Height = 8,
      BorderStyle = LineStyle.Rounded,
    };
    dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

    var nameLabel = new Label
    {
      Text = "Module name:",
      X = 2,
      Y = 1,
    };

    var nameField = new TextField
    {
      X = 2,
      Y = 2,
      Width = Dim.Fill(2),
    };

    nameField.TextChanged += (_, _) =>
    {
      moduleName = nameField.Text ?? string.Empty;
    };

    dialog.Add(nameLabel, nameField);

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
      if (!string.IsNullOrWhiteSpace(moduleName))
      {
        modules.Add(moduleName);
      }

      TguiApp.RequestStop();
    };

    dialog.AddButton(cancelButton);
    dialog.AddButton(addButton);

    TguiApp.Run(dialog);
    dialog.Dispose();
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

    // TUI path: use the complex edit dialog
    if (SpectreUserInterface.Current?.Toplevel is not null)
    {
      await HandleEditTuiAsync(profile, ct);
      return;
    }

    // Spectre fallback: inline edit loop
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

  private async Task HandleEditTuiAsync(JeaProfile profile, CancellationToken ct)
  {
    using var dialog = new JeaEditDialog(profile);
    if (!dialog.ShowDialog())
    {
      SpectreHelpers.Cancelled();
      return;
    }

    var updatedProfile = dialog.BuildProfile();
    await _store.SaveAsync(updatedProfile, ct);

    var filePath = GetProfileFilePath(updatedProfile.Name);
    SpectreHelpers.Success($"Profile '{updatedProfile.Name}' saved.");
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

      if (SpectreUserInterface.Current?.Toplevel is not null)
      {
        var selected = SpectreHelpers.ShowSelectionDialog("Select Profile to Delete", deletable);
        if (selected is null)
        {
          SpectreHelpers.Cancelled();
          return;
        }

        name = selected;
      }
      else
      {
        name = SpectreHelpers.Select("Select profile to delete:", deletable);
      }
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

    if (SpectreUserInterface.Current?.Toplevel is not null)
    {
      var deleteResult = MessageBox.Query(TguiApp.Instance, "Delete JEA Profile", $"Delete profile '{name}'?", "Cancel", "Delete");
      if (deleteResult != 1)
      {
        SpectreHelpers.Cancelled();
        return;
      }
    }
    else if (!SpectreHelpers.Confirm($"Delete profile [bold]{Markup.Escape(name)}[/]?", defaultValue: false))
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

      if (SpectreUserInterface.Current?.Toplevel is not null)
      {
        var selected = SpectreHelpers.ShowSelectionDialog("Select Profile to Assign", assignable);
        if (selected is null)
        {
          SpectreHelpers.Cancelled();
          return;
        }

        name = selected;
      }
      else
      {
        name = SpectreHelpers.Select("Select profile to assign:", assignable);
      }
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
      if (SpectreUserInterface.Current?.Toplevel is not null)
      {
        var selected = SpectreHelpers.ShowSelectionDialog("Select Profile to Unassign", assigned);
        if (selected is null)
        {
          SpectreHelpers.Cancelled();
          return;
        }

        name = selected;
      }
      else
      {
        name = SpectreHelpers.Select("Select profile to unassign:", assigned);
      }
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

    if (SpectreUserInterface.Current?.Toplevel is not null)
    {
      return SpectreHelpers.ShowSelectionDialog("Select Profile", names);
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
