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
}
