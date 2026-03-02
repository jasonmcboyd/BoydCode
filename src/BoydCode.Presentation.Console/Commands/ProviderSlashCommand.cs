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
        SpectreHelpers.Usage("/provider list|setup|show|remove");
        break;
    }

    return true;
  }

  private async Task HandleListAsync(CancellationToken ct)
  {
    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup($"{"Provider",-14}{"Status",-10}{"Model",-30}{"API Key"}");
    SpectreHelpers.OutputMarkup(new string('\u2500', 70));

    var allProfiles = await _providerConfigStore.GetAllAsync(ct);
    var profileLookup = allProfiles.ToDictionary(p => p.ProviderType);

    foreach (var providerType in Enum.GetValues<LlmProviderType>())
    {
      profileLookup.TryGetValue(providerType, out var profile);

      var isActive = _activeProvider.IsConfigured
          && _activeProvider.Config!.ProviderType == providerType;

      var status = isActive ? "active" : profile?.ApiKey is not null ? "ready" : "--";

      var model = profile?.DefaultModel
          ?? ProviderDefaults.DefaultModelFor(providerType);

      var apiKeyDisplay = profile?.ApiKey is { Length: > 0 } key
          ? MaskApiKey(key)
          : "(not set)";

      SpectreHelpers.OutputMarkup(
        $"{Markup.Escape(providerType.ToString()),-14}" +
        $"{Markup.Escape(status),-10}" +
        $"{Markup.Escape(model),-30}" +
        $"{Markup.Escape(apiKeyDisplay)}");
    }

    SpectreHelpers.OutputLine();
  }

  private async Task HandleSetupAsync(string[] tokens, CancellationToken ct)
  {
    if (!_ui.IsInteractive && tokens.Length <= 2)
    {
      SpectreHelpers.Usage("/provider setup <name>");
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

      var selected = SpectreHelpers.Select("Select a provider:", providerNames);

      providerType = Enum.Parse<LlmProviderType>(selected);
    }

    if (!_ui.IsInteractive)
    {
      SpectreHelpers.Error("/provider setup requires an interactive terminal. Use --api-key instead.");
      return;
    }

    var isOllama = providerType == LlmProviderType.Ollama;

    var apiKey = SpectreHelpers.PromptSecret("API key:", allowEmpty: isOllama);
    if (string.IsNullOrWhiteSpace(apiKey))
    {
      apiKey = null;
    }

    var defaultModel = ProviderDefaults.DefaultModelFor(providerType);
    var model = SpectreHelpers.PromptWithDefault("Model:", defaultModel);

    var profile = new ProviderProfile(providerType, apiKey, model);
    await _providerConfigStore.SaveAsync(profile, ct);

    var config = BuildConfigFromProfile(profile);
    _activeProvider.Activate(config);
    var existingParts = (_ui.StatusLine ?? "").Split(" | ");
    var suffix = existingParts.Length > 2 ? " | " + string.Join(" | ", existingParts.Skip(2)) : "";
    _ui.StatusLine = $"{config.ProviderType} | {config.Model}{suffix}";

    await _providerConfigStore.SetLastUsedProviderAsync(providerType, ct);

    SpectreHelpers.Success($"Provider '{providerType}' configured and activated.");
  }

  private void HandleShow()
  {
    if (!_activeProvider.IsConfigured)
    {
      SpectreHelpers.Warning("No provider is currently active. Use /provider setup to configure one.");
      return;
    }

    var config = _activeProvider.Config!;
    var capabilities = _activeProvider.Provider!.Capabilities;

    var content =
      $"Provider:       {config.ProviderType}\n" +
      $"Model:          {config.Model}\n" +
      $"Context window: {capabilities.MaxContextWindowTokens.ToString("N0", CultureInfo.InvariantCulture)} tokens";

    _ui.ShowModal("Active Provider", content);
  }

  private async Task HandleRemoveAsync(string[] tokens, CancellationToken ct)
  {
    if (!_ui.IsInteractive && tokens.Length <= 2)
    {
      SpectreHelpers.Usage("/provider remove <name>");
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

      var selected = SpectreHelpers.Select("Select a provider to remove:", providerNames);

      providerType = Enum.Parse<LlmProviderType>(selected);
    }

    await _providerConfigStore.RemoveAsync(providerType, ct);

    if (_activeProvider.IsConfigured
        && _activeProvider.Config!.ProviderType == providerType)
    {
      SpectreHelpers.Warning($"'{providerType}' is the active provider. It will remain active for this session but won't persist.");
    }

    SpectreHelpers.Success($"Provider '{providerType}' removed.");
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
      return key;
    }

    return key[..4] + "****";
  }
}
