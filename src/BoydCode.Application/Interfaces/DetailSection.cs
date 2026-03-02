namespace BoydCode.Application.Interfaces;

/// <summary>
/// A single key-value row in a detail modal.
/// </summary>
/// <param name="Label">The label text, rendered as secondary/muted.</param>
/// <param name="Value">The value text, rendered as info/data or default/text.</param>
/// <param name="IsMultiLine">When true, the value appears below its label instead of beside it.</param>
public sealed record DetailRow(string Label, string Value, bool IsMultiLine = false);

/// <summary>
/// A named section of key-value rows in a detail modal.
/// </summary>
/// <param name="Title">Section title (null for the first untitled section).</param>
/// <param name="Rows">Key-value pairs in this section.</param>
public sealed record DetailSection(string? Title, IReadOnlyList<DetailRow> Rows);
