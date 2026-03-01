using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BoydCode.Infrastructure.PowerShell;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddBoydCodePowerShell(this IServiceCollection services)
  {
    services.AddKeyedSingleton<ExecutionEngineCreator>(
        ExecutionMode.InProcess,
        (sp, _) => (config, dirs, projectName, ct) =>
        {
          var options = Options.Create(config);
          var engine = ActivatorUtilities.CreateInstance<ConstrainedRunspaceEngine>(sp, options);
          return Task.FromResult<IExecutionEngine>(engine);
        });
    return services;
  }
}
