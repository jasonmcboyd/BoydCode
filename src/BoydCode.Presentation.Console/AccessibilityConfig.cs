using Spectre.Console;

namespace BoydCode.Presentation.Console;

/// <summary>
/// Detects and applies accessibility settings at startup.
/// </summary>
internal static class AccessibilityConfig
{
  /// <summary>
  /// NO_COLOR standard: https://no-color.org/
  /// When set, all color should be disabled.
  /// </summary>
  public static bool NoColor { get; } = !string.IsNullOrEmpty(
    Environment.GetEnvironmentVariable("NO_COLOR"));

  /// <summary>
  /// Accessible mode: reduces animation, uses text-only indicators.
  /// Set via BOYDCODE_ACCESSIBLE env var or --accessible CLI flag.
  /// </summary>
  public static bool Accessible { get; set; } = !string.IsNullOrEmpty(
    Environment.GetEnvironmentVariable("BOYDCODE_ACCESSIBLE"));

  /// <summary>
  /// Apply NO_COLOR to Spectre.Console at startup.
  /// Call this from Program.cs before any Spectre output.
  /// </summary>
  public static void Apply()
  {
    if (NoColor)
    {
      AnsiConsole.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;
    }
  }
}
