using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TguiApp = Terminal.Gui.App.Application;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable CS0618 // Application.Run/RequestStop - using legacy static API during Terminal.Gui migration

namespace BoydCode.Presentation.Console.Terminal;

/// <summary>
/// The result of a successfully submitted form dialog.
/// </summary>
internal sealed record FormResult(IReadOnlyDictionary<string, string> Values);

/// <summary>
/// Builder for modal form dialogs with labeled text fields, secret fields,
/// and multi-line text areas. Produces a <see cref="Dialog"/> styled per the
/// UX design system (Theme.Modal, Theme.Semantic, Theme.Input).
/// </summary>
internal sealed class FormDialogBuilder
{
  private string _title = "Form";
  private string _confirmText = "Ok";
  private string _cancelText = "Cancel";
  private readonly List<FieldDefinition> _fields = [];

  // ── Fluent configuration ──────────────────────────────────

  /// <summary>Sets the dialog title shown in the border.</summary>
  public FormDialogBuilder SetTitle(string title)
  {
    _title = title;
    return this;
  }

  /// <summary>Sets the confirm button label (default "Ok").</summary>
  public FormDialogBuilder SetConfirmText(string text)
  {
    _confirmText = text;
    return this;
  }

  /// <summary>Sets the cancel button label (default "Cancel").</summary>
  public FormDialogBuilder SetCancelText(string text)
  {
    _cancelText = text;
    return this;
  }

  /// <summary>Adds a single-line text input field.</summary>
  public FormDialogBuilder AddTextField(
    string label,
    string? defaultValue = null,
    Func<string, string?>? validate = null)
  {
    _fields.Add(new FieldDefinition(label, FieldKind.Text, defaultValue, validate, false, 1));
    return this;
  }

  /// <summary>Adds a masked secret input field (e.g. for API keys).</summary>
  public FormDialogBuilder AddSecretField(string label)
  {
    _fields.Add(new FieldDefinition(label, FieldKind.Secret, null, null, false, 1));
    return this;
  }

  /// <summary>Adds a multi-line text area below its label.</summary>
  public FormDialogBuilder AddTextArea(
    string label,
    string? defaultValue = null,
    int height = 3)
  {
    _fields.Add(new FieldDefinition(label, FieldKind.TextArea, defaultValue, null, false, height));
    return this;
  }

  // ── Show ──────────────────────────────────────────────────

  /// <summary>
  /// Builds and shows the form dialog modally. Returns a <see cref="FormResult"/>
  /// with all field values keyed by label, or <c>null</c> if the user cancelled.
  /// </summary>
  /// <remarks>
  /// Must be called on the Terminal.Gui UI thread (inside <c>Application.Invoke</c>
  /// or from an event handler).
  /// </remarks>
  public FormResult? Show()
  {
    if (_fields.Count == 0)
    {
      return null;
    }

    // Compute label X offset: longest label + padding + left margin
    const int leftMargin = 2;
    const int rightMargin = 2;
    const int labelFieldGap = 2;

    var maxLabelLength = 0;
    foreach (var field in _fields)
    {
      // TextArea labels are stacked (label above field), so they don't
      // influence the side-by-side label column width.
      if (field.Kind != FieldKind.TextArea && field.Label.Length > maxLabelLength)
      {
        maxLabelLength = field.Label.Length;
      }
    }

    // +1 accounts for the ":" suffix appended to each label
    var fieldX = leftMargin + maxLabelLength + 1 + labelFieldGap;

    // Build dialog
    var dialog = new Dialog
    {
      Title = _title,
      Width = Dim.Percent(70),
      BorderStyle = LineStyle.Rounded,
    };
    dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

    // Bold label attribute
    var boldLabel = new Attribute(ColorName16.White, Color.None, TextStyle.Bold);

    // Track views for each field
    var fieldEntries = new List<FieldEntry>();

    // Layout: Y position tracks where the next row goes.
    // Start at Y=1 to leave a blank row inside the dialog border.
    var currentY = 1;

    foreach (var fieldDef in _fields)
    {
      if (fieldDef.Kind == FieldKind.TextArea)
      {
        // Text areas: label on its own line, field below spanning full width
        var label = new Label
        {
          Text = fieldDef.Label + ":",
          X = leftMargin,
          Y = currentY,
        };
        label.SetScheme(new Scheme(boldLabel));
        dialog.Add(label);
        currentY++;

        var textView = new TextView
        {
          X = leftMargin,
          Y = currentY,
          Width = Dim.Fill(rightMargin),
          Height = fieldDef.Height,
          Text = fieldDef.DefaultValue ?? string.Empty,
        };
        dialog.Add(textView);

        // Error label below the text area (hidden initially)
        var errorLabel = new Label
        {
          X = leftMargin,
          Y = currentY + fieldDef.Height,
          Width = Dim.Fill(rightMargin),
          Visible = false,
        };
        errorLabel.SetScheme(new Scheme(Theme.Semantic.Error));
        dialog.Add(errorLabel);

        fieldEntries.Add(new FieldEntry(fieldDef, null, textView, errorLabel));

        // Advance Y past the text area + error label row + blank line
        currentY += fieldDef.Height + 1 + 1;
      }
      else
      {
        // Single-line fields (Text or Secret): label and field on the same row
        var label = new Label
        {
          Text = fieldDef.Label + ":",
          X = leftMargin,
          Y = currentY,
        };
        label.SetScheme(new Scheme(boldLabel));
        dialog.Add(label);

        var textField = new TextField
        {
          X = fieldX,
          Y = currentY,
          Width = Dim.Fill(rightMargin),
          Text = fieldDef.DefaultValue ?? string.Empty,
          Secret = fieldDef.Kind == FieldKind.Secret,
        };
        dialog.Add(textField);

        // Error label below the field (hidden initially)
        var errorLabel = new Label
        {
          X = fieldX,
          Y = currentY + 1,
          Width = Dim.Fill(rightMargin),
          Visible = false,
        };
        errorLabel.SetScheme(new Scheme(Theme.Semantic.Error));
        dialog.Add(errorLabel);

        fieldEntries.Add(new FieldEntry(fieldDef, textField, null, errorLabel));

        // Advance Y past the field + error label row + blank line
        currentY += 3;
      }
    }

    // Explicit height: content rows + button area (managed by Dialog).
    // Dialog's default Dim.Auto can undersize when subviews use Dim.Fill for width,
    // so we compute height explicitly: currentY content rows + ~3 rows for the
    // button container and bottom border chrome.
    dialog.Height = currentY + 3;

    // Buttons: Cancel first (non-default), then Confirm (default, added last)
    var cancelButton = new Button { Text = _cancelText };
    var confirmButton = new Button { Text = _confirmText };

    dialog.AddButton(cancelButton);
    dialog.AddButton(confirmButton);

    // Validation state
    var validationPassed = false;

    // Intercept the dialog's Accepting event to validate before closing
    dialog.Accepting += (sender, args) =>
    {
      // Only validate on the confirm button (the default/last button).
      // When Cancel is pressed or Esc, Dialog routes through Activating, not Accepting.
      if (!ValidateAll(fieldEntries))
      {
        args.Handled = true;
        return;
      }
      validationPassed = true;
    };

    // Run the dialog modally
    TguiApp.Run(dialog);

    // Check result: Canceled is true for Esc or non-default buttons
    if (dialog.Canceled || !validationPassed)
    {
      dialog.Dispose();
      return null;
    }

    // Collect values
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var entry in fieldEntries)
    {
      var value = entry.TextField is not null
        ? entry.TextField.Text ?? string.Empty
        : entry.TextView?.Text ?? string.Empty;
      values[entry.Definition.Label] = value;
    }

    dialog.Dispose();
    return new FormResult(values);
  }

  // ── Validation ────────────────────────────────────────────

  private static bool ValidateAll(List<FieldEntry> entries)
  {
    var allValid = true;

    foreach (var entry in entries)
    {
      var value = entry.TextField is not null
        ? entry.TextField.Text ?? string.Empty
        : entry.TextView?.Text ?? string.Empty;

      if (entry.Definition.Validate is not null)
      {
        var error = entry.Definition.Validate(value);
        if (error is not null)
        {
          entry.ErrorLabel.Text = error;
          entry.ErrorLabel.Visible = true;
          allValid = false;
        }
        else
        {
          entry.ErrorLabel.Visible = false;
        }
      }
      else
      {
        entry.ErrorLabel.Visible = false;
      }
    }

    return allValid;
  }

  // ── Internal types ────────────────────────────────────────

  private enum FieldKind
  {
    Text,
    Secret,
    TextArea,
  }

  private sealed record FieldDefinition(
    string Label,
    FieldKind Kind,
    string? DefaultValue,
    Func<string, string?>? Validate,
    bool IsOptional,
    int Height);

  private sealed record FieldEntry(
    FieldDefinition Definition,
    TextField? TextField,
    TextView? TextView,
    Label ErrorLabel);
}
