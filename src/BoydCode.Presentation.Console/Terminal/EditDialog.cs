using System.Collections;
using System.Collections.Specialized;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TguiApp = Terminal.Gui.App.Application;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable CS0618 // Application.Run/RequestStop - using legacy static API during Terminal.Gui migration
#pragma warning disable CS0067 // CollectionChanged event is required by IListDataSource but never raised directly

namespace BoydCode.Presentation.Console.Terminal;

/// <summary>
/// Defines a sidebar item for an <see cref="EditDialog"/>.
/// </summary>
/// <param name="Label">Display label for the sidebar item.</param>
/// <param name="SummaryFunc">Returns a short summary string shown beside the label in muted text.</param>
internal sealed record EditDialogField(string Label, Func<string> SummaryFunc);

/// <summary>
/// Abstract base class for complex edit dialogs that follow Component Pattern #16
/// (Edit Menu Loop, Dialog Approach). Provides a modal <see cref="Dialog"/> with a
/// sidebar <see cref="ListView"/> on the left and a dynamic content area on the right.
/// Cancel and Done buttons control the dialog lifecycle.
/// </summary>
internal abstract class EditDialog : IDisposable
{
  private readonly string _title;
  private bool _confirmed;
  private bool _disposed;

  private Dialog _dialog = null!;
  private ListView _sidebar = null!;
  private View _contentArea = null!;

  /// <summary>
  /// Creates a new edit dialog.
  /// </summary>
  /// <param name="title">The dialog title shown in the border.</param>
  protected EditDialog(string title)
  {
    _title = title;
  }

  /// <summary>
  /// Builds the sidebar field definitions. Called at the start of <see cref="ShowDialog"/>
  /// so that closures can capture the subclass's mutable edit state.
  /// </summary>
  protected abstract IReadOnlyList<EditDialogField> BuildFields();

  /// <summary>
  /// Called when the sidebar selection changes. The subclass should populate
  /// the content area with the appropriate editor for the given field index.
  /// </summary>
  protected abstract void OnSidebarSelectionChanged(int index);

  /// <summary>
  /// Called when the Done button is pressed. Returns true if the dialog should
  /// close (all edits are valid), false to stay open.
  /// </summary>
  protected abstract bool OnDone();

  /// <summary>
  /// Removes all views from the content area.
  /// </summary>
  protected void ClearContentArea()
  {
    _contentArea.RemoveAll();
  }

  /// <summary>
  /// Adds a view to the content area.
  /// </summary>
  protected void ShowInContentArea(View view)
  {
    _contentArea.Add(view);
    _contentArea.SetNeedsDraw();
  }

  /// <summary>
  /// Gets the content area view for sizing child views.
  /// </summary>
  protected View ContentArea => _contentArea;

  /// <summary>
  /// Refreshes the sidebar summaries (e.g. after an edit changes a value).
  /// </summary>
  protected void RefreshSidebar()
  {
    _sidebar.SetNeedsDraw();
  }

  /// <summary>
  /// Shows the dialog modally. Returns true if Done was pressed and
  /// <see cref="OnDone"/> returned true; false if cancelled.
  /// </summary>
  internal bool ShowDialog()
  {
    _confirmed = false;
    var fields = BuildFields();

    _dialog = new Dialog
    {
      Title = _title,
      Width = Dim.Percent(80),
      Height = Dim.Percent(70),
      BorderStyle = LineStyle.Rounded,
    };
    _dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

    // Sidebar ListView at left
    var sidebarSource = new SidebarDataSource(fields);
    _sidebar = new ListView
    {
      X = 2,
      Y = 2,
      Width = 22,
      Height = Dim.Fill(3),
      CanFocus = true,
      Source = sidebarSource,
    };

    _sidebar.RowRender += OnSidebarRowRender;
    _sidebar.ValueChanged += OnSidebarValueChanged;

    // Vertical separator
    var separator = new SeparatorLineView
    {
      X = 25,
      Y = 2,
      Width = 1,
      Height = Dim.Fill(3),
    };

    // Right content area
    _contentArea = new View
    {
      X = 27,
      Y = 2,
      Width = Dim.Fill(2),
      Height = Dim.Fill(3),
    };

    _dialog.Add(_sidebar, separator, _contentArea);

    // Buttons
    var cancelButton = new Button { Text = "Cancel" };
    var doneButton = new Button { Text = "Done", IsDefault = true };

    _dialog.AddButton(cancelButton);
    _dialog.AddButton(doneButton);

    cancelButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      _confirmed = false;
      TguiApp.RequestStop();
    };

    doneButton.Accepting += (_, args) =>
    {
      args.Handled = true;
      if (OnDone())
      {
        _confirmed = true;
        TguiApp.RequestStop();
      }
    };

    // Initialize with the first field selected
    OnSidebarSelectionChanged(0);

    TguiApp.Run(_dialog);

    return _confirmed;
  }

  private void OnSidebarRowRender(object? sender, ListViewRowEventArgs e)
  {
    if (_sidebar.SelectedItem == e.Row)
    {
      e.RowAttribute = Theme.List.SelectedText;
    }
  }

  private void OnSidebarValueChanged(object? sender, ValueChangedEventArgs<int?> e)
  {
    if (e.NewValue is { } index)
    {
      OnSidebarSelectionChanged(index);
    }
  }

  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;
    _dialog?.Dispose();
  }

  // ─────────────────────────────────────────────────────────────
  //  SeparatorLineView — draws vertical | characters
  // ─────────────────────────────────────────────────────────────

  private sealed class SeparatorLineView : View
  {
    protected override bool OnDrawingContent(DrawContext? context)
    {
      var height = Viewport.Height;
      if (height <= 0) return true;

      SetAttribute(Theme.Semantic.Muted);
      for (var row = 0; row < height; row++)
      {
        Move(0, row);
        AddStr(Theme.Symbols.BoxVertical.ToString());
      }

      return true;
    }
  }

  // ─────────────────────────────────────────────────────────────
  //  SidebarDataSource — custom IListDataSource with arrow indicator
  // ─────────────────────────────────────────────────────────────

  private sealed class SidebarDataSource : IListDataSource, IDisposable
  {
    private readonly IReadOnlyList<EditDialogField> _fields;

    internal SidebarDataSource(IReadOnlyList<EditDialogField> fields)
    {
      _fields = fields;
    }

    public void Dispose()
    {
      // No unmanaged resources
    }

    public int Count => _fields.Count;

    public int MaxItemLength
    {
      get
      {
        var max = 0;
        foreach (var field in _fields)
        {
          // Arrow prefix "▸ " = 2 chars, label, "  ", summary
          var summary = field.SummaryFunc();
          var len = 2 + field.Label.Length + 2 + summary.Length;
          if (len > max) max = len;
        }

        return Math.Max(max, 1);
      }
    }

    public bool SuspendCollectionChangedEvent { get; set; }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public void Render(
      ListView listView,
      bool selected,
      int item,
      int col,
      int row,
      int width,
      int viewportX)
    {
      if (item < 0 || item >= _fields.Count || width <= 0)
      {
        listView.Move(col, row);
        listView.SetAttribute(Theme.Semantic.Default);
        listView.AddStr(new string(' ', width));
        return;
      }

      var field = _fields[item];
      var prefix = selected ? "\u25b8 " : "  ";
      var label = field.Label;
      var summary = field.SummaryFunc();

      listView.Move(col, row);

      if (selected)
      {
        listView.SetAttribute(Theme.List.SelectedText);
        var text = $"{prefix}{label}";
        if (text.Length < width)
        {
          text += new string(' ', width - text.Length);
        }
        else if (text.Length > width)
        {
          text = text[..width];
        }

        listView.AddStr(text);
      }
      else
      {
        // Label in default color, summary in muted
        listView.SetAttribute(Theme.Semantic.Default);
        listView.AddStr(prefix);
        listView.AddStr(label);

        var usedWidth = prefix.Length + label.Length;
        var remainingWidth = width - usedWidth;

        if (remainingWidth > 2 && summary.Length > 0)
        {
          listView.SetAttribute(Theme.Semantic.Muted);
          var summaryText = "  " + summary;
          if (summaryText.Length > remainingWidth)
          {
            summaryText = summaryText[..remainingWidth];
          }

          listView.AddStr(summaryText);
          usedWidth += summaryText.Length;
        }

        // Pad remaining
        if (usedWidth < width)
        {
          listView.SetAttribute(Theme.Semantic.Default);
          listView.AddStr(new string(' ', width - usedWidth));
        }
      }
    }

    public bool IsMarked(int item) => false;

    public void SetMark(int item, bool value)
    {
      // Marking not supported
    }

    public IList ToList() => _fields.Select(f => f.Label).ToList();

    public bool RenderMark(
      ListView listView,
      int item,
      int row,
      bool isMarked,
      bool markMultiple) => false;
  }
}
