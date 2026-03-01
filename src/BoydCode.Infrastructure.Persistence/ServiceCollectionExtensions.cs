using BoydCode.Application.Interfaces;
using BoydCode.Infrastructure.Persistence.Auth;
using BoydCode.Infrastructure.Persistence.Jea;
using BoydCode.Infrastructure.Persistence.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace BoydCode.Infrastructure.Persistence;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddBoydCodePersistence(this IServiceCollection services)
  {
    services.AddSingleton<ISessionRepository, JsonSessionRepository>();
    services.AddSingleton<ISettingsProvider, FileSettingsProvider>();
    services.AddSingleton<IContextCompactor, NoOpContextCompactor>();
    services.AddSingleton<IHookEngine, NoOpHookEngine>();
    services.AddSingleton<IProjectRepository, JsonProjectRepository>();
    services.AddHttpClient("OAuth");
    services.AddSingleton<ICredentialStore, JsonCredentialStore>();
    services.AddSingleton<IOAuthClientConfigStore, JsonOAuthClientConfigStore>();
    services.AddSingleton<IProviderConfigStore, JsonProviderConfigStore>();
    services.AddSingleton<IJeaProfileStore, FileJeaProfileStore>();
    return services;
  }
}
