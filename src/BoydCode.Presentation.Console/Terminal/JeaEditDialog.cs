using System.Collections.ObjectModel;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TguiApp = Terminal.Gui.App.Application;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable CS0618 // Application.Run/RequestStop - using legacy static API during Terminal.Gui migration

namespace BoydCode.Presentation.Console.Terminal;

/// <summary>
/// A complex edit dialog for modifying JEA profile settings. Shows a sidebar with
/// six fields (Name, Language mode, Add command, Remove command, Add module,
/// Remove module) and a dynamic content area for editing each field.
/// Nothing is persisted until the user presses Done.
/// </summary>
internal sealed class JeaEditDialog : EditDialog
{
  private static readonly Attribute BoldLabel =
    new(ColorName16.White, Color.None, TextStyle.Bold);

  private static readonly string[] LanguageModeNames =
  [
    nameof(PSLanguageModeName.FullLanguage),
    nameof(PSLanguageModeName.ConstrainedLanguage),
    nameof(PSLanguageModeName.RestrictedLanguage),
    nameof(PSLanguageModeName.NoLanguage),
  ];

  private readonly string _editName;
  private PSLanguageModeName _editLanguageMode;
  private readonly List<JeaProfileEntry> _editEntries;
  private readonly List<string> _editModules;

  internal JeaEditDialog(JeaProfile profile)
    : base($"Edit JEA Profile: {profile.Name}")
  {
    _editName = profile.Name;
    _editLanguageMode = profile.LanguageMode;
    _editEntries = new List<JeaProfileEntry>(profile.Entries);
    _editModules = new List<string>(profile.Modules);
  }

  /// <summary>
  /// Builds a new <see cref="JeaProfile"/> from the edited state. Call only after
  /// <see cref="EditDialog.ShowDialog"/> returns true.
  /// </summary>
  internal JeaProfile BuildProfile()
  {
    return new JeaProfile(_editName, _editLanguageMode, _editModules, _editEntries);
  }

  protected override IReadOnlyList<EditDialogField> BuildFields()
  {
    return
    [
      new EditDialogField("Name", () => _editName),
      new EditDialogField("Language mode", () => _editLanguageMode.ToString()),
      new EditDialogField("Add command", () =>
        _editEntries.Count > 0 ? $"{_editEntries.Count} cmd(s)" : "(none)"),
      new EditDialogField("Remove command", () =>
        _editEntries.Count > 0 ? $"{_editEntries.Count} cmd(s)" : "(none)"),
      new EditDialogField("Add module", () =>
        _editModules.Count > 0 ? $"{_editModules.Count} mod(s)" : "(none)"),
      new EditDialogField("Remove module", () =>
        _editModules.Count > 0 ? $"{_editModules.Count} mod(s)" : "(none)"),
    ];
  }

  protected override void OnSidebarSelectionChanged(int index)
  {
    ClearContentArea();

    switch (index)
    {
      case 0:
        ShowNameField();
        break;
      case 1:
        ShowLanguageModeField();
        break;
      case 2:
        ShowAddCommandField();
        break;
      case 3:
        ShowRemoveCommandField();
        break;
      case 4:
        ShowAddModuleField();
        break;
      case 5:
        ShowRemoveModuleField();
        break;
    }

    RefreshSidebar();
  }

  protected override bool OnDone()
  {
    return true;
  }

  // ─── Field Editors ──────────────────────────────────────────

  private void ShowNameField()
  {
    var nameLabel = new Label
    {
      Text = _editName,
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

  private void ShowLanguageModeField()
  {
    var fieldLabel = new Label
    {
      Text = "Select language mode:",
      X = 0,
      Y = 0,
    };
    fieldLabel.SetScheme(new Scheme(BoldLabel));

    var modeList = new ListView
    {
      X = 0,
      Y = 1,
      Width = 30,
      Height = 4,
      CanFocus = true,
    };
    modeList.SetSource(new ObservableCollection<string>(LanguageModeNames));

    // Pre-select the current mode
    var currentIndex = (int)_editLanguageMode;
    if (currentIndex >= 0 && currentIndex < LanguageModeNames.Length)
    {
      modeList.SelectedItem = currentIndex;
    }

    modeList.ValueChanged += (_, e) =>
    {
      if (e.NewValue is { } idx && idx >= 0 && idx < LanguageModeNames.Length)
      {
        _editLanguageMode = (PSLanguageModeName)idx;
        RefreshSidebar();
      }
    };

    ShowInContentArea(fieldLabel);
    ShowInContentArea(modeList);

    modeList.SetFocus();
  }

  private void ShowAddCommandField()
  {
    var commandLabel = new Label
    {
      Text = "Command name:",
      X = 0,
      Y = 0,
    };
    commandLabel.SetScheme(new Scheme(BoldLabel));

    var commandField = new TextField
    {
      X = 0,
      Y = 1,
      Width = Dim.Fill(),
      CanFocus = true,
    };

    var actionLabel = new Label
    {
      Text = "Action:",
      X = 0,
      Y = 3,
    };
    actionLabel.SetScheme(new Scheme(BoldLabel));

    var actionList = new ListView
    {
      X = 0,
      Y = 4,
      Width = 15,
      Height = 2,
    };
    actionList.SetSource(new ObservableCollection<string>(["Allow", "Deny"]));

    var addButton = new Button
    {
      Text = "Add",
      X = 0,
      Y = 7,
    };

    var statusLabel = new Label
    {
      Text = "",
      X = 0,
      Y = 8,
      Width = Dim.Fill(),
      Visible = false,
    };

    addButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      var name = commandField.Text?.Trim();
      if (string.IsNullOrWhiteSpace(name))
      {
        statusLabel.Text = "Command name is required.";
        statusLabel.SetScheme(new Scheme(Theme.Semantic.Error));
        statusLabel.Visible = true;
        return;
      }

      var isDenied = (actionList.SelectedItem ?? 0) == 1;
      _editEntries.Add(new JeaProfileEntry(name, isDenied));

      var marker = isDenied ? "Deny" : "Allow";
      statusLabel.Text = $"Added: {marker} {name}";
      statusLabel.SetScheme(new Scheme(Theme.Semantic.Success));
      statusLabel.Visible = true;
      commandField.Text = string.Empty;
      commandField.SetFocus();
      RefreshSidebar();
    };

    ShowInContentArea(commandLabel);
    ShowInContentArea(commandField);
    ShowInContentArea(actionLabel);
    ShowInContentArea(actionList);
    ShowInContentArea(addButton);
    ShowInContentArea(statusLabel);

    commandField.SetFocus();
  }

  private void ShowRemoveCommandField()
  {
    if (_editEntries.Count == 0)
    {
      var emptyLabel = new Label
      {
        Text = "(no commands configured)",
        X = 0,
        Y = 0,
      };
      emptyLabel.SetScheme(new Scheme(Theme.Semantic.Muted));
      ShowInContentArea(emptyLabel);
      return;
    }

    RebuildRemoveCommandView();
  }

  private void RebuildRemoveCommandView()
  {
    ClearContentArea();

    if (_editEntries.Count == 0)
    {
      var emptyLabel = new Label
      {
        Text = "(no commands configured)",
        X = 0,
        Y = 0,
      };
      emptyLabel.SetScheme(new Scheme(Theme.Semantic.Muted));
      ShowInContentArea(emptyLabel);
      RefreshSidebar();
      return;
    }

    var headerLabel = new Label
    {
      Text = "Commands:",
      X = 0,
      Y = 0,
    };
    headerLabel.SetScheme(new Scheme(BoldLabel));

    var items = _editEntries
      .Select(e =>
      {
        var marker = e.IsDenied ? "[Deny]" : "[Allow]";
        return $"{marker} {e.CommandName}";
      })
      .ToList();

    var commandList = new ListView
    {
      X = 0,
      Y = 1,
      Width = Dim.Fill(),
      Height = Math.Min(_editEntries.Count, 10),
      CanFocus = true,
    };
    commandList.SetSource(new ObservableCollection<string>(items));

    var removeButton = new Button
    {
      Text = "Remove",
      X = 0,
      Y = Math.Min(_editEntries.Count, 10) + 2,
    };

    removeButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      var index = commandList.SelectedItem ?? -1;
      if (index < 0 || index >= _editEntries.Count) return;

      _editEntries.RemoveAt(index);
      RebuildRemoveCommandView();
    };

    ShowInContentArea(headerLabel);
    ShowInContentArea(commandList);
    ShowInContentArea(removeButton);

    commandList.SetFocus();
  }

  private void ShowAddModuleField()
  {
    var moduleLabel = new Label
    {
      Text = "Module name:",
      X = 0,
      Y = 0,
    };
    moduleLabel.SetScheme(new Scheme(BoldLabel));

    var moduleField = new TextField
    {
      X = 0,
      Y = 1,
      Width = Dim.Fill(),
      CanFocus = true,
    };

    var addButton = new Button
    {
      Text = "Add",
      X = 0,
      Y = 3,
    };

    var statusLabel = new Label
    {
      Text = "",
      X = 0,
      Y = 4,
      Width = Dim.Fill(),
      Visible = false,
    };

    addButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      var name = moduleField.Text?.Trim();
      if (string.IsNullOrWhiteSpace(name))
      {
        statusLabel.Text = "Module name is required.";
        statusLabel.SetScheme(new Scheme(Theme.Semantic.Error));
        statusLabel.Visible = true;
        return;
      }

      _editModules.Add(name);
      statusLabel.Text = $"Added module: {name}";
      statusLabel.SetScheme(new Scheme(Theme.Semantic.Success));
      statusLabel.Visible = true;
      moduleField.Text = string.Empty;
      moduleField.SetFocus();
      RefreshSidebar();
    };

    ShowInContentArea(moduleLabel);
    ShowInContentArea(moduleField);
    ShowInContentArea(addButton);
    ShowInContentArea(statusLabel);

    moduleField.SetFocus();
  }

  private void ShowRemoveModuleField()
  {
    if (_editModules.Count == 0)
    {
      var emptyLabel = new Label
      {
        Text = "(no modules configured)",
        X = 0,
        Y = 0,
      };
      emptyLabel.SetScheme(new Scheme(Theme.Semantic.Muted));
      ShowInContentArea(emptyLabel);
      return;
    }

    RebuildRemoveModuleView();
  }

  private void RebuildRemoveModuleView()
  {
    ClearContentArea();

    if (_editModules.Count == 0)
    {
      var emptyLabel = new Label
      {
        Text = "(no modules configured)",
        X = 0,
        Y = 0,
      };
      emptyLabel.SetScheme(new Scheme(Theme.Semantic.Muted));
      ShowInContentArea(emptyLabel);
      RefreshSidebar();
      return;
    }

    var headerLabel = new Label
    {
      Text = "Modules:",
      X = 0,
      Y = 0,
    };
    headerLabel.SetScheme(new Scheme(BoldLabel));

    var moduleList = new ListView
    {
      X = 0,
      Y = 1,
      Width = Dim.Fill(),
      Height = Math.Min(_editModules.Count, 10),
      CanFocus = true,
    };
    moduleList.SetSource(new ObservableCollection<string>(_editModules));

    var removeButton = new Button
    {
      Text = "Remove",
      X = 0,
      Y = Math.Min(_editModules.Count, 10) + 2,
    };

    removeButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      var index = moduleList.SelectedItem ?? -1;
      if (index < 0 || index >= _editModules.Count) return;

      _editModules.RemoveAt(index);
      RebuildRemoveModuleView();
    };

    ShowInContentArea(headerLabel);
    ShowInContentArea(moduleList);
    ShowInContentArea(removeButton);

    moduleList.SetFocus();
  }
}
