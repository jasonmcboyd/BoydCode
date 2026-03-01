using BoydCode.Domain.SlashCommands;

namespace BoydCode.Application.Interfaces;

public interface ISlashCommand
{
  SlashCommandDescriptor Descriptor { get; }
  Task<bool> TryHandleAsync(string input, CancellationToken ct = default);
}
