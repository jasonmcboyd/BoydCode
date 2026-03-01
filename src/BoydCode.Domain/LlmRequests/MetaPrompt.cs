using BoydCode.Domain.Enums;

namespace BoydCode.Domain.LlmRequests;

public static class MetaPrompt
{
  public const string Text = """
        You are a server-side AI coding agent running in an agentic loop.

        Each turn you receive:
        - A system prompt containing project-specific instructions and directory context
          (paths, access levels: ReadWrite / ReadOnly / None, and git metadata).
        - A Shell tool for executing commands in the current execution environment.
        - Conversation history: user messages, your prior responses, and tool results
          (correlated to your tool calls by ID).

        Use the Shell tool to execute commands for reading files, writing files, searching,
        and any other operations. The execution environment enforces security boundaries —
        use native shell commands directly. You may invoke the Shell tool multiple times per
        turn. The loop continues until you stop calling tools. Respond to the latest user
        message using the tool and context provided.
        """;

  public static string Build(
      ExecutionMode executionMode,
      IReadOnlyList<string> availableCommands,
      IReadOnlyList<string>? agentNames = null)
  {
    string result;

    if (executionMode == ExecutionMode.Container)
    {
      var shell = availableCommands.Count > 0 ? availableCommands[0] : "sh";
      result = $"""
          {Text}

          ## Execution Environment — IMPORTANT
          Commands execute inside a Docker container via the Shell tool.
          The available shell is: {shell}
          All file paths are container paths. Use them directly in shell commands.
          """;
    }
    else if (executionMode == ExecutionMode.InProcess && availableCommands.Count > 0)
    {
      var commandList = string.Join(", ", availableCommands);
      result = $"""
          {Text}

          ## Execution Environment — IMPORTANT
          Commands execute in a JEA-constrained PowerShell runspace via the Shell tool.
          ONLY the following commands are available. Any other command WILL fail:
          {commandList}

          NEVER attempt commands not in this list. There are no alternatives or workarounds
          within this environment. If the user requests an action that requires an unavailable
          command, explain the limitation instead of trying.
          """;
    }
    else
    {
      result = Text;
    }

    if (agentNames is { Count: > 0 })
    {
      var agentList = string.Join(", ", agentNames);
      result = $"""
          {result}

          ## Agent Delegation
          You have an Agent tool that lets you delegate tasks to specialized sub-agents.
          Each agent runs in its own conversation with Shell access but cannot delegate further.
          Available agents: {agentList}
          Use the Agent tool when a task would benefit from specialized expertise.
          """;
    }

    return result;
  }
}
