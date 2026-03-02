using System.Collections;
using System.Collections.Specialized;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable IDE0060 // Remove unused parameter - context is required by the override signature
#pragma warning disable CS0067  // CollectionChanged event is required by IListDataSource but never raised directly

namespace BoydCode.Presentation.Console.Terminal;

/// <summary>
/// Defines a keyboard-triggered action available in an <see cref="InteractiveListWindow{T}"/>.
/// Actions are displayed in the <see cref="ActionBarView"/> and dispatched via hotkey.
/// </summary>
/// <param name="Hotkey">The key that triggers this action (e.g. <c>Key.E</c>).</param>
/// <param name="Label">Human-readable label shown in the action bar (e.g. "Edit").</param>
/// <param name="Action">Callback invoked with the currently selected item.</param>
/// <param name="HotkeyDisplay">
/// Display text for the key hint (e.g. "e", "Enter"). If null, derived from <paramref name="Hotkey"/>.
/// </param>
/// <param name="IsPrimary">
/// When true, this action is triggered by Enter. Primary actions are always shown in the action bar.
/// </param>
/// <param name="RequiresSelection">
/// When true, this action is only available when the list has items.
/// When false, the action is always available (e.g. "New").
/// </param>
internal sealed record ActionDefinition<T>(
  Key Hotkey,
  string Label,
  Action<T?> Action,
  string? HotkeyDisplay = null,
  bool IsPrimary = false,
  bool RequiresSelection = true);

/// <summary>
/// A modeless window containing a navigable <see cref="ListView"/> with an <see cref="ActionBarView"/>
/// at the bottom. Implements pattern #28 (Interactive List) from the UX component spec.
/// </summary>
/// <typeparam name="T">The type of items displayed in the list.</typeparam>
internal sealed class InteractiveListWindow<T> : Window
{
  private readonly List<T> _allItems;
  private readonly List<T> _filteredItems;
  private readonly Func<T, int, string> _formatter;
  private readonly IReadOnlyList<ActionDefinition<T>> _actions;
  private readonly string? _columnHeader;

  private readonly ListView _listView;
  private readonly ActionBarView _actionBar;
  private readonly Label _headerLabel;
  private readonly EmptyStateView _emptyStateView;

  /// <summary>
  /// Raised when the window should be dismissed (Esc pressed or action requests close).
  /// </summary>
  internal event Action? CloseRequested;

  /// <summary>
  /// Gets the currently selected item, or default if the list is empty.
  /// </summary>
  internal T? SelectedItem
  {
    get
    {
      var index = _listView.SelectedItem ?? -1;
      return _filteredItems.Count > 0 && index >= 0 && index < _filteredItems.Count
        ? _filteredItems[index]
        : default;
    }
  }

  /// <summary>
  /// Creates a new interactive list window.
  /// </summary>
  /// <param name="title">Window title shown in the border.</param>
  /// <param name="items">The items to display.</param>
  /// <param name="formatter">
  /// Formats an item for display. Receives the item and the available row width.
  /// Should return a string (will be padded/truncated to fill the row).
  /// </param>
  /// <param name="actions">Action definitions for the action bar and keyboard dispatch.</param>
  /// <param name="columnHeader">Optional column header text displayed above the list.</param>
  /// <param name="emptyMessage">Message shown when the list has no items.</param>
  /// <param name="emptyHint">Secondary hint shown below the empty message.</param>
  internal InteractiveListWindow(
    string title,
    IEnumerable<T> items,
    Func<T, int, string> formatter,
    IReadOnlyList<ActionDefinition<T>> actions,
    string? columnHeader = null,
    string emptyMessage = "No items found.",
    string? emptyHint = null)
  {
    _allItems = new List<T>(items);
    _filteredItems = new List<T>(_allItems);
    _formatter = formatter;
    _actions = actions;
    _columnHeader = columnHeader;

    Title = title;
    X = Pos.Center();
    Y = Pos.Center();
    Width = Dim.Percent(80);
    Height = Dim.Percent(70);
    BorderStyle = LineStyle.Rounded;
    Border?.SetScheme(Theme.Modal.BorderScheme);

    var hasHeader = _columnHeader is not null;
    var listY = hasHeader ? 1 : 0;

    // Column header (bold muted label above the list)
    _headerLabel = new Label
    {
      X = 1,
      Y = 0,
      Width = Dim.Fill(1),
      Height = 1,
      Visible = hasHeader && _filteredItems.Count > 0,
    };

    if (hasHeader)
    {
      _headerLabel.Text = _columnHeader!;
    }

    // ListView with custom data source for arrow indicators and selection colors
    _listView = new ListView
    {
      X = 1,
      Y = listY,
      Width = Dim.Fill(1),
      Height = Dim.Fill(2),
      CanFocus = true,
    };

    _listView.Source = new InteractiveListDataSource<T>(_filteredItems, _formatter);
    _listView.RowRender += OnRowRender;

    // Empty state (centered message when list is empty)
    _emptyStateView = new EmptyStateView(emptyMessage, emptyHint)
    {
      X = 1,
      Y = listY,
      Width = Dim.Fill(1),
      Height = Dim.Fill(2),
      Visible = _filteredItems.Count == 0,
    };

    // Action bar at bottom of window
    _actionBar = new ActionBarView(
      BuildActionBarHints(_actions),
      () => _filteredItems.Count > 0)
    {
      X = 1,
      Y = Pos.AnchorEnd(2),
      Width = Dim.Fill(1),
      Height = 1,
    };

    Add(_headerLabel, _listView, _emptyStateView, _actionBar);
    UpdateVisibility();

    if (_filteredItems.Count > 0)
    {
      _listView.SetFocus();
    }
  }

  /// <summary>
  /// Replaces the items in the list (e.g. after a mutation like delete or rename).
  /// </summary>
  internal void UpdateItems(IEnumerable<T> newItems)
  {
    _allItems.Clear();
    _allItems.AddRange(newItems);
    _filteredItems.Clear();
    _filteredItems.AddRange(_allItems);
    RefreshListSource();
    UpdateVisibility();
    SetNeedsDraw();
  }

  protected override bool OnKeyDown(Key key)
  {
    // Esc always closes
    if (key == Key.Esc)
    {
      CloseRequested?.Invoke();
      return true;
    }

    // Vim-style j/k navigation
    if (KeyIsChar(key, 'j'))
    {
      if (_filteredItems.Count > 0)
      {
        _listView.MoveDown();
      }

      return true;
    }

    if (KeyIsChar(key, 'k'))
    {
      if (_filteredItems.Count > 0)
      {
        _listView.MoveUp();
      }

      return true;
    }

    // Enter triggers primary action
    if (key == Key.Enter)
    {
      if (_filteredItems.Count > 0)
      {
        var primary = FindPrimaryAction();
        primary?.Action(SelectedItem);
      }

      return true;
    }

    // Match single-letter hotkeys for secondary actions
    foreach (var action in _actions)
    {
      if (action.IsPrimary)
      {
        continue;
      }

      if (key == action.Hotkey)
      {
        if (action.RequiresSelection && _filteredItems.Count == 0)
        {
          continue;
        }

        action.Action(SelectedItem);
        return true;
      }
    }

    return base.OnKeyDown(key);
  }

  private void OnRowRender(object? sender, ListViewRowEventArgs e)
  {
    if (_listView.SelectedItem == e.Row)
    {
      e.RowAttribute = Theme.List.SelectedText;
    }
  }

  private ActionDefinition<T>? FindPrimaryAction()
  {
    foreach (var action in _actions)
    {
      if (action.IsPrimary)
      {
        return action;
      }
    }

    return null;
  }

  private void RefreshListSource()
  {
    _listView.Source = new InteractiveListDataSource<T>(_filteredItems, _formatter);
  }

  private void UpdateVisibility()
  {
    var hasItems = _filteredItems.Count > 0;
    _listView.Visible = hasItems;
    _emptyStateView.Visible = !hasItems;
    _headerLabel.Visible = _columnHeader is not null && hasItems;
    _actionBar.SetNeedsDraw();
  }

  private static bool KeyIsChar(Key key, char c)
  {
    var rune = key.AsRune;
    return rune.Value == c && !key.IsCtrl && !key.IsAlt;
  }

  private static List<ActionBarHint> BuildActionBarHints(
    IReadOnlyList<ActionDefinition<T>> actions)
  {
    var hints = new List<ActionBarHint>();

    // Primary action first (Enter: Label)
    foreach (var action in actions)
    {
      if (action.IsPrimary)
      {
        hints.Add(new ActionBarHint(
          "Enter", action.Label,
          IsPrimary: true,
          RequiresSelection: action.RequiresSelection));
      }
    }

    // Secondary actions in definition order
    foreach (var action in actions)
    {
      if (!action.IsPrimary)
      {
        var display = action.HotkeyDisplay ?? KeyToDisplayString(action.Hotkey);
        hints.Add(new ActionBarHint(
          display, action.Label,
          IsPrimary: false,
          RequiresSelection: action.RequiresSelection));
      }
    }

    // Esc: Close is always last and always shown
    hints.Add(new ActionBarHint("Esc", "Close", IsPrimary: false, RequiresSelection: false));

    return hints;
  }

  private static string KeyToDisplayString(Key key)
  {
    var rune = key.AsRune;

    if (rune.Value != 0 && !char.IsControl((char)rune.Value))
    {
      return ((char)rune.Value).ToString();
    }

    return key.ToString();
  }
}

// ─────────────────────────────────────────────────────────────
//  ActionBarHint — display-only hint record used by ActionBarView
// ─────────────────────────────────────────────────────────────

/// <summary>
/// A single keyboard hint displayed in the <see cref="ActionBarView"/>.
/// </summary>
/// <param name="KeyDisplay">Display text for the key (e.g. "Enter", "e", "Esc").</param>
/// <param name="Label">Action label (e.g. "Open", "Edit", "Close").</param>
/// <param name="IsPrimary">Primary hints are never dropped during responsive truncation.</param>
/// <param name="RequiresSelection">When true, hidden if the list is empty.</param>
internal sealed record ActionBarHint(
  string KeyDisplay,
  string Label,
  bool IsPrimary,
  bool RequiresSelection);

// ─────────────────────────────────────────────────────────────
//  ActionBarView — responsive hint bar at the bottom of a window
// ─────────────────────────────────────────────────────────────

/// <summary>
/// A horizontal bar of keyboard shortcut hints displayed at the bottom of a window.
/// Implements pattern #29 (Action Bar) from the UX component spec.
/// Responsively truncates hints right-to-left when the terminal is too narrow.
/// </summary>
internal sealed class ActionBarView : View
{
  private readonly IReadOnlyList<ActionBarHint> _hints;
  private readonly Func<bool> _hasItems;

  private const string Separator = "  ";

  /// <summary>
  /// Creates an action bar with pre-built hint entries.
  /// </summary>
  /// <param name="hints">Ordered list of hints (primary first, Esc: Close last).</param>
  /// <param name="hasItems">Returns true when the parent list has items.</param>
  internal ActionBarView(IReadOnlyList<ActionBarHint> hints, Func<bool> hasItems)
  {
    _hints = hints;
    _hasItems = hasItems;
  }

  protected override bool OnDrawingContent(DrawContext? context)
  {
    var width = Viewport.Width;

    if (width <= 0)
    {
      return true;
    }

    // Clear the row
    Move(0, 0);
    SetAttribute(Theme.Semantic.Muted);
    AddStr(new string(' ', width));

    var hasSelection = _hasItems();

    // Collect visible hints (filter out selection-dependent hints when list is empty)
    var visible = new List<ActionBarHint>();

    foreach (var hint in _hints)
    {
      if (hint.RequiresSelection && !hasSelection)
      {
        continue;
      }

      visible.Add(hint);
    }

    // Responsively truncate: drop rightmost non-protected hints until they fit.
    // Protected hints: IsPrimary (Enter action) and the last item (Esc: Close).
    var fitted = FitHintsToWidth(visible, width);

    // Render hints
    Move(0, 0);

    for (var i = 0; i < fitted.Count; i++)
    {
      if (i > 0)
      {
        SetAttribute(Theme.Semantic.Muted);
        AddStr(Separator);
      }

      var hint = fitted[i];

      // Key name in muted (dark gray)
      SetAttribute(Theme.Semantic.Muted);
      AddStr(hint.KeyDisplay);
      AddStr(": ");

      // Label in default (white)
      SetAttribute(Theme.Semantic.Default);
      AddStr(hint.Label);
    }

    return true;
  }

  private static List<ActionBarHint> FitHintsToWidth(
    List<ActionBarHint> hints,
    int availableWidth)
  {
    var result = new List<ActionBarHint>(hints);

    while (result.Count > 1 && MeasureWidth(result) > availableWidth)
    {
      // Find the rightmost droppable hint (not primary, not the last Esc entry)
      var dropIndex = -1;

      for (var i = result.Count - 2; i >= 0; i--)
      {
        if (!result[i].IsPrimary)
        {
          dropIndex = i;
          break;
        }
      }

      if (dropIndex < 0)
      {
        // Only protected hints remain; show them even if they overflow
        break;
      }

      result.RemoveAt(dropIndex);
    }

    return result;
  }

  private static int MeasureWidth(List<ActionBarHint> hints)
  {
    var total = 0;

    for (var i = 0; i < hints.Count; i++)
    {
      if (i > 0)
      {
        total += Separator.Length;
      }

      // "Key: Label"
      total += hints[i].KeyDisplay.Length + 2 + hints[i].Label.Length;
    }

    return total;
  }
}

// ─────────────────────────────────────────────────────────────
//  EmptyStateView — centered message for empty lists
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Renders a centered empty-state message when a list has no items.
/// Implements the empty state variant of pattern #28 (Interactive List).
/// </summary>
internal sealed class EmptyStateView : View
{
  private readonly string _message;
  private readonly string? _hint;

  internal EmptyStateView(string message, string? hint)
  {
    _message = message;
    _hint = hint;
  }

  protected override bool OnDrawingContent(DrawContext? context)
  {
    var width = Viewport.Width;
    var height = Viewport.Height;

    if (width <= 0 || height <= 0)
    {
      return true;
    }

    // Clear the area
    SetAttribute(Theme.Semantic.Default);

    for (var row = 0; row < height; row++)
    {
      Move(0, row);
      AddStr(new string(' ', width));
    }

    // Center the message block vertically
    var lineCount = _hint is not null ? 2 : 1;
    var startY = Math.Max(0, (height - lineCount) / 2);

    // Primary message
    SetAttribute(Theme.Semantic.Muted);
    var msgX = Math.Max(0, (width - _message.Length) / 2);
    Move(msgX, startY);
    AddStr(_message.Length <= width ? _message : _message[..width]);

    // Secondary hint
    if (_hint is not null && startY + 1 < height)
    {
      var hintX = Math.Max(0, (width - _hint.Length) / 2);
      Move(hintX, startY + 1);
      AddStr(_hint.Length <= width ? _hint : _hint[..width]);
    }

    return true;
  }
}

// ─────────────────────────────────────────────────────────────
//  InteractiveListDataSource — custom IListDataSource with arrow indicator
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Custom <see cref="IListDataSource"/> that renders list items with a <c>▶</c> arrow indicator
/// on the selected row and uses <see cref="Theme.List"/> colors for selection highlighting.
/// </summary>
internal sealed class InteractiveListDataSource<T> : IListDataSource, IDisposable
{
  private readonly List<T> _items;
  private readonly Func<T, int, string> _formatter;

  internal InteractiveListDataSource(List<T> items, Func<T, int, string> formatter)
  {
    _items = items;
    _formatter = formatter;
  }

  public void Dispose()
  {
    // No unmanaged resources to release
  }

  public int Count => _items.Count;

  public int MaxItemLength
  {
    get
    {
      var max = 0;

      foreach (var item in _items)
      {
        // Arrow prefix is 2 chars ("▶ " or "  ")
        var len = _formatter(item, 120).Length + 2;

        if (len > max)
        {
          max = len;
        }
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
    if (item < 0 || item >= _items.Count || width <= 0)
    {
      listView.Move(col, row);
      listView.SetAttribute(Theme.Semantic.Default);
      listView.AddStr(new string(' ', width));
      return;
    }

    // Arrow indicator: "▶ " for selected, "  " for others
    var prefix = selected
      ? $"{Theme.Symbols.Arrow} "
      : "  ";

    var textWidth = Math.Max(0, width - prefix.Length);
    var text = _formatter(_items[item], textWidth);

    // Pad or truncate to fill the full row width (prevents rendering artifacts)
    text = FitToWidth(text, textWidth);

    listView.Move(col, row);

    if (selected)
    {
      // Selected row: entire row in selection colors
      listView.SetAttribute(Theme.List.SelectedText);
      listView.AddStr(prefix);
      listView.AddStr(text);
    }
    else
    {
      // Unselected row: prefix space in muted, text in default
      listView.SetAttribute(Theme.Semantic.Muted);
      listView.AddStr(prefix);
      listView.SetAttribute(Theme.Semantic.Default);
      listView.AddStr(text);
    }
  }

  public bool IsMarked(int item) => false;

  public void SetMark(int item, bool value)
  {
    // Marking not supported in interactive list
  }

  public IList ToList() => _items;

  public bool RenderMark(
    ListView listView,
    int item,
    int row,
    bool isMarked,
    bool markMultiple) => false;

  private static string FitToWidth(string text, int width)
  {
    if (width <= 0)
    {
      return string.Empty;
    }

    if (text.Length > width)
    {
      return width > 3
        ? string.Concat(text.AsSpan(0, width - 3), "...")
        : text[..width];
    }

    if (text.Length < width)
    {
      return text + new string(' ', width - text.Length);
    }

    return text;
  }
}
