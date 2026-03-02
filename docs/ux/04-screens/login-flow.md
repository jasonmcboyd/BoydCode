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

## Style References

See [06-style-tokens.md](../06-style-tokens.md) for the complete visual language.

**Theme constants used:** `Theme.Semantic.Default` with bold weight ("Logging in to {Provider}..."),
`Theme.Semantic.Success` (green, "Successfully logged in!"), `Theme.Semantic.Error`
(red, all error messages), `Theme.Semantic.Warning` (yellow, credential requirement notice),
`Theme.Semantic.Muted` (browser fallback hint, GCP location hint, credential creation link)

Auth URL is rendered as a clickable terminal hyperlink (OSC 8 escape sequence where
supported; plain URL text as fallback).

**Component patterns:** Text Prompt (#13), Status Message (#7)

**Note on error pattern**: LoginCommand does not use the standard error prefix pattern.
Error messages are rendered as full red lines: "Login timed out. Please try again."
This inconsistency is documented in the style tokens audit (11.7).

## Interactive Elements

| Element | Type | Condition |
|---|---|---|
| Client ID | Non-empty text prompt | Provider has no built-in client ID and no stored config |
| Client Secret | Masked text prompt (input not echoed) | Provider's OAuth config has `RequiresClientSecret` |
| GCP Project ID | Non-empty text prompt | Same as Client Secret (triggered by `RequiresClientSecret`) |
| GCP Location | Text prompt with default "us-central1" | Same as Client Secret |

The prompts use `Theme.Semantic.Success` (green) for field name highlights in labels:

```
(Markup notation indicates visual intent, not implementation API)
"Enter [green]Client ID[/]:"
"Enter [green]Client Secret[/]:"  (input masked)
"Enter [green]GCP Project ID[/]:"
"Enter [green]GCP Location[/] [dim](default: us-central1)[/]:"
```

The Client Secret prompt has a validation error message in `Theme.Semantic.Error`
(red): "Client Secret cannot be empty".

## Behavior

- **Non-interactive guard**: The first check detects whether the terminal is
  interactive (stdin connected to a TTY). If not interactive, the error is shown
  immediately with guidance to use `--api-key` or environment variables.

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
  `Theme.Semantic.Error` (red) as plain text. The JSON structure is not parsed
  for user-friendly extraction.

- **Stored client credentials**: Once entered, client credentials are saved
  and reused on subsequent `boydcode login` calls. The user is not prompted
  again unless the stored config is deleted.

- **Non-interactive/piped terminal**: Login is completely blocked with a
  helpful error message directing to alternatives.

## Component Patterns Used

| Pattern | Reference (07-component-patterns.md) | Usage |
|---|---|---|
| Text Prompt | #13 | Client ID, GCP Project ID, GCP Location prompts |
| Status Message | #7 | Error messages (though non-standard format) |

