using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Container;

internal sealed partial class DockerCli
{
  private readonly ILogger<DockerCli> _logger;

  internal DockerCli(ILogger<DockerCli> logger)
  {
    _logger = logger;
  }

  internal async Task<DockerCliResult> RunAsync(
      string arguments,
      TimeSpan? timeout = null,
      CancellationToken ct = default)
  {
    var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
    LogDockerCommand(arguments);

    var psi = new ProcessStartInfo
    {
      FileName = "docker",
      Arguments = arguments,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };

    using var process = Process.Start(psi)
        ?? throw new InvalidOperationException("Failed to start docker process.");

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(effectiveTimeout);

    var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
    var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

    try
    {
      await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
      throw;
    }

    var stdout = await stdoutTask.ConfigureAwait(false);
    var stderr = await stderrTask.ConfigureAwait(false);

    LogDockerResult(process.ExitCode, stdout.Length, stderr.Length);
    return new DockerCliResult(stdout.Trim(), stderr.Trim(), process.ExitCode);
  }

  internal Process StartInteractive(string arguments)
  {
    LogDockerInteractive(arguments);

    var psi = new ProcessStartInfo
    {
      FileName = "docker",
      Arguments = arguments,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };

    return Process.Start(psi)
        ?? throw new InvalidOperationException("Failed to start interactive docker process.");
  }

  [LoggerMessage(Level = LogLevel.Debug, Message = "Running: docker {Arguments}")]
  private partial void LogDockerCommand(string arguments);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Docker result: exit={ExitCode}, stdout={StdoutLength}B, stderr={StderrLength}B")]
  private partial void LogDockerResult(int exitCode, int stdoutLength, int stderrLength);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Starting interactive: docker {Arguments}")]
  private partial void LogDockerInteractive(string arguments);
}
