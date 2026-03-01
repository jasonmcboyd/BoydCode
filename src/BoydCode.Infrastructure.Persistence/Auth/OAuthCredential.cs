using System.Text.Json.Serialization;

namespace BoydCode.Infrastructure.Persistence.Auth;

public sealed record OAuthCredential
{
  [JsonPropertyName("access_token")]
  public required string AccessToken { get; init; }

  [JsonPropertyName("refresh_token")]
  public required string RefreshToken { get; init; }

  [JsonPropertyName("expires_at")]
  public required DateTimeOffset ExpiresAt { get; init; }

  [JsonPropertyName("scope")]
  public required string Scope { get; init; }
}
