using BoydCode.Application.Interfaces;
using BoydCode.Domain.Entities;

namespace BoydCode.Application.Services;

public sealed class ProjectResolver
{
  private readonly IProjectRepository _projectRepository;

  public ProjectResolver(IProjectRepository projectRepository)
  {
    _projectRepository = projectRepository;
  }

  public async Task<Project> ResolveAsync(
      string? explicitProjectName,
      string currentWorkingDirectory,
      CancellationToken ct = default)
  {
    // 1. Explicit --project flag
    if (!string.IsNullOrWhiteSpace(explicitProjectName))
    {
      var explicitProject = await _projectRepository.LoadAsync(explicitProjectName, ct);
      if (explicitProject is not null)
      {
        explicitProject.LastAccessedAt = DateTimeOffset.UtcNow;
        return explicitProject;
      }

      // Project not found -- fall through to ambient
    }

    // 2. CWD matching against configured project directories
    var projectNames = await _projectRepository.ListNamesAsync(ct);
    foreach (var name in projectNames)
    {
      if (string.Equals(name, Project.AmbientProjectName, StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      var project = await _projectRepository.LoadAsync(name, ct);
      if (project is null)
      {
        continue;
      }

      foreach (var dir in project.Directories)
      {
        var normalizedDir = Path.GetFullPath(dir.Path);
        if (!normalizedDir.EndsWith(Path.DirectorySeparatorChar))
        {
          normalizedDir += Path.DirectorySeparatorChar;
        }

        if (currentWorkingDirectory.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentWorkingDirectory, normalizedDir.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
          project.LastAccessedAt = DateTimeOffset.UtcNow;
          return project;
        }
      }
    }

    // 3. Ambient fallback
    var ambient = await _projectRepository.LoadAsync(Project.AmbientProjectName, ct);
    if (ambient is null)
    {
      ambient = Project.CreateAmbient();
      await _projectRepository.SaveAsync(ambient, ct);
    }

    ambient.LastAccessedAt = DateTimeOffset.UtcNow;
    return ambient;
  }
}
