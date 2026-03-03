namespace BoydCode.Application.Interfaces;

/// <summary>
/// Controls the color of a <see cref="DetailRow"/> value in the detail modal.
/// </summary>
public enum DetailValueStyle
{
  /// <summary>Default: Info (cyan) for single-line, Default (white) for multi-line.</summary>
  Auto,
  /// <summary>Theme.Semantic.Success (green).</summary>
  Success,
  /// <summary>Theme.Semantic.Warning (yellow).</summary>
  Warning,
  /// <summary>Theme.Semantic.Error (red).</summary>
  Error,
  /// <summary>Theme.Semantic.Muted (dim gray).</summary>
  Muted,
  /// <summary>Theme.Semantic.Default (white) forced.</summary>
  Default,
  /// <summary>Theme.Semantic.Info (cyan) forced.</summary>
  Info,
}

/// <summary>
/// A single key-value row in a detail modal.
/// </summary>
/// <param name="Label">The label text, rendered as secondary/muted.</param>
/// <param name="Value">The value text, rendered as info/data or default/text.</param>
/// <param name="IsMultiLine">When true, the value appears below its label instead of beside it.</param>
/// <param name="Style">Controls the value color. <see cref="DetailValueStyle.Auto"/> by default.</param>
public sealed record DetailRow(
  string Label,
  string Value,
  bool IsMultiLine = false,
  DetailValueStyle Style = DetailValueStyle.Auto);

/// <summary>
/// A named section of key-value rows in a detail modal.
/// </summary>
/// <param name="Title">Section title (null for the first untitled section).</param>
/// <param name="Rows">Key-value pairs in this section.</param>
public sealed record DetailSection(string? Title, IReadOnlyList<DetailRow> Rows);
