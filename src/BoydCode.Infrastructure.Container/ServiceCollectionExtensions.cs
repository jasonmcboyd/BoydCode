using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Container;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddBoydCodeContainer(this IServiceCollection services)
  {
    services.AddSingleton<DockerCli>();
    services.AddKeyedSingleton<ExecutionEngineCreator>(
        ExecutionMode.Container,
        (sp, _) => (config, dirs, projectName, ct) =>
        {
          var dockerCli = sp.GetRequiredService<DockerCli>();
          var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
          IExecutionEngine engine = new ContainerExecutionEngine(
                  config.Container
                      ?? throw new InvalidOperationException("ContainerConfig is required for container mode."),
                  dirs,
                  projectName,
                  dockerCli,
                  loggerFactory);
          return Task.FromResult(engine);
        });
    return services;
  }
}
