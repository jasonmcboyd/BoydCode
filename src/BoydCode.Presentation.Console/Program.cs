using BoydCode.Application;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Enums;
using BoydCode.Infrastructure.Container;
using BoydCode.Infrastructure.LLM;
using BoydCode.Infrastructure.Persistence;
using BoydCode.Infrastructure.PowerShell;
using BoydCode.Presentation.Console;
using BoydCode.Presentation.Console.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

// Ensure UTF-8 output encoding so Spectre.Console detects Unicode capability correctly.
// Windows terminals (Windows Terminal, ConPTY) render Unicode fine, but .NET defaults to
// the system code page which causes Spectre to set Capabilities.Unicode = false.
System.Console.OutputEncoding = System.Text.Encoding.UTF8;

// Apply accessibility settings (NO_COLOR, etc.) before any Spectre output.
AccessibilityConfig.Apply();

// Wire up global exception handlers before anything else.
// These catch exceptions that escape all other handlers (background tasks, finalizers, etc.).
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
  if (e.ExceptionObject is Exception ex)
  {
    CrashLogger.LogException(ex);
    RenderCrashMessage(ex);
  }
};

TaskScheduler.UnobservedTaskException += (_, e) =>
{
  CrashLogger.LogException(e.Exception);
  e.SetObserved();
};

IHost? host = null;

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
  try
  {
    // Dispose UI first (tears down layout, stops input reader)
    var ui = host?.Services.GetService<IUserInterface>();
    if (ui is IDisposable disposableUi)
    {
      disposableUi.Dispose();
    }

    var activeEngine = host?.Services.GetService<ActiveExecutionEngine>();
    activeEngine?.DisposeAsync().AsTask().GetAwaiter().GetResult();

    var logger = host?.Services.GetService<IConversationLogger>();
    if (logger is IAsyncDisposable disposable)
    {
      disposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
  }
  catch
  {
    // Best effort cleanup during process exit
  }
};

try
{
  // Build host for DI
  var hostBuilder = Host.CreateApplicationBuilder(args);

  // Suppress default console logging (info: lines clutter the splash screen)
  hostBuilder.Logging.SetMinimumLevel(LogLevel.Warning);

  // Bind configuration sections
  hostBuilder.Services.Configure<BoydCode.Domain.Configuration.AppSettings>(
      hostBuilder.Configuration.GetSection("BoydCode"));
  hostBuilder.Services.Configure<BoydCode.Domain.Configuration.LlmProviderConfig>(
      hostBuilder.Configuration.GetSection("BoydCode:Llm"));
  hostBuilder.Services.Configure<BoydCode.Domain.Configuration.ExecutionConfig>(
      hostBuilder.Configuration.GetSection("BoydCode:Execution"));

  // Register all services
  hostBuilder.Services.AddBoydCodeApplication();
  hostBuilder.Services.AddBoydCodeLlm();
  hostBuilder.Services.AddBoydCodePowerShell();
  hostBuilder.Services.AddBoydCodeContainer();
  hostBuilder.Services.AddBoydCodePersistence();

  // Register UI
  hostBuilder.Services.AddSingleton<IUserInterface, SpectreUserInterface>();

  // Register commands so Spectre.Console.Cli can resolve them
  hostBuilder.Services.AddTransient<ChatCommand>();
  hostBuilder.Services.AddTransient<LoginCommand>();
  hostBuilder.Services.AddTransient<ISlashCommand, ProjectSlashCommand>();
  hostBuilder.Services.AddTransient<ISlashCommand, HelpSlashCommand>();
  hostBuilder.Services.AddTransient<ISlashCommand, ProviderSlashCommand>();
  hostBuilder.Services.AddTransient<ISlashCommand, JeaSlashCommand>();
  hostBuilder.Services.AddTransient<ISlashCommand, ContextSlashCommand>();
  hostBuilder.Services.AddTransient<ISlashCommand, ConversationsSlashCommand>();
  hostBuilder.Services.AddTransient<ISlashCommand, ExpandSlashCommand>();
  hostBuilder.Services.AddTransient<ISlashCommand, AgentSlashCommand>();

  host = hostBuilder.Build();

  // Initialize slash commands in registry
  var slashCommandRegistry = host.Services.GetRequiredService<ISlashCommandRegistry>();
  foreach (var command in host.Services.GetServices<ISlashCommand>())
  {
    slashCommandRegistry.Register(command);
  }

  // Build Spectre CLI app using the DI container
  var app = new CommandApp(new TypeRegistrar(host.Services));
  app.Configure(config =>
  {
    config.SetApplicationName("boydcode");
    config.AddCommand<ChatCommand>("chat").WithDescription("Start an interactive chat session");
    config.AddCommand<LoginCommand>("login").WithDescription("Log in with your LLM provider subscription (Anthropic, Gemini)");
  });

  // Make chat the default command
  app.SetDefaultCommand<ChatCommand>();

  return await app.RunAsync(args);
}
catch (Exception ex)
{
  CrashLogger.LogException(ex);
  RenderCrashMessage(ex);
  return (int)ExitCode.GeneralError;
}

static void RenderCrashMessage(Exception ex)
{
  try
  {
    AnsiConsole.WriteLine();
    AnsiConsole.Write(
        new Panel(
            new Markup(
                $"[red bold]An unexpected error occurred.[/]\n\n" +
                $"[red]{Markup.Escape(ex.Message)}[/]\n\n" +
                $"[dim]Details have been written to:[/]\n" +
                $"[cyan]{Markup.Escape(CrashLogger.LogFilePath)}[/]"))
        .Header("[red bold] boydcode crash [/]")
        .BorderColor(Color.Red)
        .Padding(1, 1, 1, 1));
    AnsiConsole.WriteLine();
  }
  catch
  {
    // Last resort: plain console output in case Spectre itself is broken
    System.Console.Error.WriteLine($"Fatal error: {ex.Message}");
    System.Console.Error.WriteLine($"Error log: {CrashLogger.LogFilePath}");
  }
}
