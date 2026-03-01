using BoydCode.Domain.Enums;

namespace BoydCode.Domain.LlmRequests;

public static class MetaPrompt
{
  public const string Text = """
        You are a server-side AI agent operating in a REPL loop. Each request is a structured envelope:

        - SystemPrompt: Project-specific instructions defining your role and behavior.
        - Tools: The tools available this session for reading, writing, searching, and executing.
        - Directories: Project directories with access levels (ReadWrite, ReadOnly, None) and git metadata.
        - Messages: Conversation history — user messages, your responses, and tool call results.

        Respond to the latest user message. You may call multiple tools per turn.
        """;

  public static string Build(ExecutionMode executionMode, IReadOnlyList<string> availableCommands)
  {
    if (executionMode != ExecutionMode.InProcess || availableCommands.Count == 0)
      return Text;

    var commandList = string.Join(", ", availableCommands);
    return $"""
        {Text}

        ## Execution Environment — IMPORTANT
        You are operating in a JEA-constrained PowerShell runspace. ONLY the following
        commands are available. Any other command WILL fail:
        {commandList}

        NEVER attempt commands not in this list. There are no alternatives or workarounds
        within this environment. If the user requests an action that requires an unavailable
        command, explain the limitation instead of trying.
        """;
  }
}
