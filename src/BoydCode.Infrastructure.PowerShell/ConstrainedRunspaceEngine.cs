using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoydCode.Infrastructure.PowerShell;

public sealed partial class ConstrainedRunspaceEngine : IExecutionEngine
{
  private readonly ExecutionConfig _config;
  private readonly JeaProfileComposer _composer;
  private readonly ILogger<ConstrainedRunspaceEngine> _logger;
  private Runspace? _runspace;
  private readonly List<string> _availableCommands = [];

  public ConstrainedRunspaceEngine(
      IOptions<ExecutionConfig> config,
      JeaProfileComposer composer,
      ILogger<ConstrainedRunspaceEngine> logger)
  {
    _config = config.Value;
    _composer = composer;
    _logger = logger;
  }

  public async Task InitializeAsync(CancellationToken ct = default)
  {
    var effective = await _composer.ComposeAsync(_config.JeaProfiles, ct).ConfigureAwait(false);

    var iss = InitialSessionState.Create();

    var commands = effective.AllowedCommands.ToList();

    // Add cmdlets from the default session state that match our whitelist
    var defaultIss = InitialSessionState.CreateDefault();
    foreach (var cmd in defaultIss.Commands)
    {
      if (commands.Contains(cmd.Name, StringComparer.OrdinalIgnoreCase))
      {
        iss.Commands.Add(cmd);
      }
    }

    // Add applications (exe) like dotnet, git
    foreach (var appName in commands.Where(c => !c.Contains('-')))
    {
      iss.Commands.Add(new SessionStateApplicationEntry(appName));
    }

    // Add core providers (FileSystem, Variable, Environment, etc.) so cmdlets can access paths and drives
    foreach (var provider in defaultIss.Providers)
    {
      iss.Providers.Add(provider);
    }

    // Import modules specified by profiles
    foreach (var module in effective.Modules)
    {
      iss.ImportPSModule(module);
    }

    iss.LanguageMode = MapLanguageMode(effective.LanguageMode);

    _runspace = RunspaceFactory.CreateRunspace(iss);
    _runspace.Open();

    // Enumerate available commands
    using var ps = System.Management.Automation.PowerShell.Create();
    ps.Runspace = _runspace;
    ps.AddCommand("Get-Command");

    try
    {
      var results = ps.Invoke();
      foreach (var result in results)
      {
        if (result.Properties["Name"]?.Value is string name)
        {
          _availableCommands.Add(name);
        }
      }
    }
    catch (Exception ex)
    {
      // In constrained mode, Get-Command might not be available
      LogCommandEnumerationFailed(ex);
      _availableCommands.AddRange(commands);
    }

    var languageModeName = iss.LanguageMode.ToString();
    LogEngineInitialized(_availableCommands.Count, languageModeName);
  }

  public async Task<ShellExecutionResult> ExecuteAsync(
      string command,
      string workingDirectory,
      Action<string>? onOutputLine = null,
      CancellationToken ct = default)
  {
    if (_runspace is null)
    {
      throw new InvalidOperationException("Engine not initialized. Call InitializeAsync first.");
    }

    var sw = Stopwatch.StartNew();

    using var ps = System.Management.Automation.PowerShell.Create();
    ps.Runspace = _runspace;

    // Set working directory
    ps.AddScript($"Set-Location -LiteralPath '{workingDirectory.Replace("'", "''")}'");
    ps.Invoke();
    if (ps.HadErrors)
    {
      var error = string.Join(Environment.NewLine, ps.Streams.Error.Select(e => e.ToString()));
      sw.Stop();
      return new ShellExecutionResult(string.Empty, $"Failed to set working directory: {error}", HadErrors: true, sw.Elapsed);
    }
    ps.Commands.Clear();
    ps.Streams.ClearStreams();

    // Execute the actual command
    ps.AddScript(command);

    try
    {
      using var reg = ct.Register(() => ps.Stop());
      var results = await Task.Run(() => ps.Invoke(), ct).ConfigureAwait(false);
      sw.Stop();

      // Batch-replay output lines through callback
      if (onOutputLine is not null)
      {
        foreach (var r in results)
        {
          var line = r?.ToString() ?? string.Empty;
          if (!string.IsNullOrEmpty(line))
          {
            onOutputLine(line);
          }
        }
      }

      var output = string.Join(Environment.NewLine, results.Select(r => r?.ToString() ?? string.Empty));
      var errorOutput = ps.HadErrors
          ? string.Join(Environment.NewLine, ps.Streams.Error.Select(e => e.ToString()))
          : null;

      return new ShellExecutionResult(output, errorOutput, ps.HadErrors, sw.Elapsed);
    }
    catch (OperationCanceledException)
    {
      ps.Stop();
      sw.Stop();
      return new ShellExecutionResult(string.Empty, "Command cancelled.", HadErrors: true, sw.Elapsed);
    }
    catch (Exception ex)
    {
      sw.Stop();
      return new ShellExecutionResult(string.Empty, ex.Message, HadErrors: true, sw.Elapsed);
    }
  }

  public IReadOnlyList<string> GetAvailableCommands() => _availableCommands.AsReadOnly();

  public IReadOnlyDictionary<string, string> PathMappings { get; } = new Dictionary<string, string>();

  public ValueTask DisposeAsync()
  {
    _runspace?.Close();
    _runspace?.Dispose();
    _runspace = null;
    return ValueTask.CompletedTask;
  }

  private static PSLanguageMode MapLanguageMode(PSLanguageModeName name) =>
      name switch
      {
        PSLanguageModeName.FullLanguage => PSLanguageMode.FullLanguage,
        PSLanguageModeName.ConstrainedLanguage => PSLanguageMode.ConstrainedLanguage,
        PSLanguageModeName.RestrictedLanguage => PSLanguageMode.RestrictedLanguage,
        PSLanguageModeName.NoLanguage => PSLanguageMode.NoLanguage,
        _ => PSLanguageMode.ConstrainedLanguage,
      };

  [LoggerMessage(Level = LogLevel.Information, Message = "Constrained runspace initialized with {CommandCount} commands, language mode: {LanguageMode}")]
  private partial void LogEngineInitialized(int commandCount, string languageMode);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Could not enumerate commands; using configured list")]
  private partial void LogCommandEnumerationFailed(Exception exception);
}
