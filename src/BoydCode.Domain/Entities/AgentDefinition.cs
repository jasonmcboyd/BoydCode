namespace BoydCode.Domain.Entities;

using BoydCode.Domain.Enums;

public sealed class AgentDefinition
{
  public required string Name { get; init; }
  public required string Description { get; init; }
  public required string Instructions { get; init; }
  public AgentScope Scope { get; init; }
  public string? ModelOverride { get; init; }
  public int? MaxTurns { get; init; }
  public string SourcePath { get; init; } = "";
}
