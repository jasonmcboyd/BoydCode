using System.Text.Json;
using System.Text.Json.Serialization;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Persistence;

public sealed partial class JsonProviderConfigStore : IProviderConfigStore, IDisposable
{
  private static readonly string FilePath =
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".boydcode", "providers.json");

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
  };

  private readonly ILogger<JsonProviderConfigStore> _logger;
  private readonly SemaphoreSlim _lock = new(1, 1);

  public JsonProviderConfigStore(ILogger<JsonProviderConfigStore> logger)
  {
    _logger = logger;
  }

  public async Task<ProviderProfile?> GetAsync(LlmProviderType provider, CancellationToken ct = default)
  {
    await _lock.WaitAsync(ct);
    try
    {
      var doc = await LoadDocumentAsync(ct);
      var key = ToKey(provider);

      if (doc.Providers is null || !doc.Providers.TryGetValue(key, out var entry))
      {
        return null;
      }

      LogFileLoaded();
      return ToProfile(provider, entry);
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task SaveAsync(ProviderProfile profile, CancellationToken ct = default)
  {
    await _lock.WaitAsync(ct);
    try
    {
      var doc = await LoadDocumentAsync(ct);
      doc.Providers ??= new Dictionary<string, ProviderEntry>();

      var key = ToKey(profile.ProviderType);
      doc.Providers[key] = new ProviderEntry
      {
        ApiKey = profile.ApiKey,
        DefaultModel = profile.DefaultModel,
      };

      await SaveDocumentAsync(doc, ct);
      LogFileSaved();
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task RemoveAsync(LlmProviderType provider, CancellationToken ct = default)
  {
    await _lock.WaitAsync(ct);
    try
    {
      var doc = await LoadDocumentAsync(ct);
      var key = ToKey(provider);

      if (doc.Providers is not null && doc.Providers.Remove(key))
      {
        if (string.Equals(doc.LastProvider, key, StringComparison.OrdinalIgnoreCase))
        {
          doc.LastProvider = null;
        }

        await SaveDocumentAsync(doc, ct);
        LogProviderRemoved(provider);
      }
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task<IReadOnlyList<ProviderProfile>> GetAllAsync(CancellationToken ct = default)
  {
    await _lock.WaitAsync(ct);
    try
    {
      var doc = await LoadDocumentAsync(ct);

      if (doc.Providers is null || doc.Providers.Count == 0)
      {
        return [];
      }

      var profiles = new List<ProviderProfile>();

      foreach (var (key, entry) in doc.Providers)
      {
        if (Enum.TryParse<LlmProviderType>(key, true, out var type))
        {
          profiles.Add(ToProfile(type, entry));
        }
      }

      LogFileLoaded();
      return profiles.AsReadOnly();
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task<LlmProviderType?> GetLastUsedProviderAsync(CancellationToken ct = default)
  {
    await _lock.WaitAsync(ct);
    try
    {
      var doc = await LoadDocumentAsync(ct);

      if (doc.LastProvider is not null &&
          Enum.TryParse<LlmProviderType>(doc.LastProvider, true, out var type))
      {
        return type;
      }

      return null;
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task SetLastUsedProviderAsync(LlmProviderType provider, CancellationToken ct = default)
  {
    await _lock.WaitAsync(ct);
    try
    {
      var doc = await LoadDocumentAsync(ct);
      doc.LastProvider = ToKey(provider);
      await SaveDocumentAsync(doc, ct);
      LogFileSaved();
    }
    finally
    {
      _lock.Release();
    }
  }

  public void Dispose()
  {
    _lock.Dispose();
  }

  private static async Task<ProviderConfigDocument> LoadDocumentAsync(CancellationToken ct)
  {
    if (!File.Exists(FilePath))
    {
      return new ProviderConfigDocument();
    }

    var json = await File.ReadAllTextAsync(FilePath, ct);
    return JsonSerializer.Deserialize<ProviderConfigDocument>(json, JsonOptions) ?? new ProviderConfigDocument();
  }

  private static async Task SaveDocumentAsync(ProviderConfigDocument doc, CancellationToken ct)
  {
    var directory = Path.GetDirectoryName(FilePath)!;
    Directory.CreateDirectory(directory);

    var json = JsonSerializer.Serialize(doc, JsonOptions);
    await File.WriteAllTextAsync(FilePath, json, ct);
  }

  private static string ToKey(LlmProviderType provider) =>
      provider.ToString().ToLowerInvariant();

  private static ProviderProfile ToProfile(LlmProviderType type, ProviderEntry entry) =>
      new(type, entry.ApiKey, entry.DefaultModel);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Provider config file loaded")]
  private partial void LogFileLoaded();

  [LoggerMessage(Level = LogLevel.Debug, Message = "Provider config file saved")]
  private partial void LogFileSaved();

  [LoggerMessage(Level = LogLevel.Information, Message = "Provider config removed for {Provider}")]
  private partial void LogProviderRemoved(LlmProviderType provider);

  private sealed class ProviderConfigDocument
  {
    [JsonPropertyName("last_provider")]
    public string? LastProvider { get; set; }

    [JsonPropertyName("providers")]
    public Dictionary<string, ProviderEntry>? Providers { get; set; }
  }

  private sealed class ProviderEntry
  {
    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("default_model")]
    public string? DefaultModel { get; set; }
  }
}
