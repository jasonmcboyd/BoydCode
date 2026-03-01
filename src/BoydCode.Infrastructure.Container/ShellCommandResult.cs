namespace BoydCode.Infrastructure.Container;

internal sealed record ShellCommandResult(
    string Output,
    string? ErrorOutput,
    int ExitCode);
