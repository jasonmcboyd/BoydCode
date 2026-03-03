using System.Globalization;

namespace BoydCode.Presentation.Console.Terminal;

internal static class TokenFormatting
{
  internal static string FormatCompact(int value)
  {
    return value switch
    {
      >= 1_000_000 => $"{value / 1_000_000.0:F1}M",
      >= 1_000 => $"{value / 1_000.0:F1}k",
      _ => value.ToString(CultureInfo.InvariantCulture),
    };
  }

  internal static string FormatPercent(double value) =>
    $"{value:F1}%";
}
