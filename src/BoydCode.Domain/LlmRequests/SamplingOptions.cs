namespace BoydCode.Domain.LlmRequests;

public sealed record SamplingOptions
{
  public float? Temperature { get; init; }
  public float? TopP { get; init; }
  public int? TopK { get; init; }
  public int? MaxOutputTokens { get; init; }
}
