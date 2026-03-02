using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using TguiApp = Terminal.Gui.App.Application;
using Attribute = Terminal.Gui.Drawing.Attribute;

#pragma warning disable CS0618 // Application.Run/RequestStop - using legacy static API during Terminal.Gui migration
#pragma warning disable IDE0060 // Unused parameter in OnDrawingContent override

namespace BoydCode.Presentation.Console.Terminal;

/// <summary>
/// The result of running a <see cref="WizardDialog"/>.
/// </summary>
/// <param name="Completed">True if the user completed all steps (pressed Done); false if cancelled.</param>
/// <param name="LastStep">The 1-based index of the last step the user was on when the wizard closed.</param>
internal sealed record WizardResult(bool Completed, int LastStep);

/// <summary>
/// A single step in a <see cref="WizardDialog"/>.
/// </summary>
/// <param name="Title">The display title for this step (shown in the step indicator).</param>
/// <param name="CreateContent">Factory that creates the view hierarchy for this step's content area.</param>
/// <param name="Validate">Optional validation function. Returns true if the step is valid and the wizard
/// can advance. When null, the step is always considered valid.</param>
internal sealed record WizardStep(
  string Title,
  Func<View> CreateContent,
  Func<bool>? Validate = null);

/// <summary>
/// A multi-step wizard dialog. Shows a step indicator, swappable content area, and
/// Cancel/Back/Next(Done) navigation buttons. Implements UX pattern #32 from
/// <c>docs/ux/07-component-patterns.md</c>.
/// </summary>
internal sealed class WizardDialog : IDisposable
{
  private bool _disposed;
  private static readonly Attribute StepIndicatorAttr =
    new(ColorName16.White, Color.None, TextStyle.Bold);

  private static readonly Attribute StepRuleAttr =
    new(ColorName16.DarkGray, Color.None);

  private readonly string _title;
  private readonly IReadOnlyList<WizardStep> _steps;
  private readonly int _totalSteps;
  private readonly string _doneButtonText;
  private readonly Func<bool>? _hasUnsavedData;

  private int _currentStep;
  private int _minStep = 1;
  private bool _completed;

  // UI elements — initialized in Show()
  private Dialog _dialog = null!;
  private Label _stepLabel = null!;
  private StepRuleView _stepRule = null!;
  private View _contentArea = null!;
  private Button _cancelButton = null!;
  private Button _backButton = null!;
  private Button _nextButton = null!;

  /// <summary>
  /// Creates a new wizard dialog.
  /// </summary>
  /// <param name="title">The dialog title shown in the border.</param>
  /// <param name="steps">The ordered list of wizard steps. Must contain at least one step.</param>
  /// <param name="doneButtonText">Text for the final step's action button (default "Done").</param>
  /// <param name="hasUnsavedData">Optional function that returns true when the wizard has unsaved data.
  /// When provided and returning true, cancelling will prompt for confirmation.</param>
  /// <exception cref="ArgumentException">Thrown when <paramref name="steps"/> is empty.</exception>
  internal WizardDialog(string title, IReadOnlyList<WizardStep> steps, string doneButtonText = "Done", Func<bool>? hasUnsavedData = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(title);
    ArgumentNullException.ThrowIfNull(steps);

    if (steps.Count == 0)
    {
      throw new ArgumentException("A wizard must have at least one step.", nameof(steps));
    }

    _title = title;
    _steps = steps;
    _totalSteps = steps.Count;
    _doneButtonText = doneButtonText;
    _hasUnsavedData = hasUnsavedData;
  }

  /// <summary>
  /// Shows the wizard modally. Blocks until the user completes or cancels.
  /// </summary>
  /// <param name="startStep">The 1-based step index to start on (default 1).
  /// The Back button is hidden on this step, preventing navigation before it.</param>
  /// <returns>A <see cref="WizardResult"/> indicating whether the wizard was completed
  /// and which step was last active.</returns>
  /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="startStep"/>
  /// is less than 1 or greater than the number of steps.</exception>
  internal WizardResult Show(int startStep = 1)
  {
    if (startStep < 1 || startStep > _totalSteps)
    {
      throw new ArgumentOutOfRangeException(nameof(startStep));
    }

    _completed = false;
    _currentStep = 0;
    _minStep = startStep;

    _dialog = new Dialog
    {
      Title = _title,
      Width = Dim.Percent(70),
      Height = Dim.Percent(60),
      BorderStyle = LineStyle.Rounded,
    };

    _dialog.Border?.SetScheme(Theme.Modal.BorderScheme);

    // Step indicator label: "Step 1 of N: {title}" — bold white
    _stepLabel = new Label
    {
      X = 2,
      Y = 1,
      Width = Dim.Fill(2),
      Height = 1,
    };

    // Dim horizontal rule below the step indicator
    _stepRule = new StepRuleView
    {
      X = 2,
      Y = 2,
      Width = Dim.Fill(2),
      Height = 1,
    };

    // Content area where each step's views are swapped in
    _contentArea = new View
    {
      X = 2,
      Y = 4,
      Width = Dim.Fill(2),
      Height = Dim.Fill(2),
    };

    _dialog.Add(_stepLabel, _stepRule, _contentArea);

    // Navigation buttons
    _cancelButton = new Button { Text = "Cancel" };
    _backButton = new Button { Text = "< Back" };
    _nextButton = new Button { Text = "Next >", IsDefault = true };

    _cancelButton.Accepting += OnCancel;
    _backButton.Accepting += OnBack;
    _nextButton.Accepting += OnNext;

    _dialog.AddButton(_cancelButton);
    _dialog.AddButton(_backButton);
    _dialog.AddButton(_nextButton);

    // Initialize to the starting step
    GoToStep(startStep);

    // Run modally
    TguiApp.Run(_dialog);

    return new WizardResult(_completed, _currentStep);
  }

  /// <summary>
  /// Transitions the wizard to the specified step (1-based).
  /// Updates the step indicator, button visibility/text, and swaps the content area.
  /// </summary>
  private void GoToStep(int step)
  {
    _currentStep = step;

    // Update step indicator text
    _stepLabel.Text = $"Step {step} of {_totalSteps}: {_steps[step - 1].Title}";

    // Show/hide Back button (hidden on the minimum step)
    _backButton.Visible = step > _minStep;

    // Change Next text to done button text on the final step
    _nextButton.Text = step == _totalSteps ? _doneButtonText : "Next >";

    // Swap content area children
    _contentArea.RemoveAll();
    var stepView = _steps[step - 1].CreateContent();
    _contentArea.Add(stepView);
    _contentArea.SetNeedsDraw();

    // Focus the first focusable child in the content area
    _contentArea.AdvanceFocus(NavigationDirection.Forward, null);
  }

  private void OnCancel(object? sender, CommandEventArgs args)
  {
    args.Handled = true; // Prevent default Dialog close behavior

    if (_hasUnsavedData is not null && _hasUnsavedData())
    {
      var result = MessageBox.Query(TguiApp.Instance, "Discard Changes", "Discard changes?", "No", "Yes");
      if (result != 1)
      {
        return; // User chose not to discard
      }
    }

    _completed = false;
    TguiApp.RequestStop();
  }

  private void OnBack(object? sender, CommandEventArgs args)
  {
    args.Handled = true; // Prevent default Dialog close behavior
    if (_currentStep > _minStep)
    {
      GoToStep(_currentStep - 1);
    }
  }

  private void OnNext(object? sender, CommandEventArgs args)
  {
    args.Handled = true; // Prevent default Dialog close behavior

    // Validate the current step before advancing
    var validate = _steps[_currentStep - 1].Validate;
    if (validate is not null && !validate())
    {
      return; // Validation failed — stay on current step
    }

    if (_currentStep < _totalSteps)
    {
      // Advance to next step
      GoToStep(_currentStep + 1);
    }
    else
    {
      // Final step completed
      _completed = true;
      TguiApp.RequestStop();
    }
  }

  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;
    _dialog?.Dispose();
    _stepLabel?.Dispose();
    _stepRule?.Dispose();
    _contentArea?.Dispose();
    _cancelButton?.Dispose();
    _backButton?.Dispose();
    _nextButton?.Dispose();
  }

  /// <summary>
  /// A simple view that draws a dim horizontal rule using <see cref="Theme.Symbols.Rule"/>.
  /// </summary>
  private sealed class StepRuleView : View
  {
    protected override bool OnDrawingContent(DrawContext? context)
    {
      var width = Viewport.Width;
      if (width <= 0)
      {
        return true;
      }

      SetAttribute(StepRuleAttr);
      Move(0, 0);
      AddStr(new string(Theme.Symbols.Rule, width));

      return true;
    }
  }
}
