using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Container;

internal sealed partial class PersistentShellSession : IAsyncDisposable
{
  private readonly Process _process;
  private readonly StreamWriter _stdin;
  private readonly ShellDialect _dialect;
  private readonly ILogger _logger;
  private readonly SemaphoreSlim _executionLock = new(1, 1);
  private readonly ConcurrentQueue<string> _stderrLines = new();
  private readonly Task _stderrReaderTask;
  private volatile bool _disposed;

  internal PersistentShellSession(Process process, ShellDialect dialect, ILogger logger)
  {
    _process = process;
    _stdin = process.StandardInput;
    _stdin.NewLine = "\n";
    _stdin.AutoFlush = true;
    _dialect = dialect;
    _logger = logger;

    // Start background stderr reader
    _stderrReaderTask = Task.Run(ReadStderrAsync);
  }

  internal async Task<ShellCommandResult> ExecuteAsync(
      string command,
      Action<string>? onOutputLine = null,
      CancellationToken ct = default)
  {
    if (_process.HasExited)
    {
      throw new InvalidOperationException("Container shell process has exited unexpectedly.");
    }

    await _executionLock.WaitAsync(ct).ConfigureAwait(false);
    try
    {
      var marker = Guid.NewGuid().ToString("N");
      var wrappedCommand = _dialect.WrapWithSentinel(command, marker);
      var startPattern = ShellDialect.BuildStartPattern(marker);
      var exitPattern = ShellDialect.BuildExitPattern(marker);

      // Drain any accumulated stderr
      DrainStderr();

      LogCommandExecution(command);
      await _stdin.WriteLineAsync(wrappedCommand).ConfigureAwait(false);

      // Read stdout: skip until start sentinel, capture until exit sentinel
      var outputLines = new List<string>();
      var startSeen = false;
      try
      {
        while (!ct.IsCancellationRequested)
        {
          var line = await _process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false);
          if (line is null)
          {
            break; // EOF -- process exited
          }

          if (!startSeen)
          {
            if (ShellDialect.IsStartSentinel(line, startPattern))
            {
              startSeen = true;
            }
            continue;
          }

          if (ShellDialect.IsExitSentinel(line, exitPattern))
          {
            var exitCode = ShellDialect.ParseExitCode(line, exitPattern);
            var stderr = DrainStderr();
            var output = string.Join("\n", outputLines);
            return new ShellCommandResult(output, stderr, exitCode);
          }

          outputLines.Add(line);
          onOutputLine?.Invoke(line);
        }
      }
      catch (OperationCanceledException)
      {
        try { await _stdin.WriteAsync("\u0003"); await _stdin.FlushAsync(CancellationToken.None); } catch { }
        var partialOutput = string.Join("\n", outputLines);
        var stderr = DrainStderr();
        return new ShellCommandResult(
            partialOutput,
            $"Command cancelled. {stderr}".Trim(),
            1);
      }

      // If we get here, the process ended without a sentinel
      var finalOutput = string.Join("\n", outputLines);
      var finalStderr = DrainStderr();
      return new ShellCommandResult(finalOutput, finalStderr, 1);
    }
    finally
    {
      _executionLock.Release();
    }
  }

  private async Task ReadStderrAsync()
  {
    try
    {
      while (!_disposed && !_process.HasExited)
      {
        var line = await _process.StandardError.ReadLineAsync().ConfigureAwait(false);
        if (line is null) break;
        _stderrLines.Enqueue(line);
      }
    }
    catch (ObjectDisposedException)
    {
      // Expected during shutdown
    }
  }

  private string? DrainStderr()
  {
    if (_stderrLines.IsEmpty) return null;
    var sb = new StringBuilder();
    while (_stderrLines.TryDequeue(out var line))
    {
      if (sb.Length > 0) sb.Append('\n');
      sb.Append(line);
    }
    return sb.Length > 0 ? sb.ToString() : null;
  }

  public async ValueTask DisposeAsync()
  {
    if (_disposed) return;
    _disposed = true;

    try
    {
      _stdin.Close();
    }
    catch (Exception ex)
    {
      LogDisposeWarning(ex);
    }

    try
    {
      await _stderrReaderTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }
    catch
    {
      // Best effort
    }

    _executionLock.Dispose();
    _process.Dispose();
  }

  [LoggerMessage(Level = LogLevel.Debug, Message = "Executing in container: {Command}")]
  private partial void LogCommandExecution(string command);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Warning during shell session dispose")]
  private partial void LogDisposeWarning(Exception exception);
}
