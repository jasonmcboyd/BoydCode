using Spectre.Console.Cli;

namespace BoydCode.Presentation.Console;

public sealed class TypeRegistrar : ITypeRegistrar
{
  private readonly IServiceProvider _provider;

  public TypeRegistrar(IServiceProvider provider)
  {
    _provider = provider;
  }

  public ITypeResolver Build() => new TypeResolver(_provider);

  public void Register(Type service, Type implementation)
  {
    // Spectre.Console.Cli registers its own internal types.
    // We rely on the pre-configured DI container, so this is a no-op.
  }

  public void RegisterInstance(Type service, object implementation)
  {
    // No-op -- our container is already built.
  }

  public void RegisterLazy(Type service, Func<object> factory)
  {
    // No-op -- our container is already built.
  }
}

public sealed class TypeResolver : ITypeResolver
{
  private readonly IServiceProvider _provider;

  public TypeResolver(IServiceProvider provider)
  {
    _provider = provider;
  }

  public object? Resolve(Type? type)
  {
    return type is null ? null : _provider.GetService(type);
  }
}
