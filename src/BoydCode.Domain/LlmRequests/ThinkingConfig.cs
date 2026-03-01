namespace BoydCode.Domain.LlmRequests;

public sealed record ThinkingConfig
{
  public bool Enabled { get; init; }
  public int? BudgetTokens { get; init; }
}
