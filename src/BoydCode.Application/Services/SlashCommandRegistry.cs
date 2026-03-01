using BoydCode.Application.Interfaces;
using BoydCode.Domain.SlashCommands;

namespace BoydCode.Application.Services;

public sealed class SlashCommandRegistry : ISlashCommandRegistry
{
  private readonly List<ISlashCommand> _commands = [];

  public void Register(ISlashCommand command)
  {
    _commands.Add(command);
  }

  public async Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    foreach (var command in _commands)
    {
      if (await command.TryHandleAsync(input, ct))
      {
        return true;
      }
    }

    return false;
  }

  public IReadOnlyList<SlashCommandDescriptor> GetAllDescriptors() =>
      _commands.Select(c => c.Descriptor).ToList().AsReadOnly();

  public string? SuggestCommand(string input)
  {
    var firstToken = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    if (firstToken is null) return null;

    string? bestMatch = null;
    var bestDistance = int.MaxValue;

    foreach (var descriptor in _commands.Select(c => c.Descriptor))
    {
      var distance = LevenshteinDistance(firstToken, descriptor.Prefix);
      if (distance < bestDistance)
      {
        bestDistance = distance;
        bestMatch = descriptor.Prefix;
      }
    }

    return bestDistance <= 3 ? bestMatch : null;
  }

  private static int LevenshteinDistance(string a, string b)
  {
    var m = a.Length;
    var n = b.Length;
    var dp = new int[m + 1, n + 1];

    for (var i = 0; i <= m; i++) dp[i, 0] = i;
    for (var j = 0; j <= n; j++) dp[0, j] = j;

    for (var i = 1; i <= m; i++)
    {
      for (var j = 1; j <= n; j++)
      {
        var cost = a[i - 1] == b[j - 1] ? 0 : 1;
        dp[i, j] = Math.Min(
            Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
            dp[i - 1, j - 1] + cost);
      }
    }

    return dp[m, n];
  }
}
