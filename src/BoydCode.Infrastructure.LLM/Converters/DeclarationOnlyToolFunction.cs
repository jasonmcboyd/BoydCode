using System.Text.Json;
using Microsoft.Extensions.AI;

namespace BoydCode.Infrastructure.LLM.Converters;

/// <summary>
/// A declaration-only tool function that describes a tool's name, description, and parameter schema
/// to an LLM provider without supporting invocation. We intercept tool calls ourselves for
/// permissions, JEA routing, hooks, and UI feedback rather than using FunctionInvokingChatClient.
/// </summary>
internal sealed class DeclarationOnlyToolFunction : AIFunctionDeclaration
{
  public DeclarationOnlyToolFunction(string name, string description, JsonElement jsonSchema)
  {
    Name = name;
    Description = description;
    JsonSchema = jsonSchema;
  }

  public override string Name { get; }

  public override string Description { get; }

  public override JsonElement JsonSchema { get; }
}
