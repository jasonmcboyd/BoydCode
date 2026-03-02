using System.Collections.ObjectModel;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TguiApp = Terminal.Gui.App.Application;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable CS0618 // Application.Run/RequestStop - using legacy static API during Terminal.Gui migration

namespace BoydCode.Presentation.Console.Terminal;

/// <summary>
/// A complex edit dialog for modifying project settings. Shows a sidebar with
/// five fields (Name, System prompt, Docker image, Require container, Directories)
/// and a dynamic content area for editing each field. Nothing is persisted until
/// the user presses Done.
/// </summary>
internal sealed class ProjectEditDialog : EditDialog
{
  private static readonly Attribute BoldLabel =
    new(ColorName16.White, Color.None, TextStyle.Bold);

  private readonly Project _project;

  // In-memory edit state (mutable copies)
  private string? _editSystemPrompt;
  private string? _editDockerImage;
  private bool _editRequireContainer;
  private readonly List<ProjectDirectory> _editDirectories;
  private bool _containerSettingsChanged;

  // Track active editors to capture values on field switch
  private int _lastSidebarIndex = -1;
  private TextView? _activeSystemPromptView;
  private TextField? _activeDockerImageField;

  internal ProjectEditDialog(Project project)
    : base($"Edit Project: {project.Name}")
  {
    _project = project;
    _editSystemPrompt = project.SystemPrompt;
    _editDockerImage = project.DockerImage;
    _editRequireContainer = project.RequireContainer;
    _editDirectories = new List<ProjectDirectory>(project.Directories);
  }

  /// <summary>
  /// Applies the edited state to the project object. Call only after
  /// <see cref="EditDialog.ShowDialog"/> returns true.
  /// </summary>
  internal void ApplyChanges(Project project)
  {
    project.SystemPrompt = _editSystemPrompt;
    project.DockerImage = _editDockerImage;
    project.RequireContainer = _editRequireContainer;
    project.Directories.Clear();
    project.Directories.AddRange(_editDirectories);
  }

  /// <summary>
  /// Returns true if container-related settings were changed.
  /// </summary>
  internal bool ContainerSettingsChanged => _containerSettingsChanged;

  protected override IReadOnlyList<EditDialogField> BuildFields()
  {
    return
    [
      new EditDialogField("Name", () => _project.Name),
      new EditDialogField("System prompt", () =>
        _editSystemPrompt is not null ? "custom" : "(default)"),
      new EditDialogField("Docker image", () =>
        _editDockerImage ?? "(none)"),
      new EditDialogField("Require container", () =>
        _editRequireContainer ? "Yes" : "No"),
      new EditDialogField("Directories", () =>
        _editDirectories.Count > 0
          ? $"{_editDirectories.Count} dir(s)"
          : "(none)"),
    ];
  }

  protected override void OnSidebarSelectionChanged(int index)
  {
    CaptureActiveFieldValues();
    _lastSidebarIndex = index;

    ClearContentArea();

    switch (index)
    {
      case 0:
        ShowNameField();
        break;
      case 1:
        ShowSystemPromptField();
        break;
      case 2:
        ShowDockerImageField();
        break;
      case 3:
        ShowRequireContainerField();
        break;
      case 4:
        ShowDirectoriesField();
        break;
    }

    RefreshSidebar();
  }

  protected override bool OnDone()
  {
    CaptureActiveFieldValues();

    if (_editDockerImage != _project.DockerImage
        || _editRequireContainer != _project.RequireContainer)
    {
      _containerSettingsChanged = true;
    }

    return true;
  }

  private void CaptureActiveFieldValues()
  {
    if (_lastSidebarIndex == 1 && _activeSystemPromptView is not null)
    {
      var text = _activeSystemPromptView.Text ?? string.Empty;
      _editSystemPrompt = text.Equals(Project.DefaultSystemPrompt, StringComparison.Ordinal)
        ? null
        : string.IsNullOrWhiteSpace(text)
          ? null
          : text;
      _activeSystemPromptView = null;
    }

    if (_lastSidebarIndex == 2 && _activeDockerImageField is not null)
    {
      var text = _activeDockerImageField.Text ?? string.Empty;
      _editDockerImage = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
      _activeDockerImageField = null;
    }
  }

  // ─── Field Editors ──────────────────────────────────────────

  private void ShowNameField()
  {
    var nameLabel = new Label
    {
      Text = _project.Name,
      X = 0,
      Y = 0,
      Width = Dim.Fill(),
    };
    nameLabel.SetScheme(new Scheme(BoldLabel));

    var hintLabel = new Label
    {
      Text = "(read-only)",
      X = 0,
      Y = 1,
    };
    hintLabel.SetScheme(new Scheme(Theme.Semantic.Muted));

    ShowInContentArea(nameLabel);
    ShowInContentArea(hintLabel);
  }

  private void ShowSystemPromptField()
  {
    var headerText = _editSystemPrompt is not null
      ? "Custom prompt:"
      : "Default prompt (edit to customize):";

    var currentLabel = new Label
    {
      Text = headerText,
      X = 0,
      Y = 0,
      Width = Dim.Fill(),
    };
    currentLabel.SetScheme(new Scheme(Theme.Semantic.Muted));

    var textView = new TextView
    {
      X = 0,
      Y = 1,
      Width = Dim.Fill(),
      Height = Dim.Fill(),
      WordWrap = true,
      Text = _editSystemPrompt ?? Project.DefaultSystemPrompt,
      CanFocus = true,
    };

    _activeSystemPromptView = textView;

    ShowInContentArea(currentLabel);
    ShowInContentArea(textView);

    textView.SetFocus();
  }

  private void ShowDockerImageField()
  {
    var fieldLabel = new Label
    {
      Text = "Docker image:",
      X = 0,
      Y = 0,
    };
    fieldLabel.SetScheme(new Scheme(BoldLabel));

    var textField = new TextField
    {
      X = 0,
      Y = 1,
      Width = Dim.Fill(),
      Text = _editDockerImage ?? string.Empty,
      CanFocus = true,
    };

    _activeDockerImageField = textField;

    var hintLabel = new Label
    {
      Text = "(leave empty to clear)",
      X = 0,
      Y = 2,
    };
    hintLabel.SetScheme(new Scheme(Theme.Semantic.Muted));

    ShowInContentArea(fieldLabel);
    ShowInContentArea(textField);
    ShowInContentArea(hintLabel);

    textField.SetFocus();
  }

  private void ShowRequireContainerField()
  {
    var currentLabel = new Label
    {
      Text = $"Current: {(_editRequireContainer ? "Yes" : "No")}",
      X = 0,
      Y = 0,
    };
    currentLabel.SetScheme(new Scheme(BoldLabel));

    var noButton = new Button
    {
      Text = "No",
      X = 0,
      Y = 2,
    };

    var yesButton = new Button
    {
      Text = "Yes",
      X = Pos.Right(noButton) + 2,
      Y = 2,
    };

    noButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      _editRequireContainer = false;
      currentLabel.Text = "Current: No";
      RefreshSidebar();
    };

    yesButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      _editRequireContainer = true;
      currentLabel.Text = "Current: Yes";
      RefreshSidebar();
    };

    ShowInContentArea(currentLabel);
    ShowInContentArea(noButton);
    ShowInContentArea(yesButton);
  }

  private void ShowDirectoriesField()
  {
    RebuildDirectoryView();
  }

  private void RebuildDirectoryView()
  {
    ClearContentArea();

    if (_editDirectories.Count > 0)
    {
      var headerLabel = new Label
      {
        Text = "Path                                     Access",
        X = 0,
        Y = 0,
        Width = Dim.Fill(),
      };
      headerLabel.SetScheme(new Scheme(Theme.Semantic.Muted));
      ShowInContentArea(headerLabel);

      for (var i = 0; i < _editDirectories.Count; i++)
      {
        var dir = _editDirectories[i];
        var accessLabel = dir.AccessLevel == DirectoryAccessLevel.ReadOnly ? "RO" : "RW";
        var displayPath = dir.Path.Length > 40
          ? string.Concat(dir.Path.AsSpan(0, 37), "...")
          : dir.Path;
        var text = $"{displayPath,-41} {accessLabel}";

        var dirLabel = new Label
        {
          Text = text,
          X = 0,
          Y = i + 1,
          Width = Dim.Fill(),
        };
        ShowInContentArea(dirLabel);
      }

      var buttonY = _editDirectories.Count + 2;

      var addButton = new Button
      {
        Text = "Add",
        X = 0,
        Y = buttonY,
      };

      var removeButton = new Button
      {
        Text = "Remove",
        X = Pos.Right(addButton) + 2,
        Y = buttonY,
      };

      var changeButton = new Button
      {
        Text = "Change",
        X = Pos.Right(removeButton) + 2,
        Y = buttonY,
      };

      addButton.Accepting += (_, args) =>
      {
        args.Handled = true;
        ShowAddDirectorySubDialog();
      };

      removeButton.Accepting += (_, args) =>
      {
        args.Handled = true;
        ShowRemoveDirectorySubDialog();
      };

      changeButton.Accepting += (_, args) =>
      {
        args.Handled = true;
        ShowChangeAccessSubDialog();
      };

      ShowInContentArea(addButton);
      ShowInContentArea(removeButton);
      ShowInContentArea(changeButton);
    }
    else
    {
      var emptyLabel = new Label
      {
        Text = "(no directories configured)",
        X = 0,
        Y = 0,
      };
      emptyLabel.SetScheme(new Scheme(Theme.Semantic.Muted));

      var addButton = new Button
      {
        Text = "Add",
        X = 0,
        Y = 2,
      };

      addButton.Accepting += (_, args) =>
      {
        args.Handled = true;
        ShowAddDirectorySubDialog();
      };

      ShowInContentArea(emptyLabel);
      ShowInContentArea(addButton);
    }

    RefreshSidebar();
  }

  // ─── Sub-Dialogs ────────────────────────────────────────────

  private void ShowAddDirectorySubDialog()
  {
    var dialog = new Dialog
    {
      Title = "Add Directory",
      Width = Dim.Percent(60),
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
    pathLabel.SetScheme(new Scheme(BoldLabel));

    var pathField = new TextField
    {
      X = 2,
      Y = 2,
      Width = Dim.Fill(2),
      CanFocus = true,
    };

    var accessLabel = new Label
    {
      Text = "Access level:",
      X = 2,
      Y = 4,
    };
    accessLabel.SetScheme(new Scheme(BoldLabel));

    var accessList = new ListView
    {
      X = 2,
      Y = 5,
      Width = 15,
      Height = 2,
    };
    accessList.SetSource(new ObservableCollection<string>(["ReadWrite", "ReadOnly"]));

    dialog.Add(pathLabel, pathField, accessLabel, accessList);

    var cancelButton = new Button { Text = "Cancel" };
    var addButton = new Button { Text = "Add", IsDefault = true };
    dialog.AddButton(cancelButton);
    dialog.AddButton(addButton);

    var added = false;

    cancelButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      TguiApp.RequestStop();
    };

    addButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      var path = pathField.Text?.Trim();
      if (string.IsNullOrWhiteSpace(path)) return;

      var selectedIndex = accessList.SelectedItem ?? 0;
      var accessLevel = selectedIndex == 1
        ? DirectoryAccessLevel.ReadOnly
        : DirectoryAccessLevel.ReadWrite;

      _editDirectories.Add(new ProjectDirectory(path, accessLevel));
      added = true;
      TguiApp.RequestStop();
    };

    TguiApp.Run(dialog);
    dialog.Dispose();

    if (added)
    {
      RebuildDirectoryView();
    }
  }

  private void ShowRemoveDirectorySubDialog()
  {
    if (_editDirectories.Count == 0) return;

    var paths = _editDirectories.Select(d => d.Path).ToList();
    var selected = SpectreHelpers.ShowSelectionDialog("Remove Directory", paths);
    if (selected is null) return;

    _editDirectories.RemoveAll(d => d.Path == selected);
    RebuildDirectoryView();
  }

  private void ShowChangeAccessSubDialog()
  {
    if (_editDirectories.Count == 0) return;

    var items = _editDirectories
      .Select(d =>
      {
        var label = d.AccessLevel == DirectoryAccessLevel.ReadOnly ? "RO" : "RW";
        return $"{d.Path} ({label})";
      })
      .ToList();

    var dirListHeight = Math.Min(_editDirectories.Count, 4);
    var dialogHeight = dirListHeight + 10;

    var dialog = new Dialog
    {
      Title = "Change Access Level",
      Width = Dim.Percent(60),
      Height = dialogHeight,
      BorderStyle = LineStyle.Rounded,
    };
    dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

    var dirLabel = new Label
    {
      Text = "Select directory:",
      X = 2,
      Y = 1,
    };
    dirLabel.SetScheme(new Scheme(BoldLabel));

    var dirList = new ListView
    {
      X = 2,
      Y = 2,
      Width = Dim.Fill(2),
      Height = dirListHeight,
      CanFocus = true,
    };
    dirList.SetSource(new ObservableCollection<string>(items));

    var accessLabel = new Label
    {
      Text = "New access level:",
      X = 2,
      Y = dirListHeight + 3,
    };
    accessLabel.SetScheme(new Scheme(BoldLabel));

    var accessList = new ListView
    {
      X = 2,
      Y = dirListHeight + 4,
      Width = 15,
      Height = 2,
    };
    accessList.SetSource(new ObservableCollection<string>(["ReadWrite", "ReadOnly"]));

    dialog.Add(dirLabel, dirList, accessLabel, accessList);

    var cancelButton = new Button { Text = "Cancel" };
    var applyButton = new Button { Text = "Apply", IsDefault = true };
    dialog.AddButton(cancelButton);
    dialog.AddButton(applyButton);

    var changed = false;

    cancelButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      TguiApp.RequestStop();
    };

    applyButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      var dirIndex = dirList.SelectedItem ?? -1;
      if (dirIndex < 0 || dirIndex >= _editDirectories.Count) return;

      var accessIndex = accessList.SelectedItem ?? 0;
      var newLevel = accessIndex == 1
        ? DirectoryAccessLevel.ReadOnly
        : DirectoryAccessLevel.ReadWrite;

      var existing = _editDirectories[dirIndex];
      _editDirectories[dirIndex] = new ProjectDirectory(existing.Path, newLevel);
      changed = true;
      TguiApp.RequestStop();
    };

    TguiApp.Run(dialog);
    dialog.Dispose();

    if (changed)
    {
      RebuildDirectoryView();
    }
  }
}
