using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Permissions;

namespace BoydCode.Domain.Entities;

public sealed class Project
{
  public const string AmbientProjectName = "_default";
  public const string DefaultSystemPrompt = "You are a helpful AI coding assistant. Use the available tools to help the user with their tasks.";

  public string Name { get; }
  public List<ProjectDirectory> Directories { get; set; } = [];
  public string? SystemPrompt { get; set; }
  public string? DockerImage { get; set; }
  public bool RequireContainer { get; set; }
  public ExecutionConfig? Execution { get; set; }
  public PermissionMode? PermissionMode { get; set; }
  public List<PermissionRule>? PermissionRules { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset LastAccessedAt { get; set; }

  public Project(string name)
  {
    Name = name;
    CreatedAt = DateTimeOffset.UtcNow;
    LastAccessedAt = DateTimeOffset.UtcNow;
  }

  public static Project CreateAmbient()
  {
    return new Project(AmbientProjectName);
  }

  public ExecutionConfig BuildExecutionConfig()
  {
    var config = new ExecutionConfig
    {
      Mode = RequireContainer ? ExecutionMode.Container : ExecutionMode.InProcess,
      AllowInProcess = !RequireContainer,
      JeaProfiles = Execution?.JeaProfiles is { Count: > 0 }
            ? [.. Execution.JeaProfiles]
            : [],
    };

    if (DockerImage is not null)
    {
      config.Container = new ContainerConfig { Image = DockerImage };
    }

    return config;
  }
}
