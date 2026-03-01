using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BoydCode.Application;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddBoydCodeApplication(this IServiceCollection services)
  {
    services.AddSingleton<IToolRegistry, ToolRegistry>();
    services.AddSingleton<ISlashCommandRegistry, SlashCommandRegistry>();
    services.AddSingleton<IPermissionEngine, PermissionEngine>();
    services.AddSingleton<DirectoryGuard>();
    services.AddSingleton<IDirectoryGuard>(sp => sp.GetRequiredService<DirectoryGuard>());
    services.AddTransient<DirectoryResolver>();
    services.AddSingleton<ActiveProvider>();
    services.AddSingleton<ActiveProject>();
    services.AddSingleton<ActiveExecutionEngine>();
    services.AddTransient<ProjectResolver>();
    services.AddSingleton<IExecutionEngineFactory, ExecutionEngineFactory>();
    services.AddTransient<AgentOrchestrator>();
    services.AddTransient<JeaProfileComposer>();
    return services;
  }
}
