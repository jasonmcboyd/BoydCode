namespace BoydCode.Domain.SlashCommands;

public sealed record SlashCommandDescriptor(
    string Prefix,
    string Description,
    IReadOnlyList<SlashSubcommandDescriptor> Subcommands);

public sealed record SlashSubcommandDescriptor(string Usage, string Description);
