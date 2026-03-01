using BoydCode.Domain.ContentBlocks;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace BoydCode.Application.Tests;

public sealed class EvictionContextCompactorTests
{
  private readonly EvictionContextCompactor _sut = new();

  // Token estimation: chars / 4
  // A string of length 400 produces 100 tokens.
  // Helper to create a string that estimates to exactly the given token count.
  private static string TextOfTokens(int tokens) => new('x', tokens * 4);

  [Fact]
  public async Task CompactAsync_EmptyConversation_ReturnsEmpty()
  {
    // Arrange
    var conversation = new Conversation();

    // Act
    var result = await _sut.CompactAsync(conversation, targetTokenCount: 1000);

    // Assert
    result.Messages.Should().BeEmpty();
  }

  [Fact]
  public async Task CompactAsync_UnderBudget_KeepsAllMessages()
  {
    // Arrange
    var conversation = new Conversation();
    // 50 tokens each, 3 messages = 150 tokens total, well under 1000 budget
    conversation.AddUserMessage(TextOfTokens(50));
    conversation.AddAssistantMessage(TextOfTokens(50));
    conversation.AddUserMessage(TextOfTokens(50));

    // Act
    var result = await _sut.CompactAsync(conversation, targetTokenCount: 1000);

    // Assert
    result.Messages.Should().HaveCount(3);
    // First message should be the original user message, not a compaction notice
    result.Messages[0].Role.Should().Be(MessageRole.User);
    var firstBlock = result.Messages[0].Content[0].Should().BeOfType<TextBlock>().Subject;
    firstBlock.Text.Should().Be(TextOfTokens(50));
  }

  [Fact]
  public async Task CompactAsync_OverBudget_EvictsOldestKeepsNewest()
  {
    // Arrange
    var conversation = new Conversation();
    // 3 standalone messages of 100 tokens each = 300 tokens total
    conversation.AddUserMessage(TextOfTokens(100));       // oldest — should be evicted
    conversation.AddAssistantMessage(TextOfTokens(100));  // middle — should be evicted
    conversation.AddUserMessage(TextOfTokens(100));       // newest — should be kept

    // Budget of 150 tokens: only the newest group (100 tokens) fits.
    // The compaction notice is prepended, so result has notice + newest message.

    // Act
    var result = await _sut.CompactAsync(conversation, targetTokenCount: 150);

    // Assert — notice + 1 kept message = 2 messages
    result.Messages.Should().HaveCount(2);

    // First message is the compaction notice
    result.Messages[0].Role.Should().Be(MessageRole.User);
    var noticeBlock = result.Messages[0].Content[0].Should().BeOfType<TextBlock>().Subject;
    noticeBlock.Text.Should().Contain("compacted to manage context window size");

    // Second message is the newest user message
    result.Messages[1].Role.Should().Be(MessageRole.User);
    var keptBlock = result.Messages[1].Content[0].Should().BeOfType<TextBlock>().Subject;
    keptBlock.Text.Should().Be(TextOfTokens(100));
  }

  [Fact]
  public async Task CompactAsync_PreservesToolCallResultPairs()
  {
    // Arrange
    // Build a conversation with a tool-call group in the middle:
    //   msg 0: user text (50 tokens)
    //   msg 1: assistant with 1 ToolUseBlock (50 tokens)
    //   msg 2: user ToolResultBlock (50 tokens)
    //   msg 3: user text (50 tokens)
    //
    // The tool group (msgs 1+2) = 100 tokens.
    // Budget = 160 tokens: walking backward, msg 3 (50 tokens) fits,
    //   then the tool group (100 tokens) fits (50+100=150 <= 160),
    //   then msg 0 would push to 200 > 160 — evicted.

    var conversation = new Conversation();
    conversation.AddUserMessage(TextOfTokens(50)); // msg 0 — standalone group

    // Assistant message with a tool call: Name(10 chars) + ArgumentsJson(190 chars) = 200 chars = 50 tokens
    var toolUseBlock = new ToolUseBlock("tool-1", new string('n', 10), new string('a', 190));
    conversation.AddAssistantMessage([toolUseBlock]); // msg 1

    // Tool result: 200 chars = 50 tokens
    conversation.AddToolResult("tool-1", TextOfTokens(50)); // msg 2

    conversation.AddUserMessage(TextOfTokens(50)); // msg 3 — standalone group

    // Act
    var result = await _sut.CompactAsync(conversation, targetTokenCount: 160);

    // Assert — notice + tool group (2 msgs) + standalone msg 3 = 4 messages
    result.Messages.Should().HaveCount(4);

    // First is the compaction notice
    var noticeBlock = result.Messages[0].Content[0].Should().BeOfType<TextBlock>().Subject;
    noticeBlock.Text.Should().Contain("compacted");

    // Second is the assistant with ToolUseBlock (kept as group)
    result.Messages[1].Role.Should().Be(MessageRole.Assistant);
    result.Messages[1].Content.Should().ContainSingle()
      .Which.Should().BeOfType<ToolUseBlock>()
      .Which.Id.Should().Be("tool-1");

    // Third is the tool result (kept with its assistant message)
    result.Messages[2].Role.Should().Be(MessageRole.User);
    result.Messages[2].Content.Should().ContainSingle()
      .Which.Should().BeOfType<ToolResultBlock>()
      .Which.ToolUseId.Should().Be("tool-1");

    // Fourth is the standalone user message
    result.Messages[3].Role.Should().Be(MessageRole.User);
    var lastBlock = result.Messages[3].Content[0].Should().BeOfType<TextBlock>().Subject;
    lastBlock.Text.Should().Be(TextOfTokens(50));
  }

  [Fact]
  public async Task CompactAsync_PrependsCompactionNotice_WhenEvicted()
  {
    // Arrange
    var conversation = new Conversation();
    conversation.AddUserMessage(TextOfTokens(100));      // will be evicted
    conversation.AddAssistantMessage(TextOfTokens(100)); // will be kept

    // Budget = 120: only the newest group (100 tokens) fits, oldest evicted.

    // Act
    var result = await _sut.CompactAsync(conversation, targetTokenCount: 120);

    // Assert
    result.Messages.Should().HaveCount(2); // notice + kept message

    var notice = result.Messages[0];
    notice.Role.Should().Be(MessageRole.User);
    notice.Content.Should().ContainSingle()
      .Which.Should().BeOfType<TextBlock>()
      .Which.Text.Should().Be(EvictionContextCompactor.CompactionNotice);
  }

  [Fact]
  public async Task CompactAsync_NoCompactionNotice_WhenAllFit()
  {
    // Arrange
    var conversation = new Conversation();
    conversation.AddUserMessage(TextOfTokens(50));
    conversation.AddAssistantMessage(TextOfTokens(50));

    // Budget = 1000: 100 tokens total, well under budget.

    // Act
    var result = await _sut.CompactAsync(conversation, targetTokenCount: 1000);

    // Assert — all original messages kept, no notice prepended
    result.Messages.Should().HaveCount(2);
    result.Messages[0].Role.Should().Be(MessageRole.User);
    result.Messages[0].Content[0].Should().BeOfType<TextBlock>()
      .Which.Text.Should().Be(TextOfTokens(50));
    result.Messages[1].Role.Should().Be(MessageRole.Assistant);
  }

  [Fact]
  public async Task CompactAsync_AlwaysKeepsAtLeastOneGroup()
  {
    // Arrange — single message that exceeds the budget
    var conversation = new Conversation();
    conversation.AddUserMessage(TextOfTokens(500)); // 500 tokens, far over budget

    // Act — budget of 10 tokens, but the single group must be kept
    var result = await _sut.CompactAsync(conversation, targetTokenCount: 10);

    // Assert — the one message is kept even though it exceeds budget
    result.Messages.Should().ContainSingle();
    result.Messages[0].Role.Should().Be(MessageRole.User);
    result.Messages[0].Content[0].Should().BeOfType<TextBlock>()
      .Which.Text.Should().Be(TextOfTokens(500));
  }

  [Fact]
  public async Task CompactAsync_MultipleToolCallGroups_EvictsWholeGroups()
  {
    // Arrange — two tool-call groups and a trailing user message.
    //
    // Group A (oldest): assistant(50) + toolResult(50) = 100 tokens
    // Group B (middle): assistant(50) + toolResult(50) = 100 tokens
    // Group C (newest): user text(50)                  =  50 tokens
    //
    // Budget = 160: walking backward:
    //   Group C (50) fits (50 <= 160)
    //   Group B (100) fits (150 <= 160)
    //   Group A (100) would push to 250 > 160 — evicted entirely
    //
    // Result: notice + Group B (2 msgs) + Group C (1 msg) = 4 messages

    var conversation = new Conversation();

    // Group A — tool call group
    var toolUseA = new ToolUseBlock("tool-a", new string('n', 10), new string('a', 190));
    conversation.AddAssistantMessage([toolUseA]);
    conversation.AddToolResult("tool-a", TextOfTokens(50));

    // Group B — tool call group
    var toolUseB = new ToolUseBlock("tool-b", new string('n', 10), new string('a', 190));
    conversation.AddAssistantMessage([toolUseB]);
    conversation.AddToolResult("tool-b", TextOfTokens(50));

    // Group C — standalone user message
    conversation.AddUserMessage(TextOfTokens(50));

    // Act
    var result = await _sut.CompactAsync(conversation, targetTokenCount: 160);

    // Assert — notice + Group B (assistant + tool result) + Group C (user)
    result.Messages.Should().HaveCount(4);

    // Compaction notice
    result.Messages[0].Role.Should().Be(MessageRole.User);
    result.Messages[0].Content[0].Should().BeOfType<TextBlock>()
      .Which.Text.Should().Contain("compacted");

    // Group B — assistant with ToolUseBlock for tool-b
    result.Messages[1].Role.Should().Be(MessageRole.Assistant);
    result.Messages[1].Content.Should().ContainSingle()
      .Which.Should().BeOfType<ToolUseBlock>()
      .Which.Id.Should().Be("tool-b");

    // Group B — tool result for tool-b
    result.Messages[2].Role.Should().Be(MessageRole.User);
    result.Messages[2].Content.Should().ContainSingle()
      .Which.Should().BeOfType<ToolResultBlock>()
      .Which.ToolUseId.Should().Be("tool-b");

    // Group C — standalone user message
    result.Messages[3].Role.Should().Be(MessageRole.User);
    result.Messages[3].Content[0].Should().BeOfType<TextBlock>()
      .Which.Text.Should().Be(TextOfTokens(50));

    // Verify Group A was fully evicted — no reference to tool-a
    result.Messages.SelectMany(m => m.Content)
      .OfType<ToolUseBlock>()
      .Should().NotContain(b => b.Id == "tool-a");
    result.Messages.SelectMany(m => m.Content)
      .OfType<ToolResultBlock>()
      .Should().NotContain(b => b.ToolUseId == "tool-a");
  }
}
