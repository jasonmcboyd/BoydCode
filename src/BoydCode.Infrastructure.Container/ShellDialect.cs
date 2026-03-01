using System.Text.RegularExpressions;

namespace BoydCode.Infrastructure.Container;

internal sealed class ShellDialect
{
  private const string SentinelPrefix = "___BOYDCODE_EXIT_";
  private const string SentinelSuffix = "___";

  private readonly bool _isPowerShell;

  internal ShellDialect(string shellName)
  {
    _isPowerShell = shellName.Contains("pwsh", StringComparison.OrdinalIgnoreCase)
        || shellName.Contains("powershell", StringComparison.OrdinalIgnoreCase);
  }

  internal string WrapWithSentinel(string command, string marker)
  {
    if (_isPowerShell)
    {
      return $"{command}\n$__bc_ec = if ($?) {{ 0 }} else {{ 1 }}; Write-Output \"{SentinelPrefix}{marker}_${{__bc_ec}}{SentinelSuffix}\"";
    }

    return $"{command}\n__bc_ec=$?; echo \"{SentinelPrefix}{marker}_${{__bc_ec}}{SentinelSuffix}\"";
  }

  internal static bool IsSentinel(string line, string marker) =>
      line.Contains($"{SentinelPrefix}{marker}_", StringComparison.Ordinal)
      && line.EndsWith(SentinelSuffix, StringComparison.Ordinal);

  internal static int ParseExitCode(string sentinelLine, string marker)
  {
    var match = Regex.Match(sentinelLine, $@"{Regex.Escape(SentinelPrefix)}{Regex.Escape(marker)}_(\d+){Regex.Escape(SentinelSuffix)}");
    return match.Success && int.TryParse(match.Groups[1].Value, out var code) ? code : 1;
  }
}
