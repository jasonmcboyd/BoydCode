using BoydCode.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BoydCode.Infrastructure.LLM;

/// <summary>
/// Registers LLM infrastructure services into the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Adds the BoydCode LLM infrastructure services, including the <see cref="ILlmProviderFactory"/>.
  /// </summary>
  public static IServiceCollection AddBoydCodeLlm(this IServiceCollection services)
  {
    services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();
    return services;
  }
}
