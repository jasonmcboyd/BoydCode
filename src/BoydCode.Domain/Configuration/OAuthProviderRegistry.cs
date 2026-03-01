using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Configuration;

public static class OAuthProviderRegistry
{
  public static OAuthProviderConfig? GetConfig(LlmProviderType provider) => provider switch
  {
    LlmProviderType.Anthropic => new OAuthProviderConfig(
        ClientId: "9d1c250a-e61b-44d9-88ed-5944d1962f5e",
        AuthorizationEndpoint: "https://claude.ai/oauth/authorize",
        TokenEndpoint: "https://console.anthropic.com/v1/oauth/token",
        RedirectUri: "http://localhost",
        Scope: "user:inference user:profile",
        ResponseType: "code",
        GrantTypeAuthorizationCode: "authorization_code",
        GrantTypeRefreshToken: "refresh_token",
        CodeChallengeMethod: "S256",
        ExpiryBuffer: TimeSpan.FromMinutes(5)),
    LlmProviderType.Gemini => new OAuthProviderConfig(
        ClientId: "",
        AuthorizationEndpoint: "https://accounts.google.com/o/oauth2/auth",
        TokenEndpoint: "https://oauth2.googleapis.com/token",
        RedirectUri: "http://localhost",
        Scope: "https://www.googleapis.com/auth/cloud-platform",
        ResponseType: "code",
        GrantTypeAuthorizationCode: "authorization_code",
        GrantTypeRefreshToken: "refresh_token",
        CodeChallengeMethod: "S256",
        ExpiryBuffer: TimeSpan.FromMinutes(5),
        ExtraAuthorizationParams: new Dictionary<string, string>
        {
          ["access_type"] = "offline",
          ["prompt"] = "consent",
        }.AsReadOnly(),
        RequiresClientSecret: true),
    _ => null,
  };
}
