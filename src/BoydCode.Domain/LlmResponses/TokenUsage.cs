namespace BoydCode.Domain.LlmResponses;

public sealed record TokenUsage(int InputTokens, int OutputTokens)
{
  public int TotalTokens => InputTokens + OutputTokens;
}
