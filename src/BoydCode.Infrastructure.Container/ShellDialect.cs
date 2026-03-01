namespace BoydCode.Infrastructure.Container;

internal sealed class ShellDialect
{
  private const string StartPrefix = "___BOYDCODE_START_";
  private const string ExitPrefix = "___BOYDCODE_EXIT_";
  private const string SentinelSuffix = "___";

  private readonly bool _isPowerShell;

  internal ShellDialect(string shellName)
  {
    _isPowerShell = shellName.Contains("pwsh", StringComparison.OrdinalIgnoreCase)
        || shellName.Contains("powershell", StringComparison.OrdinalIgnoreCase);
  }

  internal string WrapWithSentinel(string command, string marker)
  {
    var startSentinel = $"{StartPrefix}{marker}{SentinelSuffix}";
    var exitSentinel = $"{ExitPrefix}{marker}";

    if (_isPowerShell)
    {
      return $"Write-Output \"{startSentinel}\"\n{command}\n$__bc_ec = if ($?) {{ 0 }} else {{ 1 }}; Write-Output \"{exitSentinel}_${{__bc_ec}}{SentinelSuffix}\"";
    }

    return $"echo \"{startSentinel}\"\n{command}\n__bc_ec=$?; echo \"{exitSentinel}_${{__bc_ec}}{SentinelSuffix}\"";
  }

  /// <summary>
  /// Builds the expected start sentinel line for a given marker.
  /// Pre-compute once per command to avoid per-line allocations.
  /// </summary>
  internal static string BuildStartPattern(string marker) =>
      $"{StartPrefix}{marker}{SentinelSuffix}";

  /// <summary>
  /// Builds the exit sentinel search prefix for a given marker.
  /// Pre-compute once per command to avoid per-line allocations.
  /// </summary>
  internal static string BuildExitPattern(string marker) =>
      $"{ExitPrefix}{marker}_";

  internal static bool IsStartSentinel(string line, string startPattern) =>
      line.Contains(startPattern, StringComparison.Ordinal)
      && line.TrimEnd().EndsWith(SentinelSuffix, StringComparison.Ordinal);

  internal static bool IsExitSentinel(string line, string exitPattern) =>
      line.Contains(exitPattern, StringComparison.Ordinal)
      && line.EndsWith(SentinelSuffix, StringComparison.Ordinal);

  internal static int ParseExitCode(string sentinelLine, string exitPattern)
  {
    var startIdx = sentinelLine.IndexOf(exitPattern, StringComparison.Ordinal);
    if (startIdx < 0) return 1;

    startIdx += exitPattern.Length;
    var endIdx = sentinelLine.IndexOf(SentinelSuffix, startIdx, StringComparison.Ordinal);
    if (endIdx < 0) return 1;

    return int.TryParse(sentinelLine.AsSpan(startIdx, endIdx - startIdx), out var code) ? code : 1;
  }
}
