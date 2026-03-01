using System.Net;
using System.Web;

namespace BoydCode.Presentation.Console.Auth;

public sealed class OAuthCallbackServer : IAsyncDisposable
{
  private readonly HttpListener _listener = new();
  private readonly TaskCompletionSource<string> _codeReceived = new();
  private readonly string? _expectedState;

  public int Port { get; }

  public OAuthCallbackServer(string? expectedState = null)
  {
    _expectedState = expectedState;
    // Use port 0 trick: bind to a random available port
    // HttpListener doesn't support port 0 directly, so we find a free port first
    var tempListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
    tempListener.Start();
    Port = ((IPEndPoint)tempListener.LocalEndpoint).Port;
    tempListener.Stop();

    // Listen on http://localhost:{port}/ — must use "localhost" (not 127.0.0.1)
    // because the OAuth server redirects to http://localhost:{port}/callback
    var prefix = $"http://localhost:{Port}/";
    _listener.Prefixes.Add(prefix);
  }

  public void Start()
  {
    _listener.Start();
    _ = ListenAsync();
  }

  public Task<string> WaitForCodeAsync(CancellationToken ct = default)
  {
    ct.Register(() => _codeReceived.TrySetCanceled(ct));
    return _codeReceived.Task;
  }

  private async Task ListenAsync()
  {
    try
    {
      while (_listener.IsListening)
      {
        var context = await _listener.GetContextAsync();
        var query = context.Request.Url?.Query;
        var queryParams = HttpUtility.ParseQueryString(query ?? string.Empty);
        var code = queryParams["code"];
        var error = queryParams["error"];
        var state = queryParams["state"];

        if (!string.IsNullOrEmpty(error))
        {
          var errorDescription = queryParams["error_description"] ?? error;
          await SendResponseAsync(context, $"<html><body><h1>Login Failed</h1><p>{WebUtility.HtmlEncode(errorDescription)}</p><p>You can close this tab.</p></body></html>");
          _codeReceived.TrySetException(new InvalidOperationException($"OAuth error: {errorDescription}"));
          return;
        }

        if (_expectedState is not null && state != _expectedState)
        {
          await SendResponseAsync(context, "<html><body><h1>Login Failed</h1><p>Invalid state parameter. Possible CSRF attack.</p><p>You can close this tab.</p></body></html>");
          _codeReceived.TrySetException(new InvalidOperationException("OAuth state mismatch — possible CSRF attack."));
          return;
        }

        if (!string.IsNullOrEmpty(code))
        {
          await SendResponseAsync(context, "<html><body><h1>Login Successful</h1><p>You can close this tab and return to BoydCode.</p></body></html>");
          _codeReceived.TrySetResult(code);
          return;
        }

        await SendResponseAsync(context, "<html><body><p>Waiting for authorization...</p></body></html>");
      }
    }
    catch (ObjectDisposedException)
    {
      // Listener was stopped
    }
    catch (HttpListenerException)
    {
      // Listener was stopped
    }
  }

  private static async Task SendResponseAsync(HttpListenerContext context, string html)
  {
    var buffer = System.Text.Encoding.UTF8.GetBytes(html);
    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.ContentLength64 = buffer.Length;
    await context.Response.OutputStream.WriteAsync(buffer);
    context.Response.Close();
  }

  public ValueTask DisposeAsync()
  {
    if (_listener.IsListening)
    {
      _listener.Stop();
    }

    _listener.Close();
    return ValueTask.CompletedTask;
  }
}
