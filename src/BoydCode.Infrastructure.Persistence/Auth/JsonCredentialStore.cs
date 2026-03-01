using System.Net.Http.Json;
using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Persistence.Auth;

public sealed partial class JsonCredentialStore : ICredentialStore, IDisposable
{
  private static readonly string CredentialDirectory =
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".boydcode", "credentials");

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true,
  };

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IOAuthClientConfigStore _oauthClientConfigStore;
  private readonly ILogger<JsonCredentialStore> _logger;
  private readonly SemaphoreSlim _lock = new(1, 1);

  public JsonCredentialStore(IHttpClientFactory httpClientFactory, IOAuthClientConfigStore oauthClientConfigStore, ILogger<JsonCredentialStore> logger)
  {
    _httpClientFactory = httpClientFactory;
    _oauthClientConfigStore = oauthClientConfigStore;
    _logger = logger;
  }

  public async Task<string?> GetValidTokenAsync(LlmProviderType provider, CancellationToken ct = default)
  {
    await _lock.WaitAsync(ct);
    try
    {
      var credential = await LoadAsync(provider, ct);
      if (credential is null)
      {
        return null;
      }

      var oauthConfig = OAuthProviderRegistry.GetConfig(provider);
      var expiryBuffer = oauthConfig?.ExpiryBuffer ?? TimeSpan.FromMinutes(5);

      if (credential.ExpiresAt - expiryBuffer > DateTimeOffset.UtcNow)
      {
        return credential.AccessToken;
      }

      LogTokenExpiring();
      var refreshed = await RefreshTokenAsync(provider, credential.RefreshToken, ct);
      return refreshed?.AccessToken;
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task SaveAsync(LlmProviderType provider, string accessToken, string refreshToken, DateTimeOffset expiresAt, string scope, CancellationToken ct = default)
  {
    await _lock.WaitAsync(ct);
    try
    {
      var credential = new OAuthCredential
      {
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        ExpiresAt = expiresAt,
        Scope = scope,
      };

      Directory.CreateDirectory(CredentialDirectory);
      var filePath = GetCredentialPath(provider);
      var json = JsonSerializer.Serialize(credential, JsonOptions);
      await File.WriteAllTextAsync(filePath, json, ct);
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task ClearAsync(LlmProviderType provider, CancellationToken ct = default)
  {
    await _lock.WaitAsync(ct);
    try
    {
      var filePath = GetCredentialPath(provider);
      if (File.Exists(filePath))
      {
        File.Delete(filePath);
      }

      await Task.CompletedTask;
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

  private static string GetCredentialPath(LlmProviderType provider) =>
      Path.Combine(CredentialDirectory, $"{provider.ToString().ToLowerInvariant()}.json");

  private static async Task<OAuthCredential?> LoadAsync(LlmProviderType provider, CancellationToken ct)
  {
    var filePath = GetCredentialPath(provider);
    if (!File.Exists(filePath))
    {
      return null;
    }

    var json = await File.ReadAllTextAsync(filePath, ct);
    return JsonSerializer.Deserialize<OAuthCredential>(json);
  }

  private async Task<OAuthCredential?> RefreshTokenAsync(LlmProviderType provider, string refreshToken, CancellationToken ct)
  {
    var oauthConfig = OAuthProviderRegistry.GetConfig(provider);
    if (oauthConfig is null)
    {
      LogTokenRefreshFailed(new InvalidOperationException($"No OAuth config for provider {provider}"));
      return null;
    }

    try
    {
      // Resolve effective client credentials
      var storedClientConfig = await _oauthClientConfigStore.GetAsync(provider, ct);
      var effectiveClientId = !string.IsNullOrEmpty(storedClientConfig?.ClientId)
          ? storedClientConfig.ClientId
          : oauthConfig.ClientId;

      var client = _httpClientFactory.CreateClient("OAuth");
      var formFields = new Dictionary<string, string>
      {
        ["grant_type"] = oauthConfig.GrantTypeRefreshToken,
        ["refresh_token"] = refreshToken,
        ["client_id"] = effectiveClientId,
      };

      if (storedClientConfig?.ClientSecret is not null)
      {
        formFields["client_secret"] = storedClientConfig.ClientSecret;
      }

      using var content = new FormUrlEncodedContent(formFields);
      var response = await client.PostAsync(oauthConfig.TokenEndpoint, content, ct);
      response.EnsureSuccessStatusCode();

      var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
      if (tokenResponse is null)
      {
        LogTokenRefreshNullResponse();
        return null;
      }

      var expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
      var credential = new OAuthCredential
      {
        AccessToken = tokenResponse.AccessToken,
        RefreshToken = tokenResponse.RefreshToken ?? refreshToken,
        ExpiresAt = expiresAt,
        Scope = tokenResponse.Scope ?? oauthConfig.Scope,
      };

      Directory.CreateDirectory(CredentialDirectory);
      var filePath = GetCredentialPath(provider);
      var json = JsonSerializer.Serialize(credential, JsonOptions);
      await File.WriteAllTextAsync(filePath, json, ct);

      LogTokenRefreshed();
      return credential;
    }
    catch (HttpRequestException ex)
    {
      LogTokenRefreshFailed(ex);
      return null;
    }
  }

  [LoggerMessage(Level = LogLevel.Debug, Message = "OAuth token expired or expiring soon, refreshing...")]
  private partial void LogTokenExpiring();

  [LoggerMessage(Level = LogLevel.Warning, Message = "Token refresh returned null response")]
  private partial void LogTokenRefreshNullResponse();

  [LoggerMessage(Level = LogLevel.Debug, Message = "OAuth token refreshed successfully")]
  private partial void LogTokenRefreshed();

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to refresh OAuth token")]
  private partial void LogTokenRefreshFailed(Exception exception);

  private sealed record TokenResponse
  {
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("scope")]
    public string? Scope { get; init; }
  }
}
