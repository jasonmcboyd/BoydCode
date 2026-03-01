using BoydCode.Application.Interfaces;
using BoydCode.Domain.Enums;

namespace BoydCode.Application.Services;

public sealed class ActiveExecutionEngine : IAsyncDisposable
{
  public IExecutionEngine? Engine { get; private set; }
  public ExecutionMode Mode { get; private set; }
  public bool IsInitialized => Engine is not null;

  public async Task SetAsync(IExecutionEngine engine, ExecutionMode mode)
  {
    if (Engine is not null)
    {
      await Engine.DisposeAsync();
    }

    Engine = engine;
    Mode = mode;
  }

  public async ValueTask DisposeAsync()
  {
    if (Engine is not null)
    {
      await Engine.DisposeAsync();
      Engine = null;
    }
  }
}
