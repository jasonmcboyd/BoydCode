namespace BoydCode.Domain.LlmRequests;

public sealed record RequestMetadata
{
  public string? UserId { get; init; }
}
