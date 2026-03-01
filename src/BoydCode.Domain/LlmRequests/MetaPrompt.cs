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
}
