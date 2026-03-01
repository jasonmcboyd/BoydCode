namespace BoydCode.Domain.LlmResponses;

public abstract record StreamChunk;

public sealed record TextChunk(string Text) : StreamChunk;

public sealed record ToolCallChunk(string CallId, string Name, string ArgumentsJson) : StreamChunk;

public sealed record CompletionChunk(string StopReason, TokenUsage Usage) : StreamChunk;
