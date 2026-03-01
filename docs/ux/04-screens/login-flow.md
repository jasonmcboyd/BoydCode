# Screen: Login Flow

## Overview

The login flow handles OAuth authentication with LLM providers. It is a
multi-step sequence: detect provider capabilities, resolve client credentials
(prompting if needed), open the user's browser for authorization, wait for the
OAuth callback, exchange the authorization code for tokens, and save
credentials. The flow runs as a standalone CLI command (`boydcode login`), not
as a slash command within the interactive session.

**Screen IDs**: AUTH-01 through AUTH-13

## Trigger

- User runs `boydcode login` from the command line.
- The `Provider` property is set by `ChatCommand` based on the active or
  default provider before `LoginCommand.ExecuteAsync()` is invoked.

## Layout (80 columns)

### Full OAuth Flow (Provider with Built-in Client ID)

```
Logging in to Anthropic...
Opening browser for authentication...
If the browser doesn't open, visit:
https://console.anthropic.com/oauth/authorize?client_id=...&response_type=code&...
Waiting for authorization...
Exchanging authorization code for tokens...
Successfully logged in!
```

### Full OAuth Flow (Provider Requiring User Credentials)

```
Logging in to Gemini...
This provider requires your own OAuth client credentials.
You can create them at: https://console.cloud.google.com/apis/credentials
Enter Client ID: ************************************
Enter Client Secret: ********
Vertex AI requires a GCP project ID and location.
Enter GCP Project ID: my-gcp-project
Enter GCP Location (us-central1): us-central1
Opening browser for authentication...
If the browser doesn't open, visit:
https://accounts.google.com/o/oauth2/v2/auth?client_id=...&response_type=code&...
Waiting for authorization...
Exchanging authorization code for tokens...
Successfully logged in!
```

### Non-Interactive Error

```
Error: Login requires an interactive terminal. Use --api-key or set the appropriate environment variable instead.
```

### No OAuth Support

```
Provider 'Ollama' does not support OAuth login.
```

### Login Timeout

```
Logging in to Anthropic...
Opening browser for authentication...
If the browser doesn't open, visit:
https://console.anthropic.com/oauth/authorize?...
Waiting for authorization...
Login timed out. Please try again.
```

### Token Exchange Failure

```
Logging in to Anthropic...
Opening browser for authentication...
...
Exchanging authorization code for tokens...
Token exchange failed (400):
{"error":"invalid_grant","error_description":"Authorization code expired."}
```

## States

| State | Condition | Visual Difference |
|---|---|---|
| Non-interactive error | Terminal is not interactive | Red error with guidance to use `--api-key` or env var |
| No OAuth support | Provider's OAuth config is null | Red message naming the provider |
| Login start | OAuth config found | Bold: "Logging in to {Provider}..." |
| Client credential prompt | Provider has no built-in client ID and no stored config | Yellow warning + dim link + Client ID prompt + optional Client Secret + optional GCP fields |
| Client ID missing | Empty client ID after resolution | Red error |
| Browser opening | Auth URL built | Three-line sequence: opening message, fallback hint (dim), clickable URL |
| Waiting | Browser opened, callback server listening | "Waiting for authorization..." (blocks up to 5 minutes) |
| Token exchange | Auth code received from callback | "Exchanging authorization code for tokens..." |
| Success | Tokens saved to credential store | Green: "Successfully logged in!" |
| Timeout | 5-minute wait exceeded | Red: "Login timed out. Please try again." |
| Auth callback error | OAuth state mismatch or provider error | Red: error message from callback server |
| Token exchange HTTP error | Non-success status from token endpoint | Red: "Token exchange failed ({status}):" + error body |
| Token exchange null | Response parsed but result is null | Red: "Failed to exchange authorization code for tokens." |

## Markup Tokens Used

| Token | Style Token (06-style-tokens.md) | Usage on This Screen |
|---|---|---|
| `[bold]` | bold (2.2) | "Logging in to {Provider}..." |
| `[green]` | success-green (1.1) | "Successfully logged in!" |
| `[red]` | error-red (1.1) | All error messages (no "Error:" prefix -- see note) |
| `[yellow]` | warning-yellow (1.1) | "This provider requires your own OAuth client credentials." |
| `[dim]` | dim (2.2) | Browser fallback hint, GCP location hint, credential creation link |
| `[link]` | (Spectre link) | Auth URL rendered as clickable terminal link |

**Note on error pattern**: LoginCommand does not use the standard
`[red]Error:[/]` prefix pattern. Error messages are rendered as full red
lines: `[red]Login timed out. Please try again.[/]`. This inconsistency is
documented in the style tokens audit (11.7).

## Interactive Elements

| Element | Type | Condition |
|---|---|---|
| Client ID | `SpectreHelpers.PromptNonEmpty` | Provider has no built-in client ID and no stored config |
| Client Secret | `AnsiConsole.Prompt` with `.Secret()` | Provider's OAuth config has `RequiresClientSecret` |
| GCP Project ID | `SpectreHelpers.PromptNonEmpty` | Same as Client Secret (triggered by `RequiresClientSecret`) |
| GCP Location | `AnsiConsole.Prompt` with `.DefaultValue("us-central1")` | Same as Client Secret |

The prompts use green-highlighted field names in labels:
- `"Enter [green]Client ID[/]:"`
- `"Enter [green]Client Secret[/]:"` (with `.Secret()` masking)
- `"Enter [green]GCP Project ID[/]:"`
- `"Enter [green]GCP Location[/] [dim](default: us-central1)[/]:"`

The Client Secret prompt has a custom validation error message:
`"[red]Client Secret cannot be empty[/]"`.

## Behavior

- **Non-interactive guard**: The first check is `AnsiConsole.Profile
  .Capabilities.Interactive`. If false, the error is shown immediately with
  guidance to use `--api-key` or environment variables.

- **OAuth config resolution**: `OAuthProviderRegistry.GetConfig(Provider)`
  returns the provider's OAuth configuration. If null, the provider does not
  support OAuth login.

- **Client credential resolution** (3-tier):
  1. If the OAuth config has a non-empty `ClientId` (e.g., Anthropic), use
     it directly. No prompts needed.
  2. If stored client config exists in `IOAuthClientConfigStore`, use it.
  3. Otherwise, prompt the user for Client ID, Client Secret (if required),
     GCP Project ID and Location (if required). Save the config for future
     use.

- **PKCE flow**: A code verifier (32 random bytes, base64url-encoded) and
  code challenge (SHA-256 of verifier, base64url-encoded) are generated for
  the OAuth PKCE extension. A random state parameter prevents CSRF.

- **Local callback server**: `OAuthCallbackServer` starts an HTTP listener
  on a random available port. The redirect URI is constructed as
  `{oauthConfig.RedirectUri}:{port}/callback`.

- **Browser launch**: `Process.Start` with `UseShellExecute = true` attempts
  to open the authorization URL. If it fails silently (the catch block
  swallows the exception), the URL is already displayed for manual copy.

- **Callback wait**: The server waits up to 5 minutes for the authorization
  code. Timeout produces `OperationCanceledException` caught as the "Login
  timed out" error.

- **Token exchange**: The authorization code is exchanged for access/refresh
  tokens via a POST to the token endpoint with form-encoded parameters.

- **Credential storage**: Tokens are saved via `ICredentialStore.SaveAsync()`
  keyed by `LlmProviderType`. Stored at
  `~/.boydcode/credentials/{provider}.json`.

- **Exit codes**: Returns `ExitCode.Success` (0) on success,
  `ExitCode.ConfigurationError` on config problems,
  `ExitCode.AuthenticationError` on auth failures.

## Edge Cases

- **Browser does not open**: The URL is displayed as a clickable `[link]`
  for manual navigation. The `Process.Start` failure is silently caught.

- **User closes browser without authorizing**: The callback server continues
  waiting until the 5-minute timeout, then shows the timeout error.

- **OAuth callback with wrong state**: The `OAuthCallbackServer` validates
  the state parameter. A mismatch throws `InvalidOperationException` caught
  as the auth error state.

- **Token exchange returns error JSON**: The full error body is rendered in
  red, escaped via `Markup.Escape`. The JSON structure is not parsed for
  user-friendly extraction.

- **Stored client credentials**: Once entered, client credentials are saved
  and reused on subsequent `boydcode login` calls. The user is not prompted
  again unless the stored config is deleted.

- **Non-interactive/piped terminal**: Login is completely blocked with a
  helpful error message directing to alternatives.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Text Prompt | Section 7 | Client ID, GCP Project ID, GCP Location prompts |
| Status Message | Section 1 | Error messages (though non-standard format) |

## Implementation

| Element | File | Method/Region | Lines |
|---|---|---|---|
| ExecuteAsync entry point | `Commands/LoginCommand.cs` | `ExecuteAsync` | 31-116 |
| Non-interactive guard | `Commands/LoginCommand.cs` | `ExecuteAsync` | 33-37 |
| OAuth config lookup | `Commands/LoginCommand.cs` | `ExecuteAsync` | 39-44 |
| Login start message | `Commands/LoginCommand.cs` | `ExecuteAsync` | 46 |
| Client credential resolution | `Commands/LoginCommand.cs` | `ResolveClientCredentialsAsync` | 118-172 |
| Client credential prompts | `Commands/LoginCommand.cs` | `ResolveClientCredentialsAsync` | 134-165 |
| PKCE generation | `Commands/LoginCommand.cs` | `GenerateCodeVerifier`, `GenerateCodeChallenge` | 174-184 |
| Auth URL construction | `Commands/LoginCommand.cs` | `BuildAuthorizationUrl` | 200-220 |
| Browser launch | `Commands/LoginCommand.cs` | `OpenBrowser` | 253-267 |
| Browser + URL display | `Commands/LoginCommand.cs` | `ExecuteAsync` | 71-74 |
| Callback wait | `Commands/LoginCommand.cs` | `ExecuteAsync` | 77-94 |
| Token exchange | `Commands/LoginCommand.cs` | `ExchangeCodeForTokensAsync` | 222-251 |
| Credential save | `Commands/LoginCommand.cs` | `ExecuteAsync` | 106-112 |
| Success message | `Commands/LoginCommand.cs` | `ExecuteAsync` | 114 |

All file paths are relative to `src/BoydCode.Presentation.Console/`.
