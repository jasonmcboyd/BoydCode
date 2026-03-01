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
  private bool _disposed;

  internal PersistentShellSession(Process process, ShellDialect dialect, ILogger logger)
  {
    _process = process;
    _stdin = process.StandardInput;
    _stdin.AutoFlush = true;
    _dialect = dialect;
    _logger = logger;

    // Start background stderr reader
    _stderrReaderTask = Task.Run(ReadStderrAsync);
  }

  internal async Task<ShellCommandResult> ExecuteAsync(
      string command,
      int timeoutMs,
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

      // Drain any accumulated stderr
      DrainStderr();

      LogCommandExecution(command);
      await _stdin.WriteLineAsync(wrappedCommand).ConfigureAwait(false);

      // Read stdout until sentinel
      var outputLines = new List<string>();
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      cts.CancelAfter(timeoutMs);

      try
      {
        while (!cts.Token.IsCancellationRequested)
        {
          var line = await _process.StandardOutput.ReadLineAsync(cts.Token).ConfigureAwait(false);
          if (line is null)
          {
            break; // EOF -- process exited
          }

          if (ShellDialect.IsSentinel(line, marker))
          {
            var exitCode = ShellDialect.ParseExitCode(line, marker);
            var stderr = DrainStderr();
            var output = string.Join("\n", outputLines);
            return new ShellCommandResult(output, stderr, exitCode);
          }

          outputLines.Add(line);
        }
      }
      catch (OperationCanceledException)
      {
        var partialOutput = string.Join("\n", outputLines);
        var stderr = DrainStderr();
        return new ShellCommandResult(
            partialOutput,
            $"Command timed out after {timeoutMs}ms. {stderr}".Trim(),
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
