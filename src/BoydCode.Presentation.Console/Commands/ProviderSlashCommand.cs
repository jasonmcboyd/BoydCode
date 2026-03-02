using System.Globalization;
using BoydCode.Application.Interfaces;
using BoydCode.Application.Services;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using BoydCode.Domain.SlashCommands;
using BoydCode.Presentation.Console.Terminal;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using TguiApp = Terminal.Gui.App.Application;

#pragma warning disable CS0618 // Application.Invoke/Run/RequestStop - using legacy static API during Terminal.Gui migration

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

  private sealed record ProviderListItem(
    LlmProviderType ProviderType, string Status, string Model, string ApiKeyDisplay);

  private async Task HandleListAsync(CancellationToken ct)
  {
    var items = await BuildProviderListItemsAsync(ct);

    var spectreUi = _ui as SpectreUserInterface;
    if (spectreUi?.Toplevel is not null)
    {
      ShowProviderListWindow(spectreUi, items, ct);
      return;
    }

    // Fallback: inline text output for non-interactive mode
    SpectreHelpers.OutputLine();
    SpectreHelpers.OutputMarkup($"{"Provider",-14}{"Status",-10}{"Model",-30}{"API Key"}");
    SpectreHelpers.OutputMarkup(new string('\u2500', 70));

    foreach (var item in items)
    {
      SpectreHelpers.OutputMarkup(
        $"{Markup.Escape(item.ProviderType.ToString()),-14}" +
        $"{Markup.Escape(item.Status),-10}" +
        $"{Markup.Escape(item.Model),-30}" +
        $"{Markup.Escape(item.ApiKeyDisplay)}");
    }

    SpectreHelpers.OutputLine();
  }

  private async Task<List<ProviderListItem>> BuildProviderListItemsAsync(CancellationToken ct)
  {
    var allProfiles = await _providerConfigStore.GetAllAsync(ct);
    var profileLookup = allProfiles.ToDictionary(p => p.ProviderType);
    var items = new List<ProviderListItem>();

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

      items.Add(new ProviderListItem(providerType, status, model, apiKeyDisplay));
    }

    return items;
  }

  private void ShowProviderListWindow(
    SpectreUserInterface spectreUi,
    List<ProviderListItem> items,
    CancellationToken ct)
  {
    InteractiveListWindow<ProviderListItem>? window = null;

    var actions = new List<ActionDefinition<ProviderListItem>>
    {
      new(
        Key.Enter, "Show",
        item => { if (item is not null) HandleShowForProvider(item.ProviderType); },
        IsPrimary: true),
      new(
        Key.S, "Setup",
        item =>
        {
          DismissListWindow(spectreUi, ref window);
          _ = HandleSetupAsync(["/provider", "setup", item?.ProviderType.ToString() ?? ""], ct);
        },
        HotkeyDisplay: "s"),
      new(
        Key.R, "Remove",
        item =>
        {
          DismissListWindow(spectreUi, ref window);
          _ = HandleRemoveAsync(["/provider", "remove", item?.ProviderType.ToString() ?? ""], ct);
        },
        HotkeyDisplay: "r"),
    };

    window = new InteractiveListWindow<ProviderListItem>(
      "Providers",
      items,
      FormatProviderListItem,
      actions,
      columnHeader: $"{"Provider",-14}{"Status",-10}{"Model",-30}{"API Key"}");

    window.CloseRequested += () => DismissListWindow(spectreUi, ref window);

    TguiApp.Invoke(() =>
    {
      spectreUi.Toplevel!.Add(window);
      window.SetFocus();
    });
  }

  private void HandleShowForProvider(LlmProviderType providerType)
  {
    if (_activeProvider.IsConfigured && _activeProvider.Config!.ProviderType == providerType)
    {
      HandleShow();
      return;
    }

    var content = $"Provider: {providerType}\n" +
        $"Status:   not active\n" +
        $"Model:    {ProviderDefaults.DefaultModelFor(providerType)}";

    _ui.ShowModal(providerType.ToString(), content);
  }

  private static void DismissListWindow(
    SpectreUserInterface spectreUi,
    ref InteractiveListWindow<ProviderListItem>? window)
  {
    if (window is null) return;
    var w = window;
    window = null;

    TguiApp.Invoke(() =>
    {
      spectreUi.Toplevel?.Remove(w);
      w.Dispose();
      spectreUi.Toplevel?.InputView.SetFocus();
    });
  }

  private static string FormatProviderListItem(ProviderListItem item, int rowWidth)
  {
    return $"{item.ProviderType.ToString(),-14}" +
        $"{item.Status,-10}" +
        $"{item.Model,-30}" +
        $"{item.ApiKeyDisplay}";
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

      if (SpectreUserInterface.Current?.Toplevel is not null)
      {
        var selected = SpectreHelpers.ShowSelectionDialog("Select Provider to Remove", providerNames);
        if (selected is null)
        {
          SpectreHelpers.Cancelled();
          return;
        }

        providerType = Enum.Parse<LlmProviderType>(selected);
      }
      else
      {
        var selected = SpectreHelpers.Select("Select a provider to remove:", providerNames);
        providerType = Enum.Parse<LlmProviderType>(selected);
      }
    }

    if (SpectreUserInterface.Current?.Toplevel is not null)
    {
      var isActive = _activeProvider.IsConfigured
          && _activeProvider.Config!.ProviderType == providerType;

      var message = isActive
        ? $"Remove provider '{providerType}'?\n\nWarning: '{providerType}' is the active provider. "
          + "It will remain active for this session but won't persist.\n\n"
          + "This will delete the stored API key and model configuration.\n"
          + "You can reconfigure later with /provider setup."
        : $"Remove provider '{providerType}'?\n\n"
          + "This will delete the stored API key and model configuration.\n"
          + "You can reconfigure later with /provider setup.";

      var confirmResult = MessageBox.Query(
        TguiApp.Instance, "Remove Provider", message, "Cancel", "Remove");
      if (confirmResult != 1)
      {
        SpectreHelpers.Cancelled();
        return;
      }
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
