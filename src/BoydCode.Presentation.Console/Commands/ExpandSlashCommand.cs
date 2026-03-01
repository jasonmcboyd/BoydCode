using BoydCode.Application.Interfaces;
using BoydCode.Domain.SlashCommands;

namespace BoydCode.Presentation.Console.Commands;

public sealed class ExpandSlashCommand : ISlashCommand
{
  private readonly IUserInterface _ui;

  public ExpandSlashCommand(IUserInterface ui)
  {
    _ui = ui;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/expand",
      "Show full output from the last tool execution",
      []);

  public Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    var trimmed = input.Trim();
    if (!trimmed.Equals("/expand", StringComparison.OrdinalIgnoreCase))
    {
      return Task.FromResult(false);
    }

    _ui.ExpandLastToolOutput();
    return Task.FromResult(true);
  }
}
