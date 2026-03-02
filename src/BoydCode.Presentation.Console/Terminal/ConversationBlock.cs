using BoydCode.Presentation.Console.Renderables;

namespace BoydCode.Presentation.Console.Terminal;

internal abstract record ConversationBlock;

internal sealed record UserMessageBlock(string Text) : ConversationBlock;

internal sealed record AssistantTextBlock(string Text) : ConversationBlock;

internal sealed record ToolCallConversationBlock(string ToolName, string Preview) : ConversationBlock;

internal sealed record ToolResultConversationBlock(string ToolName, int LineCount, string Duration, bool IsError) : ConversationBlock;

internal sealed record ExpandHintBlock() : ConversationBlock;

internal sealed record TokenUsageBlock(int InputTokens, int OutputTokens) : ConversationBlock;

internal sealed record SeparatorBlock() : ConversationBlock;

internal sealed record SectionBlock(string Title) : ConversationBlock;

internal sealed record StatusMessageBlock(string Text, MessageKind Kind) : ConversationBlock;

internal sealed record PlainTextBlock(string Text) : ConversationBlock;

internal sealed record BannerBlock(BannerData Data) : ConversationBlock;

internal enum MessageKind { Success, Error, Warning, Hint }
