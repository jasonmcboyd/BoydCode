using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.LlmRequests;
using BoydCode.Domain.LlmResponses;
using Microsoft.Extensions.Logging;

namespace BoydCode.Application.Services;

public sealed partial class SubAgentExecutor
{
  private readonly ActiveProvider _activeProvider;
  private readonly ActiveExecutionEngine _activeEngine;
  private readonly ILlmProviderFactory _providerFactory;
  private readonly IUserInterface _ui;
  private readonly IConversationLogger _conversationLogger;
  private readonly ILogger<SubAgentExecutor> _logger;

  public SubAgentExecutor(
      ActiveProvider activeProvider,
      ActiveExecutionEngine activeEngine,
      ILlmProviderFactory providerFactory,
      IUserInterface ui,
      IConversationLogger conversationLogger,
      ILogger<SubAgentExecutor> logger)
  {
    _activeProvider = activeProvider;
    _activeEngine = activeEngine;
    _providerFactory = providerFactory;
    _ui = ui;
    _conversationLogger = conversationLogger;
    _logger = logger;
  }

  public async Task<string> ExecuteAsync(
      AgentDefinition agent, string task, string workingDirectory,
      string? promptExtensions = null, CancellationToken ct = default)
  {
    LogAgentDelegation(agent.Name, task);
    _ui.RenderHint($"Agent '{agent.Name}' working...");

    var maxTurns = Math.Min(
        agent.MaxTurns ?? AgentDefaults.DefaultMaxTurns,
        AgentDefaults.MaxAllowedTurns);

    // Build system prompt: MetaPrompt + BOYDCODE.md extensions + agent instructions
    var metaPrompt = MetaPrompt.Build(
        _activeEngine.Mode,
        _activeEngine.Engine?.GetAvailableCommands() ?? []);
    var systemPrompt = promptExtensions is not null
        ? $"{metaPrompt}\n\n---\n\n{promptExtensions}\n\n---\n\n{agent.Instructions}"
        : $"{metaPrompt}\n\n---\n\n{agent.Instructions}";

    // Determine model and provider
    var model = agent.ModelOverride ?? _activeProvider.Config!.Model;
    ILlmProvider provider;
    IDisposable? temporaryProvider = null;

    if (agent.ModelOverride is not null && agent.ModelOverride != _activeProvider.Config!.Model)
    {
      // Create a temporary provider with the overridden model
      var overrideConfig = new LlmProviderConfig
      {
        ProviderType = _activeProvider.Config!.ProviderType,
        ApiKey = _activeProvider.Config.ApiKey,
        AuthToken = _activeProvider.Config.AuthToken,
        BaseUrl = _activeProvider.Config.BaseUrl,
        Model = agent.ModelOverride,
        MaxTokens = _activeProvider.Config.MaxTokens,
        GcpProject = _activeProvider.Config.GcpProject,
        GcpLocation = _activeProvider.Config.GcpLocation,
      };
      provider = _providerFactory.Create(overrideConfig);
      temporaryProvider = provider as IDisposable;
    }
    else
    {
      provider = _activeProvider.Provider!;
    }

    try
    {
      return await RunAgentLoopAsync(
          agent, provider, model, systemPrompt, task, workingDirectory, maxTurns, ct);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      LogAgentError(agent.Name, ex);
      var errorResult = $"Agent '{agent.Name}' failed: {ex.Message}";
      _ui.RenderHint($"Agent '{agent.Name}' finished with error.");
      return errorResult;
    }
    finally
    {
      temporaryProvider?.Dispose();
    }
  }

  private async Task<string> RunAgentLoopAsync(
      AgentDefinition agent,
      ILlmProvider provider,
      string model,
      string systemPrompt,
      string task,
      string workingDirectory,
      int maxTurns,
      CancellationToken ct)
  {
    // Isolated conversation for this sub-agent execution
    var conversation = new Conversation();
    conversation.AddUserMessage(task);

    var baseRequest = new LlmRequest
    {
      Model = model,
      SystemPrompt = systemPrompt,
      Tools = [AgentOrchestrator.ShellToolDefinition],
      ToolChoice = ToolChoiceStrategy.Auto,
    };

    string? lastTextResponse = null;

    for (var turn = 0; turn < maxTurns; turn++)
    {
      var request = baseRequest with
      {
        Messages = conversation.Messages,
        Stream = false,
      };

      var response = await provider.SendAsync(request, ct);

      var textContent = response.TextContent;
      if (!string.IsNullOrEmpty(textContent))
      {
        lastTextResponse = textContent;
      }

      // Add assistant response to conversation
      conversation.AddAssistantMessage(response.Content);

      // If no tool calls, the agent is done
      if (!response.HasToolUse)
      {
        break;
      }

      // Process tool calls
      await ProcessToolCallsAsync(conversation, response.ToolUseCalls, workingDirectory, ct);
    }

    var result = lastTextResponse ?? "(No response from agent)";
    LogAgentCompleted(agent.Name);
    _ui.RenderHint($"Agent '{agent.Name}' finished.");
    return result;
  }

  private async Task ProcessToolCallsAsync(
      Conversation conversation,
      IEnumerable<ToolUseBlock> toolCalls,
      string workingDirectory,
      CancellationToken ct)
  {
    foreach (var toolCall in toolCalls)
    {
      if (!toolCall.Name.Equals("Shell", StringComparison.OrdinalIgnoreCase))
      {
        conversation.AddToolResult(
            toolCall.Id, $"Error: Unknown tool '{toolCall.Name}'. Use the Shell tool.", isError: true);
        continue;
      }

      if (!_activeEngine.IsInitialized)
      {
        conversation.AddToolResult(
            toolCall.Id, "Error: Execution engine not initialized.", isError: true);
        continue;
      }

      try
      {
        using var doc = JsonDocument.Parse(toolCall.ArgumentsJson);
        var root = doc.RootElement;
        var command = root.GetProperty("command").GetString()
            ?? throw new ArgumentException("command is required");

        var result = await _activeEngine.Engine!.ExecuteAsync(
            command, workingDirectory, onOutputLine: null, ct);

        // Format output for LLM
        var output = result.Output;
        if (result.HadErrors && result.ErrorOutput is not null)
        {
          output = string.IsNullOrEmpty(output)
              ? $"Error: {result.ErrorOutput}"
              : $"{output}\n\nErrors:\n{result.ErrorOutput}";
        }

        if (output.Length > 30_000)
        {
          output = string.Concat(output.AsSpan(0, 29_997), "...");
        }

        conversation.AddToolResult(toolCall.Id, output, result.HadErrors);
      }
      catch (OperationCanceledException)
      {
        conversation.AddToolResult(
            toolCall.Id, "Command was cancelled by the user.", isError: false);
        throw;
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        var errorMsg = $"Error executing command: {ex.Message}";
        conversation.AddToolResult(toolCall.Id, errorMsg, isError: true);
      }
    }
  }

  [LoggerMessage(Level = LogLevel.Information, Message = "Delegating to agent '{AgentName}': {Task}")]
  private partial void LogAgentDelegation(string agentName, string task);

  [LoggerMessage(Level = LogLevel.Information, Message = "Agent '{AgentName}' completed successfully")]
  private partial void LogAgentCompleted(string agentName);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Agent '{AgentName}' execution failed")]
  private partial void LogAgentError(string agentName, Exception exception);
}
