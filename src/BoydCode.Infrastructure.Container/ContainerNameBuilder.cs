using System.Text.RegularExpressions;

namespace BoydCode.Infrastructure.Container;

internal static partial class ContainerNameBuilder
{
  internal const string Prefix = "boydcode-";

  internal static string Build(string projectName)
  {
    var sanitized = SanitizeRegex().Replace(projectName.ToLowerInvariant(), "-");
    sanitized = CollapseHyphensRegex().Replace(sanitized, "-").Trim('-');
    if (string.IsNullOrEmpty(sanitized))
    {
      sanitized = "project";
    }
    var shortGuid = Guid.NewGuid().ToString("N")[..8];
    return $"{Prefix}{sanitized}-{shortGuid}";
  }

  [GeneratedRegex("[^a-z0-9]")]
  private static partial Regex SanitizeRegex();

  [GeneratedRegex("-{2,}")]
  private static partial Regex CollapseHyphensRegex();
}
