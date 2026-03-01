using System.Text.Json;
using BoydCode.Application.Interfaces;
using BoydCode.Domain.Configuration;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Permissions;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Persistence.Projects;

/// <summary>
/// Persists projects as individual JSON files under ~/.boydcode/projects/{name}/project.json.
/// Creates an ambient project on first access if it does not already exist.
/// </summary>
public sealed partial class JsonProjectRepository : IProjectRepository
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
  };

  private readonly ILogger<JsonProjectRepository> _logger;

  public JsonProjectRepository(ILogger<JsonProjectRepository> logger)
  {
    _logger = logger;
  }

  public async Task<Project?> LoadAsync(string name, CancellationToken ct = default)
  {
    var filePath = GetProjectFilePath(name);

    if (!File.Exists(filePath))
    {
      if (name == Project.AmbientProjectName)
      {
        var ambient = Project.CreateAmbient();
        await SaveAsync(ambient, ct).ConfigureAwait(false);
        LogProjectSaved(name);
        return ambient;
      }

      return null;
    }

    try
    {
      var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
      var doc = JsonSerializer.Deserialize<ProjectDocument>(json, JsonOptions);

      if (doc is null)
      {
        return null;
      }

      var project = ToDomain(doc);
      LogProjectLoaded(name);
      return project;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      LogProjectLoadFailed(name, ex);
      return null;
    }
  }

  public async Task SaveAsync(Project project, CancellationToken ct = default)
  {
    var filePath = GetProjectFilePath(project.Name);
    var directory = Path.GetDirectoryName(filePath)!;
    Directory.CreateDirectory(directory);

    var doc = ToDocument(project);
    var json = JsonSerializer.Serialize(doc, JsonOptions);

    await File.WriteAllTextAsync(filePath, json, ct).ConfigureAwait(false);
    LogProjectSaved(project.Name);
  }

  public Task DeleteAsync(string name, CancellationToken ct = default)
  {
    var directory = GetProjectDirectory(name);

    if (Directory.Exists(directory))
    {
      Directory.Delete(directory, recursive: true);
      LogProjectDeleted(name);
    }

    return Task.CompletedTask;
  }

  public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken ct = default)
  {
    var projectsRoot = GetProjectsRootDirectory();

    if (!Directory.Exists(projectsRoot))
    {
      return Task.FromResult<IReadOnlyList<string>>([]);
    }

    var names = Directory.GetDirectories(projectsRoot)
        .Select(Path.GetFileName)
        .Where(n => n is not null)
        .Cast<string>()
        .ToList()
        .AsReadOnly();

    return Task.FromResult<IReadOnlyList<string>>(names);
  }

  private static string GetProjectsRootDirectory() =>
      Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
          ".boydcode",
          "projects");

  private static string GetProjectDirectory(string name) =>
      Path.Combine(GetProjectsRootDirectory(), name);

  private static string GetProjectFilePath(string name) =>
      Path.Combine(GetProjectDirectory(name), "project.json");

  private static ProjectDocument ToDocument(Project project)
  {
    var directories = new List<ProjectDirectoryDocument>(project.Directories.Count);

    foreach (var dir in project.Directories)
    {
      directories.Add(new ProjectDirectoryDocument
      {
        Path = dir.Path,
        AccessLevel = dir.AccessLevel.ToString(),
      });
    }

    List<PermissionRuleDocument>? permissionRules = null;

    if (project.PermissionRules is { Count: > 0 })
    {
      permissionRules = new List<PermissionRuleDocument>(project.PermissionRules.Count);

      foreach (var rule in project.PermissionRules)
      {
        permissionRules.Add(new PermissionRuleDocument
        {
          ToolName = rule.ToolName,
          Level = rule.Level.ToString(),
          Description = rule.Description,
        });
      }
    }

    return new ProjectDocument
    {
      Name = project.Name,
      Directories = directories,
      SystemPrompt = project.SystemPrompt,
      DockerImage = project.DockerImage,
      RequireContainer = project.RequireContainer,
      Execution = project.Execution is not null ? ToExecutionDocument(project.Execution) : null,
      PermissionMode = project.PermissionMode?.ToString(),
      PermissionRules = permissionRules,
      CreatedAt = project.CreatedAt,
      LastAccessedAt = project.LastAccessedAt,
    };
  }

  private static ExecutionConfigDocument ToExecutionDocument(ExecutionConfig config)
  {
    var doc = new ExecutionConfigDocument
    {
      Mode = config.Mode.ToString(),
      JeaProfiles = [.. config.JeaProfiles],
      AllowInProcess = config.AllowInProcess,
    };

    if (config.Container is not null)
    {
      doc.Container = new ContainerConfigDocument
      {
        Image = config.Container.Image,
        Network = config.Container.Network,
        Shell = config.Container.Shell,
        Environment = new Dictionary<string, string>(config.Container.Environment),
      };
    }

    return doc;
  }

  private static Project ToDomain(ProjectDocument doc)
  {
    var project = new Project(doc.Name)
    {
      SystemPrompt = doc.SystemPrompt,
      DockerImage = doc.DockerImage,
      RequireContainer = doc.RequireContainer,
      CreatedAt = doc.CreatedAt,
      LastAccessedAt = doc.LastAccessedAt,
    };

    foreach (var dirDoc in doc.Directories)
    {
      var accessLevel = Enum.TryParse<DirectoryAccessLevel>(dirDoc.AccessLevel, out var parsed)
          ? parsed
          : DirectoryAccessLevel.ReadWrite;

      project.Directories.Add(new ProjectDirectory(dirDoc.Path, accessLevel));
    }

    if (doc.Execution is not null)
    {
      project.Execution = ToDomainExecutionConfig(doc.Execution);

      // Backward compat: migrate from Execution block if first-class fields absent
      if (doc.DockerImage is null && doc.Execution.Container is not null)
      {
        project.DockerImage = doc.Execution.Container.Image;
      }

      if (!doc.RequireContainer && doc.Execution.Mode == "Container")
      {
        project.RequireContainer = true;
      }
    }
    else if (doc.JeaConfig is not null)
    {
      // Migration: convert legacy JeaConfig to ExecutionConfig
      project.Execution = MigrateLegacyJeaConfig(doc.JeaConfig);
    }

    if (doc.PermissionMode is not null &&
        Enum.TryParse<PermissionMode>(doc.PermissionMode, out var permMode))
    {
      project.PermissionMode = permMode;
    }

    if (doc.PermissionRules is { Count: > 0 })
    {
      project.PermissionRules = [];

      foreach (var ruleDoc in doc.PermissionRules)
      {
        var level = Enum.TryParse<PermissionLevel>(ruleDoc.Level, out var parsedLevel)
            ? parsedLevel
            : PermissionLevel.Ask;

        project.PermissionRules.Add(new PermissionRule(ruleDoc.ToolName, level, ruleDoc.Description));
      }
    }

    return project;
  }

  private static ExecutionConfig ToDomainExecutionConfig(ExecutionConfigDocument doc)
  {
    var mode = Enum.TryParse<ExecutionMode>(doc.Mode, out var parsed)
        ? parsed
        : ExecutionMode.InProcess;

    var config = new ExecutionConfig
    {
      Mode = mode,
      JeaProfiles = [.. doc.JeaProfiles],
      AllowInProcess = doc.AllowInProcess,
    };

    if (doc.Container is not null)
    {
      config.Container = new ContainerConfig
      {
        Image = doc.Container.Image,
        Network = doc.Container.Network,
        Shell = doc.Container.Shell,
        Environment = new Dictionary<string, string>(doc.Container.Environment),
      };
    }

    return config;
  }

  /// <summary>
  /// Migrates a legacy JeaConfig document to the new ExecutionConfig model.
  /// AllowedCommands, AllowedModules, LanguageMode, EndpointName, and ComputerName
  /// are dropped (moved to JEA profiles in Phase 2).
  /// </summary>
  private static ExecutionConfig MigrateLegacyJeaConfig(LegacyJeaConfigDocument doc)
  {
    // Old ConstrainedRunspace and FullJeaEndpoint both map to InProcess
    return new ExecutionConfig
    {
      Mode = ExecutionMode.InProcess,
      AllowInProcess = true,
    };
  }

  [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded project: {ProjectName}")]
  private partial void LogProjectLoaded(string projectName);

  [LoggerMessage(Level = LogLevel.Information, Message = "Saved project: {ProjectName}")]
  private partial void LogProjectSaved(string projectName);

  [LoggerMessage(Level = LogLevel.Information, Message = "Deleted project: {ProjectName}")]
  private partial void LogProjectDeleted(string projectName);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load project: {ProjectName}")]
  private partial void LogProjectLoadFailed(string projectName, Exception exception);
}
