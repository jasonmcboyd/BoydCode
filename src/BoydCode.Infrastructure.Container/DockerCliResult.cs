namespace BoydCode.Infrastructure.Container;

internal sealed record DockerCliResult(
    string StandardOutput,
    string StandardError,
    int ExitCode)
{
  internal bool Succeeded => ExitCode == 0;
}
