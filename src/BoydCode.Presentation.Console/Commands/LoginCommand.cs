using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Web;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using BoydCode.Presentation.Console.Auth;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BoydCode.Presentation.Console.Commands;

public sealed class LoginCommand : AsyncCommand
{
  private readonly ICredentialStore _credentialStore;
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IOAuthClientConfigStore _oauthClientConfigStore;

  public LlmProviderType Provider { get; set; } = LlmProviderType.Anthropic;

  public LoginCommand(ICredentialStore credentialStore, IHttpClientFactory httpClientFactory, IOAuthClientConfigStore oauthClientConfigStore)
  {
    _credentialStore = credentialStore;
    _httpClientFactory = httpClientFactory;
    _oauthClientConfigStore = oauthClientConfigStore;
  }

  public override async Task<int> ExecuteAsync(CommandContext context)
  {
    if (!AnsiConsole.Profile.Capabilities.Interactive)
    {
      SpectreHelpers.Error("Login requires an interactive terminal. Use --api-key or set the appropriate environment variable instead.");
      return (int)ExitCode.ConfigurationError;
    }

    var oauthConfig = OAuthProviderRegistry.GetConfig(Provider);
    if (oauthConfig is null)
    {
      AnsiConsole.MarkupLine($"[red]Provider '{Provider}' does not support OAuth login.[/]");
      return (int)ExitCode.ConfigurationError;
    }

    AnsiConsole.MarkupLine($"[bold]Logging in to {Provider}...[/]");

    // Resolve effective client credentials
    var clientConfig = await ResolveClientCredentialsAsync(oauthConfig);
    if (clientConfig is null || string.IsNullOrEmpty(clientConfig.ClientId))
    {
      AnsiConsole.MarkupLine("[red]OAuth client ID is required. Please try again.[/]");
      return (int)ExitCode.ConfigurationError;
    }

    // Generate PKCE code verifier and challenge
    var codeVerifier = GenerateCodeVerifier();
    var codeChallenge = GenerateCodeChallenge(codeVerifier);
    var state = GenerateState();

    // Start local callback server
    await using var callbackServer = new OAuthCallbackServer(state);
    callbackServer.Start();

    var redirectUri = $"{oauthConfig.RedirectUri}:{callbackServer.Port}/callback";

    // Build authorization URL
    var authUrl = BuildAuthorizationUrl(oauthConfig, clientConfig.ClientId, codeChallenge, redirectUri, state);

    // Open browser
    AnsiConsole.MarkupLine("Opening browser for authentication...");
    AnsiConsole.MarkupLine("[dim]If the browser doesn't open, visit:[/]");
    AnsiConsole.MarkupLine($"[link]{authUrl}[/]");
    OpenBrowser(authUrl);

    // Wait for callback
    AnsiConsole.MarkupLine("Waiting for authorization...");
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

    string authorizationCode;
    try
    {
      authorizationCode = await callbackServer.WaitForCodeAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
      AnsiConsole.MarkupLine("[red]Login timed out. Please try again.[/]");
      return (int)ExitCode.AuthenticationError;
    }
    catch (InvalidOperationException ex)
    {
      AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
      return (int)ExitCode.AuthenticationError;
    }

    // Exchange code for tokens
    AnsiConsole.MarkupLine("Exchanging authorization code for tokens...");
    var tokenResult = await ExchangeCodeForTokensAsync(oauthConfig, clientConfig.ClientId, clientConfig.ClientSecret, authorizationCode, codeVerifier, redirectUri, cts.Token);
    if (tokenResult is null)
    {
      AnsiConsole.MarkupLine("[red]Failed to exchange authorization code for tokens.[/]");
      return (int)ExitCode.AuthenticationError;
    }

    // Save credentials
    var expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResult.ExpiresIn);
    await _credentialStore.SaveAsync(
        Provider,
        tokenResult.AccessToken,
        tokenResult.RefreshToken ?? string.Empty,
        expiresAt,
        tokenResult.Scope ?? oauthConfig.Scope);

    AnsiConsole.MarkupLine("[green]Successfully logged in![/]");
    return (int)ExitCode.Success;
  }

  private async Task<OAuthClientConfig?> ResolveClientCredentialsAsync(OAuthProviderConfig oauthConfig)
  {
    // If the static config has a non-empty ClientId, use it directly (e.g., Anthropic)
    if (!string.IsNullOrEmpty(oauthConfig.ClientId))
    {
      return new OAuthClientConfig(oauthConfig.ClientId, null);
    }

    // Try loading stored client config
    var stored = await _oauthClientConfigStore.GetAsync(Provider);
    if (stored is not null)
    {
      return stored;
    }

    // Prompt user for credentials
    AnsiConsole.MarkupLine("[yellow]This provider requires your own OAuth client credentials.[/]");
    AnsiConsole.MarkupLine("[dim]You can create them at: https://console.cloud.google.com/apis/credentials[/]");

    var clientId = SpectreHelpers.PromptNonEmpty("Enter [green]Client ID[/]:");

    string? clientSecret = null;
    if (oauthConfig.RequiresClientSecret)
    {
      clientSecret = AnsiConsole.Prompt(
          new TextPrompt<string>("Enter [green]Client Secret[/]:")
              .Secret()
              .ValidationErrorMessage("[red]Client Secret cannot be empty[/]")
              .Validate(s => !string.IsNullOrWhiteSpace(s)));
    }

    string? gcpProject = null;
    string? gcpLocation = null;
    if (oauthConfig.RequiresClientSecret)
    {
      AnsiConsole.MarkupLine("[dim]Vertex AI requires a GCP project ID and location.[/]");
      gcpProject = SpectreHelpers.PromptNonEmpty("Enter [green]GCP Project ID[/]:");

      gcpLocation = AnsiConsole.Prompt(
          new TextPrompt<string>("Enter [green]GCP Location[/] [dim](default: us-central1)[/]:")
              .DefaultValue("us-central1")
              .AllowEmpty());

      if (string.IsNullOrWhiteSpace(gcpLocation))
      {
        gcpLocation = "us-central1";
      }
    }

    // Save for future use
    var config = new OAuthClientConfig(clientId, clientSecret, gcpProject, gcpLocation);
    await _oauthClientConfigStore.SaveAsync(Provider, config);

    return config;
  }

  private static string GenerateCodeVerifier()
  {
    var bytes = RandomNumberGenerator.GetBytes(32);
    return Base64UrlEncode(bytes);
  }

  private static string GenerateCodeChallenge(string codeVerifier)
  {
    var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
    return Base64UrlEncode(hash);
  }

  private static string GenerateState()
  {
    var bytes = RandomNumberGenerator.GetBytes(32);
    return Base64UrlEncode(bytes);
  }

  private static string Base64UrlEncode(byte[] bytes)
  {
    return Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
  }

  private static string BuildAuthorizationUrl(OAuthProviderConfig oauthConfig, string effectiveClientId, string codeChallenge, string redirectUri, string state)
  {
    var queryParams = HttpUtility.ParseQueryString(string.Empty);
    queryParams["client_id"] = effectiveClientId;
    queryParams["response_type"] = oauthConfig.ResponseType;
    queryParams["redirect_uri"] = redirectUri;
    queryParams["scope"] = oauthConfig.Scope;
    queryParams["code_challenge"] = codeChallenge;
    queryParams["code_challenge_method"] = oauthConfig.CodeChallengeMethod;
    queryParams["state"] = state;

    if (oauthConfig.ExtraAuthorizationParams is not null)
    {
      foreach (var (key, value) in oauthConfig.ExtraAuthorizationParams)
      {
        queryParams[key] = value;
      }
    }

    return $"{oauthConfig.AuthorizationEndpoint}?{queryParams}";
  }

  private async Task<TokenResponse?> ExchangeCodeForTokensAsync(
      OAuthProviderConfig oauthConfig, string effectiveClientId, string? clientSecret, string code, string codeVerifier, string redirectUri, CancellationToken ct)
  {
    var client = _httpClientFactory.CreateClient("OAuth");
    var formFields = new Dictionary<string, string>
    {
      ["grant_type"] = oauthConfig.GrantTypeAuthorizationCode,
      ["code"] = code,
      ["redirect_uri"] = redirectUri,
      ["client_id"] = effectiveClientId,
      ["code_verifier"] = codeVerifier,
    };

    if (clientSecret is not null)
    {
      formFields["client_secret"] = clientSecret;
    }

    using var content = new FormUrlEncodedContent(formFields);
    var response = await client.PostAsync(oauthConfig.TokenEndpoint, content, ct);
    if (!response.IsSuccessStatusCode)
    {
      var errorBody = await response.Content.ReadAsStringAsync(ct);
      AnsiConsole.MarkupLine($"[red]Token exchange failed ({response.StatusCode}):[/]");
      AnsiConsole.MarkupLine($"[red]{Markup.Escape(errorBody)}[/]");
      return null;
    }

    return await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
  }

  private static void OpenBrowser(string url)
  {
    try
    {
      Process.Start(new ProcessStartInfo
      {
        FileName = url,
        UseShellExecute = true,
      });
    }
    catch
    {
      // Browser open failed; URL is already displayed for manual copy
    }
  }

  private sealed record TokenResponse
  {
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
  }
}
