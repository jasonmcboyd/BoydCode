using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;

namespace BoydCode.Application.Services;

public sealed class ActiveProvider : IDisposable
{
  private readonly ILlmProviderFactory _factory;

  public ILlmProvider? Provider { get; private set; }
  public LlmProviderConfig? Config { get; private set; }
  public bool IsConfigured => Provider is not null;

  public ActiveProvider(ILlmProviderFactory factory)
  {
    _factory = factory;
  }

  public void Activate(LlmProviderConfig config)
  {
    if (Provider is IDisposable disposable)
    {
      disposable.Dispose();
    }

    Config = config;
    Provider = _factory.Create(config);
  }

  public void Dispose()
  {
    if (Provider is IDisposable disposable)
    {
      disposable.Dispose();
    }
  }
}
