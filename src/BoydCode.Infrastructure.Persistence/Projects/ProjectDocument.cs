namespace BoydCode.Infrastructure.Persistence.Projects;

/// <summary>
/// Flat DTO for JSON serialization of a Project. Avoids coupling the domain model
/// to serialization concerns.
/// </summary>
internal sealed class ProjectDocument
{
  public string Name { get; set; } = "";
  public List<ProjectDirectoryDocument> Directories { get; set; } = [];
  public string? SystemPrompt { get; set; }
  public string? DockerImage { get; set; }
  public bool RequireContainer { get; set; }
  public ExecutionConfigDocument? Execution { get; set; }
  // Legacy: kept for migration from old format (serialized as "jea_config" in JSON)
  public LegacyJeaConfigDocument? JeaConfig { get; set; }
  public string? PermissionMode { get; set; }
  public List<PermissionRuleDocument>? PermissionRules { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset LastAccessedAt { get; set; }
}

internal sealed class ProjectDirectoryDocument
{
  public string Path { get; set; } = "";
  public string AccessLevel { get; set; } = "ReadWrite";
}

internal sealed class ExecutionConfigDocument
{
  public string Mode { get; set; } = "InProcess";
  public ContainerConfigDocument? Container { get; set; }
  public List<string> JeaProfiles { get; set; } = [];
  public bool AllowInProcess { get; set; } = true;
}

internal sealed class ContainerConfigDocument
{
  public string Image { get; set; } = "";
  public bool Network { get; set; } = true;
  public string Shell { get; set; } = "pwsh";
  public Dictionary<string, string> Environment { get; set; } = [];
}

/// <summary>
/// Legacy DTO kept for deserializing old project files that used JeaConfig.
/// Will be migrated to <see cref="ExecutionConfigDocument"/> on load.
/// </summary>
internal sealed class LegacyJeaConfigDocument
{
  public string Mode { get; set; } = "ConstrainedRunspace";
  public string? EndpointName { get; set; }
  public string? ComputerName { get; set; }
  public List<string> AllowedCommands { get; set; } = [];
  public List<string> AllowedModules { get; set; } = [];
  public string LanguageMode { get; set; } = "ConstrainedLanguage";
}

internal sealed class PermissionRuleDocument
{
  public string ToolName { get; set; } = "";
  public string Level { get; set; } = "Ask";
  public string? Description { get; set; }
}
