# Terminal.Gui v2 API Reference (build 5092)

Quick reference for the Terminal.Gui v2 develop build used in this project. Pin version: `2.0.0-develop.5092`.

## ANSI Rendering Gap

Terminal.Gui v2 does NOT support ANSI escape sequences in views (issue #1097, tagged "Post-V2"). Spectre.Console's string-rendering bridge (`AnsiConsole.Create()` with `StringWriter`) produces ANSI codes that render as garbage in Terminal.Gui views.

**Correct approach**: Use Terminal.Gui's native drawing API (`SetAttribute`, `Move`, `AddStr`) for all content inside the TUI. The content model uses typed `ConversationBlock` records that draw themselves using Terminal.Gui primitives.

Spectre.Console remains only for:
- Startup banner (rendered to stdout BEFORE `Application.Init()`)
- Non-interactive/piped output (no Terminal.Gui in this path)
- Slash command interactive prompts (during Terminal.Gui suspension)

## Namespace Restructuring

Build 5092 reorganized types into sub-namespaces:

| Type | Namespace | Notes |
|------|-----------|-------|
| `View`, `Pos`, `Dim` | `Terminal.Gui.ViewBase` | |
| `Color`, `Attribute`, `ColorName16` | `Terminal.Gui.Drawing` | Need `using Attribute = Terminal.Gui.Drawing.Attribute;` alias |
| `Key` | `Terminal.Gui.Input` | |
| `Application` | `Terminal.Gui.App` | Need `using TguiApp = Terminal.Gui.App.Application;` alias |
| `Window`, `Dialog`, `TextView` | `Terminal.Gui.Views` | |
| `Toplevel` | **Removed** | Root view is just `View` |

## Namespace Collision: `Application`

`Terminal.Gui.App.Application` collides with the `BoydCode.Application` namespace. Use the `TguiApp` alias:

```csharp
using TguiApp = Terminal.Gui.App.Application;

// Then use:
TguiApp.Invoke(() => { ... });
TguiApp.AddTimeout(TimeSpan.FromMilliseconds(100), () => { ... });
```

## Colors

Named colors use the `ColorName16` enum (not `Color.X` static properties):

```csharp
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

// Named 16-color palette
var attr = new Attribute(ColorName16.Green, ColorName16.Black);

// RGB color (24-bit)
var custom = new Color(50, 50, 50);
var attr2 = new Attribute(ColorName16.White, custom);
```

Available `ColorName16` values: `Black`, `Blue`, `Green`, `Cyan`, `Red`, `Magenta`, `Yellow`, `White`, `BrightBlack` (aka DarkGray), `BrightBlue`, `BrightGreen`, `BrightCyan`, `BrightRed`, `BrightMagenta`, `BrightYellow`, `BrightWhite`, `DarkGray`.

## Custom View Drawing

Override `OnDrawingContent(DrawContext?)` (returns `bool`, true = handled):

```csharp
#pragma warning disable IDE0060 // context param required by override signature

protected override bool OnDrawingContent(DrawContext? context)
{
    var width = Viewport.Width;

    // Clear row
    SetAttribute(new Attribute(ColorName16.White, ColorName16.Black));
    Move(0, 0);
    AddStr(new string(' ', width));

    // Draw colored text
    Move(0, 0);
    SetAttribute(new Attribute(ColorName16.Cyan, ColorName16.Black));
    AddStr("Hello");

    return true;
}
```

Drawing methods (on `View`):
- `Move(col, row)` — position cursor within view coordinates
- `SetAttribute(Attribute)` — set fg/bg colors for subsequent output
- `AddStr(string)` — write text at cursor position
- `SetNeedsDraw()` — request repaint

## Timers

```csharp
#pragma warning disable CS0618 // legacy static API

using TguiApp = Terminal.Gui.App.Application;

// Start timer (returns token)
object? token = TguiApp.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
{
    // Return true to continue, false to stop
    return true;
});

// Stop timer
TguiApp.RemoveTimeout(token);
```

Note: `AddTimeout`/`RemoveTimeout` are marked `[Obsolete]` in this build ("legacy static Application going away"). Suppress CS0618.

## Thread-Safe UI Updates

```csharp
TguiApp.Invoke(() =>
{
    // Runs on the UI thread
    _conversationView.AddBlock(new AssistantTextBlock(text));
});
```

## View Layout

```csharp
using Terminal.Gui.ViewBase;

var view = new View
{
    X = 0,
    Y = Pos.AnchorEnd(1),     // 1 row from bottom
    Width = Dim.Fill(),         // fill available width
    Height = 1,                 // fixed 1 row
};

// Relative positioning
activityBar.Y = Pos.Top(inputView) - 1;

// Fill with reserved space
conversationView.Height = Dim.Fill(3); // leave 3 rows at bottom
```

## Key Handling

```csharp
using Terminal.Gui.Input;

protected override bool OnKeyDown(Key key)
{
    if (key == Key.Enter) { ... }
    if (key == Key.Enter.WithShift) { ... }
    if (key == Key.A.WithCtrl) { ... }
    if (key == Key.Esc) { ... }
    if (key == Key.PageUp) { ... }
    if (key == Key.CursorUp) { ... }
    if (key == Key.Home.WithCtrl) { ... }

    // Printable character
    var rune = key.AsRune;
    if (rune.Value != 0 && !key.IsCtrl && !key.IsAlt && !char.IsControl((char)rune.Value))
    {
        InsertChar((char)rune.Value);
        return true;
    }

    return base.OnKeyDown(key);
}
```

## Application Lifecycle

```csharp
using TguiApp = Terminal.Gui.App.Application;

// Init
TguiApp.Init();

// Run (blocks main thread)
TguiApp.Run(rootView);

// Cleanup (use instead of Shutdown which is obsolete)
TguiApp.Shutdown();
```

## Property Hiding

`View.Enabled` exists as a base property. Custom views that wrap it need `new`:

```csharp
public new bool Enabled
{
    get => _enabled;
    set { _enabled = value; SetNeedsDraw(); }
}
```

## Project Patterns

Established patterns in this codebase:

| Pattern | Example |
|---------|---------|
| Attribute alias | `using Attribute = Terminal.Gui.Drawing.Attribute;` |
| App alias | `using TguiApp = Terminal.Gui.App.Application;` |
| DrawContext pragma | `#pragma warning disable IDE0060` |
| Timer pragma | `#pragma warning disable CS0618` |
| Color constants | `private static readonly Attribute FooAttr = new(ColorName16.X, ColorName16.Black);` |
| Truncate helper | `private static string Truncate(string text, int maxWidth)` |
| SetNeedsDraw | Call after any state change that affects rendering |
