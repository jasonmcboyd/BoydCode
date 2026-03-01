using System.Globalization;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using BoydCode.Domain.SlashCommands;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace BoydCode.Presentation.Console.Commands;

public sealed class ProviderSlashCommand : ISlashCommand
{
  private readonly IProviderConfigStore _providerConfigStore;
  private readonly ActiveProvider _activeProvider;
  private readonly IOptions<AppSettings> _appSettings;
  private readonly IUserInterface _ui;

  public ProviderSlashCommand(
      IProviderConfigStore providerConfigStore,
      ActiveProvider activeProvider,
      IOptions<AppSettings> appSettings,
      IUserInterface ui)
  {
    _providerConfigStore = providerConfigStore;
    _activeProvider = activeProvider;
    _appSettings = appSettings;
    _ui = ui;
  }

  public SlashCommandDescriptor Descriptor { get; } = new(
      "/provider",
      "Manage LLM providers",
      [
          new("list", "List all providers and their status"),
            new("setup [name]", "Configure a provider (API key, model)"),
            new("show", "Show active provider details"),
            new("remove [name]", "Remove a provider configuration"),
      ]);

  public async Task<bool> TryHandleAsync(string input, CancellationToken ct = default)
  {
    var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (tokens.Length == 0 || !tokens[0].Equals("/provider", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    var subcommand = tokens.Length > 1 ? tokens[1].ToLowerInvariant() : string.Empty;

    switch (subcommand)
    {
      case "setup":
        await HandleSetupAsync(tokens, ct);
        break;
      case "show":
        HandleShow();
        break;
      case "remove":
        await HandleRemoveAsync(tokens, ct);
        break;
      case "list":
      case "":
        await HandleListAsync(ct);
        break;
      default:
        AnsiConsole.MarkupLine("[yellow]Usage: /provider list|setup|show|remove[/]");
        break;
    }

    return true;
  }

  private async Task HandleListAsync(CancellationToken ct)
  {
    var table = new Table();
    table.AddColumn("Provider");
    table.AddColumn("Status");
    table.AddColumn("Model");
    table.AddColumn("API Key");

    var allProfiles = await _providerConfigStore.GetAllAsync(ct);
    var profileLookup = allProfiles.ToDictionary(p => p.ProviderType);

    foreach (var providerType in Enum.GetValues<LlmProviderType>())
    {
      profileLookup.TryGetValue(providerType, out var profile);

      var isActive = _activeProvider.IsConfigured
          && _activeProvider.Config!.ProviderType == providerType;

      var status = isActive
          ? "[green bold]active[/]"
          : profile?.ApiKey is not null
              ? "[dim]ready[/]"
              : "";

      var model = profile?.DefaultModel
          ?? ProviderDefaults.DefaultModelFor(providerType);

      var apiKeyDisplay = profile?.ApiKey is { Length: > 0 } key
          ? MaskApiKey(key)
          : "[dim]not set[/]";

      table.AddRow(
          Markup.Escape(providerType.ToString()),
          status,
          Markup.Escape(model),
          apiKeyDisplay);
    }

    AnsiConsole.Write(table);
  }

  private async Task HandleSetupAsync(string[] tokens, CancellationToken ct)
  {
    if (!_ui.IsInteractive && tokens.Length <= 2)
    {
      AnsiConsole.MarkupLine("[red]Usage:[/] /provider setup <name>");
      return;
    }

    LlmProviderType providerType;

    if (tokens.Length > 2
        && Enum.TryParse<LlmProviderType>(tokens[2], ignoreCase: true, out var parsed))
    {
      providerType = parsed;
    }
    else
    {
      var providerNames = Enum.GetValues<LlmProviderType>()
          .Select(p => p.ToString())
          .ToList();

      var selected = AnsiConsole.Prompt(
          new SelectionPrompt<string>()
              .Title("Select a provider:")
              .AddChoices(providerNames));

      providerType = Enum.Parse<LlmProviderType>(selected);
    }

    if (!_ui.IsInteractive)
    {
      AnsiConsole.MarkupLine("[red]Error:[/] /provider setup requires an interactive terminal. Use --api-key instead.");
      return;
    }

    var isOllama = providerType == LlmProviderType.Ollama;

    var apiKeyPrompt = new TextPrompt<string>("API key:")
        .Secret();

    if (isOllama)
    {
      apiKeyPrompt.AllowEmpty();
    }

    var apiKey = AnsiConsole.Prompt(apiKeyPrompt);
    if (string.IsNullOrWhiteSpace(apiKey))
    {
      apiKey = null;
    }

    var defaultModel = ProviderDefaults.DefaultModelFor(providerType);
    var model = AnsiConsole.Prompt(
        new TextPrompt<string>("Model:")
            .DefaultValue(defaultModel)
            .ShowDefaultValue());

    var profile = new ProviderProfile(providerType, apiKey, model);
    await _providerConfigStore.SaveAsync(profile, ct);

    var config = BuildConfigFromProfile(profile);
    _activeProvider.Activate(config);
    var existingParts = (_ui.StatusLine ?? "").Split(" | ");
    var suffix = existingParts.Length > 2 ? " | " + string.Join(" | ", existingParts.Skip(2)) : "";
    _ui.StatusLine = $"{config.ProviderType} | {config.Model}{suffix}";

    await _providerConfigStore.SetLastUsedProviderAsync(providerType, ct);

    AnsiConsole.MarkupLine(
        $"[green]Provider '{Markup.Escape(providerType.ToString())}' configured and activated.[/]");
  }

  private void HandleShow()
  {
    if (!_activeProvider.IsConfigured)
    {
      AnsiConsole.MarkupLine("[yellow]No provider is currently active. Use /provider setup to configure one.[/]");
      return;
    }

    var config = _activeProvider.Config!;
    var capabilities = _activeProvider.Provider!.Capabilities;

    var lines = new List<string>
        {
            $"[bold]Provider:[/]       {Markup.Escape(config.ProviderType.ToString())}",
            $"[bold]Model:[/]          {Markup.Escape(config.Model)}",
            $"[bold]Context window:[/] {capabilities.MaxContextWindowTokens.ToString("N0", CultureInfo.InvariantCulture)} tokens",
        };

    var panel = new Panel(new Markup(string.Join("\n", lines)))
        .Header("[bold]Active Provider[/]")
        .Border(BoxBorder.Rounded)
        .Padding(1, 0, 1, 0);

    AnsiConsole.Write(panel);
  }

  private async Task HandleRemoveAsync(string[] tokens, CancellationToken ct)
  {
    if (!_ui.IsInteractive && tokens.Length <= 2)
    {
      AnsiConsole.MarkupLine("[red]Usage:[/] /provider remove <name>");
      return;
    }

    LlmProviderType providerType;

    if (tokens.Length > 2
        && Enum.TryParse<LlmProviderType>(tokens[2], ignoreCase: true, out var parsed))
    {
      providerType = parsed;
    }
    else
    {
      var providerNames = Enum.GetValues<LlmProviderType>()
          .Select(p => p.ToString())
          .ToList();

      var selected = AnsiConsole.Prompt(
          new SelectionPrompt<string>()
              .Title("Select a provider to remove:")
              .AddChoices(providerNames));

      providerType = Enum.Parse<LlmProviderType>(selected);
    }

    await _providerConfigStore.RemoveAsync(providerType, ct);

    if (_activeProvider.IsConfigured
        && _activeProvider.Config!.ProviderType == providerType)
    {
      AnsiConsole.MarkupLine(
          $"[yellow]Warning: '{Markup.Escape(providerType.ToString())}' is the active provider. "
          + "It will remain active for this session but won't persist.[/]");
    }

    AnsiConsole.MarkupLine(
        $"[green]Provider '{Markup.Escape(providerType.ToString())}' removed.[/]");
  }

  private LlmProviderConfig BuildConfigFromProfile(ProviderProfile profile)
  {
    var defaults = _appSettings.Value.Llm;

    return new LlmProviderConfig
    {
      ProviderType = profile.ProviderType,
      ApiKey = profile.ApiKey,
      Model = profile.DefaultModel ?? ProviderDefaults.DefaultModelFor(profile.ProviderType),
      MaxTokens = defaults.MaxTokens,
      BaseUrl = defaults.BaseUrl,
    };
  }

  private static string MaskApiKey(string key)
  {
    if (key.Length <= 4)
    {
      return Markup.Escape(key);
    }

    return Markup.Escape(key[..4] + "****");
  }
}
