namespace BoydCode.Domain.Configuration;

public sealed record OAuthProviderConfig(
    string ClientId,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string RedirectUri,
    string Scope,
    string ResponseType,
    string GrantTypeAuthorizationCode,
    string GrantTypeRefreshToken,
    string CodeChallengeMethod,
    TimeSpan ExpiryBuffer,
    IReadOnlyDictionary<string, string>? ExtraAuthorizationParams = null,
    bool RequiresClientSecret = false);
