using System.Text.Json;
using System.Text.Json.Serialization;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Persistence.Auth;

public sealed partial class JsonOAuthClientConfigStore : IOAuthClientConfigStore
{
  private static readonly string OAuthDirectory =
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".boydcode", "oauth");

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true,
  };

  private readonly ILogger<JsonOAuthClientConfigStore> _logger;

  public JsonOAuthClientConfigStore(ILogger<JsonOAuthClientConfigStore> logger)
  {
    _logger = logger;
  }

  public async Task<OAuthClientConfig?> GetAsync(LlmProviderType provider, CancellationToken ct = default)
  {
    var filePath = GetFilePath(provider);
    if (!File.Exists(filePath))
    {
      return null;
    }

    try
    {
      var json = await File.ReadAllTextAsync(filePath, ct);
      var doc = JsonSerializer.Deserialize<OAuthClientConfigDocument>(json, JsonOptions);
      if (doc?.ClientId is null)
      {
        return null;
      }

      LogLoaded(provider);
      return new OAuthClientConfig(doc.ClientId, doc.ClientSecret, doc.GcpProject, doc.GcpLocation);
    }
    catch (JsonException ex)
    {
      LogLoadFailed(provider, ex);
      return null;
    }
  }

  public async Task SaveAsync(LlmProviderType provider, OAuthClientConfig config, CancellationToken ct = default)
  {
    Directory.CreateDirectory(OAuthDirectory);
    var filePath = GetFilePath(provider);

    var doc = new OAuthClientConfigDocument
    {
      ClientId = config.ClientId,
      ClientSecret = config.ClientSecret,
      GcpProject = config.GcpProject,
      GcpLocation = config.GcpLocation,
    };

    var json = JsonSerializer.Serialize(doc, JsonOptions);
    await File.WriteAllTextAsync(filePath, json, ct);
    LogSaved(provider);
  }

  private static string GetFilePath(LlmProviderType provider) =>
      Path.Combine(OAuthDirectory, $"{provider.ToString().ToLowerInvariant()}.json");

  [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded OAuth client config for {Provider}")]
  private partial void LogLoaded(LlmProviderType provider);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load OAuth client config for {Provider}")]
  private partial void LogLoadFailed(LlmProviderType provider, Exception exception);

  [LoggerMessage(Level = LogLevel.Debug, Message = "Saved OAuth client config for {Provider}")]
  private partial void LogSaved(LlmProviderType provider);

  private sealed class OAuthClientConfigDocument
  {
    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    [JsonPropertyName("client_secret")]
    public string? ClientSecret { get; set; }

    [JsonPropertyName("gcp_project")]
    public string? GcpProject { get; set; }

    [JsonPropertyName("gcp_location")]
    public string? GcpLocation { get; set; }
  }
}
