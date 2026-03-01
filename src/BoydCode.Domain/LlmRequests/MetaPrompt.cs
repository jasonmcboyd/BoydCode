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

  public static string Build(ExecutionMode executionMode, IReadOnlyList<string> availableCommands)
  {
    if (executionMode == ExecutionMode.Container)
    {
      var shell = availableCommands.Count > 0 ? availableCommands[0] : "sh";
      return $"""
          {Text}

          ## Execution Environment — IMPORTANT
          Commands execute inside a Docker container via the Shell tool.
          The available shell is: {shell}
          All file paths are container paths. Use them directly in shell commands.
          """;
    }

    if (executionMode != ExecutionMode.InProcess || availableCommands.Count == 0)
      return Text;

    var commandList = string.Join(", ", availableCommands);
    return $"""
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
}
