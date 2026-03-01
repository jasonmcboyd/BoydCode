namespace BoydCode.Domain.Configuration;

public sealed record OAuthClientConfig(string ClientId, string? ClientSecret, string? GcpProject = null, string? GcpLocation = null);
