using BoydCode.Domain.SlashCommands;

namespace BoydCode.Application.Interfaces;

public interface ISlashCommandRegistry
{
  void Register(ISlashCommand command);
  Task<bool> TryHandleAsync(string input, CancellationToken ct = default);
  IReadOnlyList<SlashCommandDescriptor> GetAllDescriptors();
  string? SuggestCommand(string input);
}
