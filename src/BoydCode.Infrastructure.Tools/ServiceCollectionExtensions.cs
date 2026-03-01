using BoydCode.Application.Interfaces;
using BoydCode.Infrastructure.Tools.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace BoydCode.Infrastructure.Tools;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddBoydCodeTools(this IServiceCollection services)
  {
    services.AddSingleton<ITool, ReadTool>();
    services.AddSingleton<ITool, WriteTool>();
    services.AddSingleton<ITool, EditTool>();
    services.AddSingleton<ITool, GlobTool>();
    services.AddSingleton<ITool, GrepTool>();
    services.AddSingleton<ITool, PowerShellTool>();
    services.AddSingleton<ITool, WebFetchTool>();
    services.AddSingleton<ITool, WebSearchTool>();

    services.AddHttpClient("WebFetch");

    return services;
  }
}
