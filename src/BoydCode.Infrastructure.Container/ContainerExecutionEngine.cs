using System.Diagnostics;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Container;

public sealed partial class ContainerExecutionEngine : IExecutionEngine
{
  private readonly ContainerConfig _containerConfig;
  private readonly IReadOnlyList<ResolvedDirectory> _directories;
  private readonly string _projectName;
  private readonly DockerCli _dockerCli;
  private readonly ILoggerFactory _loggerFactory;
  private readonly ILogger<ContainerExecutionEngine> _logger;

  private string? _containerName;
  private PersistentShellSession? _shellSession;
  private Dictionary<string, string> _hostToContainerPaths = new(StringComparer.OrdinalIgnoreCase);

  internal ContainerExecutionEngine(
      ContainerConfig containerConfig,
      IReadOnlyList<ResolvedDirectory> directories,
      string projectName,
      DockerCli dockerCli,
      ILoggerFactory loggerFactory)
  {
    _containerConfig = containerConfig;
    _directories = directories;
    _projectName = projectName;
    _dockerCli = dockerCli;
    _loggerFactory = loggerFactory;
    _logger = loggerFactory.CreateLogger<ContainerExecutionEngine>();
  }

  public async Task InitializeAsync(CancellationToken ct = default)
  {
    // 1. Check Docker availability
    var versionResult = await _dockerCli.RunAsync("version --format '{{.Server.Version}}'", ct: ct);
    if (!versionResult.Succeeded)
    {
      throw new InvalidOperationException(
          $"Docker is not available. Ensure Docker Desktop is running. Error: {versionResult.StandardError}");
    }
    LogDockerAvailable(versionResult.StandardOutput);

    // 2. Clean stale containers
    await CleanStaleContainersAsync(ct);

    // 3. Build volume mounts
    var volumeArgs = VolumeMountBuilder.Build(_directories);
    _hostToContainerPaths = VolumeMountBuilder.BuildPathMapping(_directories);

    // 4. Build container name
    _containerName = ContainerNameBuilder.Build(_projectName);

    // 5. Start container
    var runArgs = BuildRunArguments(_containerName, volumeArgs);
    var runResult = await _dockerCli.RunAsync(runArgs, timeout: TimeSpan.FromSeconds(120), ct: ct);
    if (!runResult.Succeeded)
    {
      throw new InvalidOperationException(
          $"Failed to start container: {runResult.StandardError}");
    }
    LogContainerStarted(_containerName, _containerConfig.Image);

    // 6. Start persistent shell session
    var execArgs = $"exec -i {_containerName} {_containerConfig.Shell}";
    var process = _dockerCli.StartInteractive(execArgs);
    var dialect = new ShellDialect(_containerConfig.Shell);
    _shellSession = new PersistentShellSession(
        process,
        dialect,
        _loggerFactory.CreateLogger<PersistentShellSession>());

    // 7. Set initial working directory (use first actual mount, not bare root)
    var initialDir = _hostToContainerPaths.Values.FirstOrDefault() ?? "/";
    await _shellSession.ExecuteAsync($"cd {initialDir}", timeoutMs: 5000, ct);
  }

  public async Task<ShellExecutionResult> ExecuteAsync(
      string command,
      string workingDirectory,
      int timeoutMs = 120_000,
      CancellationToken ct = default)
  {
    if (_shellSession is null)
    {
      throw new InvalidOperationException("Engine not initialized. Call InitializeAsync first.");
    }

    var sw = Stopwatch.StartNew();

    // Translate host working directory to container path (cascade: exact match → first mount → root)
    var containerPath = VolumeMountBuilder.ResolveContainerPath(workingDirectory, _hostToContainerPaths)
        ?? _hostToContainerPaths.Values.FirstOrDefault()
        ?? "/";

    var composedCommand = $"cd {containerPath} && {command}";
    var result = await _shellSession.ExecuteAsync(composedCommand, timeoutMs, ct);
    sw.Stop();

    return new ShellExecutionResult(
        result.Output,
        result.ErrorOutput,
        result.ExitCode != 0,
        sw.Elapsed);
  }

  public IReadOnlyList<string> GetAvailableCommands() => [_containerConfig.Shell];

  public async ValueTask DisposeAsync()
  {
    if (_shellSession is not null)
    {
      await _shellSession.DisposeAsync();
      _shellSession = null;
    }

    if (_containerName is not null)
    {
      try
      {
        await _dockerCli.RunAsync($"stop {_containerName}", timeout: TimeSpan.FromSeconds(10));
      }
      catch (Exception ex)
      {
        LogStopWarning(_containerName, ex);
      }
      _containerName = null;
    }
  }

  private string BuildRunArguments(string containerName, IReadOnlyList<string> volumeArgs)
  {
    var args = new List<string>
        {
            "run -d --rm",
            $"--name {containerName}",
        };

    if (!_containerConfig.Network)
    {
      args.Add("--network none");
    }

    args.AddRange(volumeArgs);

    foreach (var (key, value) in _containerConfig.Environment)
    {
      args.Add($"-e {key}={value}");
    }

    args.Add(_containerConfig.Image);
    args.Add("sleep infinity");

    return string.Join(" ", args);
  }

  private async Task CleanStaleContainersAsync(CancellationToken ct)
  {
    try
    {
      var listResult = await _dockerCli.RunAsync(
          "ps -a --filter name=boydcode- --format {{.Names}}", ct: ct);

      if (!listResult.Succeeded || string.IsNullOrWhiteSpace(listResult.StandardOutput))
      {
        return;
      }

      foreach (var name in listResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
      {
        LogCleaningStaleContainer(name);
        await _dockerCli.RunAsync($"rm -f {name}", ct: ct);
      }
    }
    catch (Exception ex)
    {
      LogStaleCleanupFailed(ex);
    }
  }

  [LoggerMessage(Level = LogLevel.Information, Message = "Docker available, server version: {Version}")]
  private partial void LogDockerAvailable(string version);

  [LoggerMessage(Level = LogLevel.Information, Message = "Container started: {ContainerName} ({Image})")]
  private partial void LogContainerStarted(string containerName, string image);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Removing stale container: {ContainerName}")]
  private partial void LogCleaningStaleContainer(string containerName);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to clean stale containers")]
  private partial void LogStaleCleanupFailed(Exception exception);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to stop container: {ContainerName}")]
  private partial void LogStopWarning(string containerName, Exception exception);
}
