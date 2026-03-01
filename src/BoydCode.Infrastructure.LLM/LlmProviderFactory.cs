using Anthropic;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using Google.Apis.Auth.OAuth2;
using Google.GenAI;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace BoydCode.Infrastructure.LLM;

/// <summary>
/// Creates <see cref="ILlmProvider"/> instances for different LLM backends by wrapping
/// their MEAI <see cref="IChatClient"/> implementations via <see cref="MeaiLlmProviderAdapter"/>.
/// </summary>
public sealed class LlmProviderFactory : ILlmProviderFactory
{
  public ILlmProvider Create(LlmProviderConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    if (config.ProviderType == LlmProviderType.Gemini)
    {
      return CreateGeminiProvider(config);
    }

    IChatClient chatClient = config.ProviderType switch
    {
      LlmProviderType.Anthropic => CreateAnthropicClient(config),
      LlmProviderType.OpenAi => CreateOpenAiClient(config),
      LlmProviderType.Ollama => CreateOllamaClient(config),
      _ => throw new ArgumentOutOfRangeException(
          nameof(config),
          config.ProviderType,
          $"Unsupported LLM provider type: {config.ProviderType}"),
    };

    var capabilities = ProviderDefaults.For(config.ProviderType);
    return new MeaiLlmProviderAdapter(chatClient, config.Model, config.MaxTokens, capabilities);
  }

  private static IChatClient CreateAnthropicClient(LlmProviderConfig config)
  {
    var clientOptions = new Anthropic.Core.ClientOptions();

    if (!string.IsNullOrEmpty(config.ApiKey))
    {
      clientOptions.ApiKey = config.ApiKey;
    }
    else if (!string.IsNullOrEmpty(config.AuthToken))
    {
      clientOptions.AuthToken = config.AuthToken;
    }
    else
    {
      throw new InvalidOperationException(
          "Anthropic requires either an API key or an OAuth token. Run 'boydcode login' to authenticate.");
    }

    var client = new AnthropicClient(clientOptions);

    // AsIChatClient wraps the Anthropic client as an MEAI IChatClient.
    // The model is passed via ChatOptions.ModelId at call time.
    return client.AsIChatClient();
  }

  private static IChatClient CreateOpenAiClient(LlmProviderConfig config)
  {
    var apiKey = config.ApiKey
        ?? throw new InvalidOperationException("OpenAI API key is required.");

    var openAiClient = new OpenAI.OpenAIClient(apiKey);

    // GetChatClient binds to a specific model; AsIChatClient wraps it as MEAI IChatClient.
    return openAiClient.GetChatClient(config.Model).AsIChatClient();
  }

  private static OllamaApiClient CreateOllamaClient(LlmProviderConfig config)
  {
    var baseUrl = config.BaseUrl ?? "http://localhost:11434";

    // OllamaApiClient implements IChatClient explicitly.
    // The default model is set in the constructor.
    return new OllamaApiClient(new Uri(baseUrl), config.Model);
  }

  private static GeminiLlmProviderAdapter CreateGeminiProvider(LlmProviderConfig config)
  {
    var apiKey = config.ApiKey;
    var authToken = config.AuthToken;

    if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(authToken))
    {
      throw new InvalidOperationException(
          "Gemini requires either an API key or an OAuth token. Set GEMINI_API_KEY or run 'boydcode login --provider gemini'.");
    }

    Client nativeClient;
    if (!string.IsNullOrEmpty(apiKey))
    {
      nativeClient = new Client(apiKey: apiKey);
    }
    else
    {
      var project = config.GcpProject
          ?? throw new InvalidOperationException(
              "GCP project ID is required for Gemini OAuth. Run 'boydcode login --provider gemini' to configure.");
      var location = config.GcpLocation ?? "us-central1";

      nativeClient = new Client(
          vertexAI: true,
          project: project,
          location: location,
          credential: GoogleCredential.FromAccessToken(authToken));
    }

    var chatClient = nativeClient.AsIChatClient(config.Model);
    var capabilities = ProviderDefaults.For(LlmProviderType.Gemini);

    return new GeminiLlmProviderAdapter(chatClient, nativeClient, config.Model, config.MaxTokens, capabilities);
  }
}
