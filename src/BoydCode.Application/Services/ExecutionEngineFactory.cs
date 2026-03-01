using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BoydCode.Application.Services;

public sealed partial class ExecutionEngineFactory : IExecutionEngineFactory
{
  private readonly IServiceProvider _services;
  private readonly ILogger<ExecutionEngineFactory> _logger;

  public ExecutionEngineFactory(IServiceProvider services, ILogger<ExecutionEngineFactory> logger)
  {
    _services = services;
    _logger = logger;
  }

  public async Task<IExecutionEngine> CreateAsync(
      ExecutionConfig config,
      IReadOnlyList<ResolvedDirectory> directories,
      string projectName,
      CancellationToken ct = default)
  {
    var creator = _services.GetKeyedService<ExecutionEngineCreator>(config.Mode);

    if (creator is null && config.Mode == ExecutionMode.Container)
    {
      if (config.AllowInProcess)
      {
        LogContainerFallback();
        creator = _services.GetKeyedService<ExecutionEngineCreator>(ExecutionMode.InProcess);
      }

      if (creator is null)
      {
        throw new InvalidOperationException(
            "Container execution mode is required but the container engine is not available. " +
            "Set AllowInProcess to true to fall back to in-process mode.");
      }
    }

    if (creator is null)
    {
      throw new InvalidOperationException($"No execution engine registered for mode: {config.Mode}");
    }

    var engine = await creator(config, directories, projectName, ct);
    await engine.InitializeAsync(ct);
    var commandCount = engine.GetAvailableCommands().Count;
    var modeName = config.Mode.ToString();
    LogEngineCreated(modeName, commandCount);
    return engine;
  }

  [LoggerMessage(Level = LogLevel.Warning, Message = "Container mode requested but not available; falling back to in-process execution")]
  private partial void LogContainerFallback();

  [LoggerMessage(Level = LogLevel.Information, Message = "Created execution engine: mode={Mode}, commands={CommandCount}")]
  private partial void LogEngineCreated(string mode, int commandCount);
}
