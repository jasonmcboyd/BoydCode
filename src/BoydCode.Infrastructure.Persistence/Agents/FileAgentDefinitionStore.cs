using BoydCode.Application.Interfaces;
using BoydCode.Domain.Entities;
using BoydCode.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BoydCode.Infrastructure.Persistence.Agents;

public sealed partial class FileAgentDefinitionStore : IAgentDefinitionStore
{
  private static readonly string UserAgentDirectory =
      Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
          ".boydcode",
          "agents");

  private const string ProjectAgentSubdirectory = ".boydcode/agents";

  private readonly ILogger<FileAgentDefinitionStore> _logger;

  public FileAgentDefinitionStore(ILogger<FileAgentDefinitionStore> logger)
  {
    _logger = logger;
  }

  public async Task<IReadOnlyList<AgentDefinition>> LoadAllAsync(
      string? projectDirectory = null, CancellationToken ct = default)
  {
    var agents = new List<AgentDefinition>();

    // Load user-scoped agents first
    await LoadFromDirectoryAsync(agents, UserAgentDirectory, AgentScope.User, ct);

    // Then project-scoped (callers can override by name later)
    if (projectDirectory is not null)
    {
      var projectAgentDir = Path.Combine(projectDirectory, ProjectAgentSubdirectory);
      await LoadFromDirectoryAsync(agents, projectAgentDir, AgentScope.Project, ct);
    }

    return agents.AsReadOnly();
  }

  private async Task LoadFromDirectoryAsync(
      List<AgentDefinition> agents, string directory, AgentScope scope, CancellationToken ct)
  {
    if (!Directory.Exists(directory))
    {
      return;
    }

    foreach (var filePath in Directory.GetFiles(directory, "*.md"))
    {
      try
      {
        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var name = Path.GetFileNameWithoutExtension(filePath);
        var agent = Parse(name, content, scope, filePath);
        agents.Add(agent);
        LogAgentLoaded(name, scope);
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        LogAgentLoadFailed(filePath, ex);
      }
    }
  }

  public static AgentDefinition Parse(string name, string content, AgentScope scope, string sourcePath = "")
  {
    string? description = null;
    string? model = null;
    int? maxTurns = null;
    string instructions;

    var lines = content.Split('\n');
    var lineIndex = 0;

    // Check for frontmatter
    if (lines.Length > 0 && lines[0].Trim() == "---")
    {
      lineIndex = 1;
      var foundEnd = false;

      while (lineIndex < lines.Length)
      {
        var line = lines[lineIndex].Trim();
        lineIndex++;

        if (line == "---")
        {
          foundEnd = true;
          break;
        }

        var colonIndex = line.IndexOf(':');
        if (colonIndex <= 0) continue;

        var key = line[..colonIndex].Trim().ToLowerInvariant();
        var value = line[(colonIndex + 1)..].Trim();

        switch (key)
        {
          case "description":
            description = value;
            break;
          case "model":
            model = string.IsNullOrEmpty(value) ? null : value;
            break;
          case "max_turns":
            maxTurns = int.TryParse(value, out var parsed) ? parsed : null;
            break;
        }
      }

      if (!foundEnd)
      {
        // No closing ---, treat entire content as instructions and discard partial metadata
        lineIndex = 0;
        description = null;
        model = null;
        maxTurns = null;
      }
    }

    // Everything after frontmatter is instructions
    instructions = string.Join("\n", lines[lineIndex..]).Trim();

    return new AgentDefinition
    {
      Name = name,
      Description = description ?? "",
      Instructions = instructions,
      Scope = scope,
      ModelOverride = model,
      MaxTurns = maxTurns,
      SourcePath = sourcePath,
    };
  }

  [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded agent definition: {AgentName} ({Scope})")]
  private partial void LogAgentLoaded(string agentName, AgentScope scope);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load agent definition from {FilePath}")]
  private partial void LogAgentLoadFailed(string filePath, Exception exception);
}
