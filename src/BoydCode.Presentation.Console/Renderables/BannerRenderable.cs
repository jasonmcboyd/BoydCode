using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace BoydCode.Presentation.Console.Renderables;

internal static class BannerRenderable
{
  private const string DefaultProjectName = "_default";

  private static readonly string[] BoydArt =
  [
    @"  ██████╗  ██████╗ ██╗   ██╗██████╗ ",
    @"  ██╔══██╗██╔═══██╗╚██╗ ██╔╝██╔══██╗",
    @"  ██████╔╝██║   ██║ ╚████╔╝ ██║  ██║",
    @"  ██╔══██╗██║   ██║  ╚██╔╝  ██║  ██║",
    @"  ██████╔╝╚██████╔╝   ██║   ██████╔╝",
    @"  ╚═════╝  ╚═════╝    ╚═╝   ╚═════╝ ",
  ];

  private static readonly string[] CodeArt =
  [
    @"                   ██████╗  ██████╗ ██████╗ ███████╗",
    @"                  ██╔════╝ ██╔═══██╗██╔══██╗██╔════╝",
    @"                  ██║      ██║   ██║██║  ██║█████╗  ",
    @"                  ██║      ██║   ██║██║  ██║██╔══╝  ",
    @"                  ╚██████╗ ╚██████╔╝██████╔╝███████╗",
    @"                   ╚═════╝  ╚═════╝ ╚═════╝ ╚══════╝",
  ];

  private static readonly string[] SidebarWide =
  [
    "Users:      1",
    "Revenue:    $0",
    "Valuation:  $0,000,000,000",
    "Commas:     tres",
    "Status:     pre-unicorn",
  ];

  private static readonly string[] SidebarNarrow =
  [
    "Users:  1",
    "Revenue: $0",
    "Valuation: $0B",
    "Commas: tres",
    "Status: pre",
  ];

  public static IRenderable Build(BannerData data)
  {
    // Issue #4: Accessible mode — structured text, no art, no color, no Unicode symbols
    if (data.Accessible)
    {
      return BuildAccessible(data);
    }

    if (data.TerminalHeight < 10)
    {
      return BuildFallback(data);
    }

    var isNarrow = data.TerminalWidth < 80;
    var isMinimal = data.TerminalHeight is >= 10 and < 15;
    var sections = new List<IRenderable>();

    // Issue #9: Skip leading blank line in minimal tier (every row is precious)
    if (!isMinimal)
    {
      sections.Add(new Text(""));
    }

    // Issue #5: Fall back to compact wordmark when Unicode is unavailable
    var canUseAsciiArt = data.SupportsUnicode && !isNarrow;
    var useFull = data.TerminalHeight >= 30 && canUseAsciiArt;
    var useCompact = data.TerminalHeight >= 15 && !useFull;
    // Minimal tier: height 10-14 — no wordmark, no rule, no hint

    if (useFull)
    {
      sections.Add(BuildFullWordmark(data));
    }
    else if (useCompact)
    {
      sections.Add(BuildCompactWordmark(data));
    }

    // Rule separator only for full and compact tiers
    if (useFull || useCompact)
    {
      sections.Add(new Text(""));
      sections.Add(new Rule().RuleStyle("dim"));
    }

    // Info grid — Issue #2: narrow width uses single-column grid
    if (!isMinimal)
    {
      sections.Add(new Text(""));
    }

    if (isNarrow)
    {
      sections.Add(BuildCompactInfoGrid(data));
    }
    else
    {
      sections.Add(BuildInfoGrid(data));
    }

    // Blank line between info grid and status footer (per spec mockups)
    sections.Add(new Text(""));

    // Status footer — Issue #2: simplified at narrow width
    if (isNarrow)
    {
      sections.Add(BuildCompactStatusFooter(data.IsConfigured));
    }
    else
    {
      sections.Add(BuildStatusFooter(data.IsConfigured, data.ExecutionMode));
    }

    // Hint line — Issue #7: only for full/compact tiers, width >= 80, when configured
    if (data.IsConfigured && (useFull || useCompact) && !isNarrow)
    {
      sections.Add(new Text(""));
      sections.Add(BuildHintLine(data.TerminalWidth));
    }

    // Resume notice — rendered after hint line
    if (data.IsResumedSession && data.ResumeSessionId is not null && !isMinimal)
    {
      if (!(data.IsConfigured && (useFull || useCompact) && !isNarrow))
      {
        sections.Add(new Text(""));
      }
      var shortId = data.ResumeSessionId.Length > 8
        ? data.ResumeSessionId[..8]
        : data.ResumeSessionId;
      var timestamp = data.ResumeTimestamp?.LocalDateTime
        .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";
      sections.Add(new Markup(
        $"  [dim italic]Resumed session {Markup.Escape(shortId)} ({data.ResumeMessageCount} messages from {Markup.Escape(timestamp)})[/]"));
    }

    // Issue #9: Skip trailing blank line in minimal tier
    if (!isMinimal)
    {
      sections.Add(new Text(""));
    }

    return new Rows(sections);
  }

  // Issue #4: Accessible mode — structured text block per UX spec
  private static Rows BuildAccessible(BannerData data)
  {
    var lines = new List<IRenderable>
    {
      new Text($"BOYDCODE v{data.Version}"),
      new Text(""),
    };

    var projectDisplay = FormatProjectName(data.ProjectName);
    lines.Add(new Text($"Provider: {data.ProviderName}"));
    lines.Add(new Text($"Model: {data.ModelName}"));
    lines.Add(new Text($"Project: {projectDisplay}"));
    lines.Add(new Text($"Engine: {data.ExecutionMode}"));

    if (data.DockerImage is not null)
    {
      lines.Add(new Text($"Docker: {data.DockerImage}"));
    }

    lines.Add(new Text($"Directory: {data.WorkingDirectory}"));

    foreach (var repo in data.GitRepositories)
    {
      var branchText = repo.Branch is not null ? repo.Branch : repo.RepoRoot;
      lines.Add(new Text($"Git: {branchText}"));
    }

    lines.Add(new Text(""));

    if (data.IsConfigured)
    {
      var engineDesc = data.ExecutionMode == "Container"
        ? "Commands execute inside a Docker container."
        : "Commands run in a constrained PowerShell runspace.";
      lines.Add(new Text($"[OK] Ready. {engineDesc}"));
    }
    else
    {
      lines.Add(new Text("Not configured. Run /provider setup or pass --api-key"));
    }

    if (data.IsConfigured && data.TerminalHeight >= 15)
    {
      lines.Add(new Text(""));
      lines.Add(new Text("Type a message to start, or /help for available commands."));
    }

    if (data.IsResumedSession && data.ResumeSessionId is not null)
    {
      lines.Add(new Text(""));
      var shortId = data.ResumeSessionId.Length > 8 ? data.ResumeSessionId[..8] : data.ResumeSessionId;
      var timestamp = data.ResumeTimestamp?.LocalDateTime
        .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";
      lines.Add(new Text($"Resumed session {shortId} ({data.ResumeMessageCount} messages from {timestamp})"));
    }

    return new Rows(lines);
  }

  private static Markup BuildFallback(BannerData data)
  {
    if (data.IsConfigured)
    {
      return new Markup(
        $"  [green]\u2713[/] [bold cyan]BoydCode[/] [dim]ready ({Markup.Escape(data.ProviderName)}, {Markup.Escape(data.ModelName)}, {Markup.Escape(FormatProjectName(data.ProjectName))})[/]");
    }

    return new Markup(
      $"  [bold cyan]BoydCode:[/] [yellow bold]Not configured.[/] [dim]Run[/] [bold]/provider setup[/] [dim]or pass[/] [bold]--api-key[/]");
  }

  private static Rows BuildFullWordmark(BannerData data)
  {
    // Cap sidebar layout width to ~80 so the sidebar sits close to the art (~77 chars
    // total) rather than being right-justified across the full terminal width.
    // Spec: "The BOYD art is ~48 characters. The sidebar adds ~30 characters. With
    // 2-space indent and padding, the total line width is ~77 characters."
    var width = Math.Min(data.TerminalWidth, 80);
    var sidebar = data.TerminalWidth >= 100 ? SidebarWide : SidebarNarrow;

    var lines = new List<IRenderable>();

    // Use a fixed gap from the longest art line so the sidebar is left-aligned
    // at a consistent column (not ragged from per-line right-justification).
    var maxArtWidth = BoydArt.Max(a => a.Length);
    var gap = Math.Max(2, width - maxArtWidth - sidebar.Max(s => s.Length) - 4);

    for (var i = 0; i < BoydArt.Length; i++)
    {
      var line = $"[bold cyan]{Markup.Escape(BoydArt[i])}[/]";
      if (i < sidebar.Length)
      {
        var artPadding = new string(' ', maxArtWidth - BoydArt[i].Length + gap);
        line += $"[dim]{artPadding}{Markup.Escape(sidebar[i])}[/]";
      }
      lines.Add(new Markup(line));
    }

    foreach (var row in CodeArt)
    {
      lines.Add(new Markup($"[bold blue]{Markup.Escape(row)}[/]"));
    }

    lines.Add(new Markup($"  [dim]v{Markup.Escape(data.Version)}  Artificial Intelligence, Personal Edition[/]"));

    return new Rows(lines);
  }

  private static Markup BuildCompactWordmark(BannerData data)
  {
    if (data.TerminalWidth >= 80)
    {
      return new Markup($"  [bold cyan]BOYD[/][bold blue]CODE[/]  [dim]v{Markup.Escape(data.Version)}  AI Coding Assistant[/]");
    }

    return new Markup($"  [bold cyan]BOYD[/][bold blue]CODE[/]  [dim]v{Markup.Escape(data.Version)}[/]");
  }

  private static IRenderable BuildInfoGrid(BannerData data)
  {
    var grid = SpectreHelpers.InfoGrid();

    // Issue #3: Display _default as (default) with dim styling
    var projectDisplay = data.ProjectName == DefaultProjectName
      ? "[dim](default)[/]"
      : $"[cyan]{Markup.Escape(data.ProjectName)}[/]";

    grid.AddRow(
      new Markup($"[dim]Provider[/]"),
      new Markup($"[cyan]{Markup.Escape(data.ProviderName)}[/]"),
      new Markup($"[dim]Project[/]"),
      new Markup(projectDisplay));
    SpectreHelpers.AddInfoRow(grid, "Model", data.ModelName, "Engine", data.ExecutionMode);

    if (data.DockerImage is not null)
    {
      SpectreHelpers.AddInfoRow(grid, "Docker", data.DockerImage);
    }

    SpectreHelpers.AddInfoRow(grid, "cwd", data.WorkingDirectory);

    foreach (var repo in data.GitRepositories)
    {
      if (repo.Branch is not null)
      {
        grid.AddRow(
          new Markup("[dim]Git[/]"),
          new Markup($"[cyan]{Markup.Escape(repo.RepoRoot)}[/] [dim]({Markup.Escape(repo.Branch)})[/]"),
          new Markup(""),
          new Markup(""));
      }
      else
      {
        SpectreHelpers.AddInfoRow(grid, "Git", repo.RepoRoot);
      }
    }

    return ConstrainGridWidth(grid, data.TerminalWidth);
  }

  // Issue #2: Narrow-width (< 80 col) single-column info grid
  private static IRenderable BuildCompactInfoGrid(BannerData data)
  {
    var grid = SpectreHelpers.CompactInfoGrid();

    SpectreHelpers.AddCompactInfoRow(grid, "Provider", data.ProviderName);
    SpectreHelpers.AddCompactInfoRow(grid, "Model", data.ModelName);

    // Issue #3: Display _default as (default) with dim styling
    if (data.ProjectName == DefaultProjectName)
    {
      grid.AddRow(
        new Markup($"[dim]Project[/]"),
        new Markup("[dim](default)[/]"));
    }
    else
    {
      SpectreHelpers.AddCompactInfoRow(grid, "Project", data.ProjectName);
    }

    SpectreHelpers.AddCompactInfoRow(grid, "Engine", data.ExecutionMode);

    if (data.DockerImage is not null)
    {
      SpectreHelpers.AddCompactInfoRow(grid, "Docker", data.DockerImage);
    }

    SpectreHelpers.AddCompactInfoRow(grid, "cwd", data.WorkingDirectory);

    foreach (var repo in data.GitRepositories)
    {
      if (repo.Branch is not null)
      {
        grid.AddRow(
          new Markup("[dim]Git[/]"),
          new Markup($"[cyan]{Markup.Escape(repo.RepoRoot)}[/] [dim]({Markup.Escape(repo.Branch)})[/]"));
      }
      else
      {
        SpectreHelpers.AddCompactInfoRow(grid, "Git", repo.RepoRoot);
      }
    }

    return ConstrainGridWidth(grid, data.TerminalWidth);
  }

  private static Markup BuildStatusFooter(bool isConfigured, string executionMode)
  {
    if (isConfigured)
    {
      var engineDesc = executionMode == "Container"
        ? "Commands execute inside a Docker container."
        : "Commands run in a constrained PowerShell runspace.";
      return new Markup($"  [green]\u2713[/] [dim]Ready  {Markup.Escape(engineDesc)}[/]");
    }

    return new Markup(
      $"  [yellow bold]Not configured[/]  [dim]Run[/] [bold]/provider setup[/] [dim]or pass[/] [bold]--api-key[/]");
  }

  // Issue #2: Simplified status footer for narrow width — checkmark and Ready only
  private static Markup BuildCompactStatusFooter(bool isConfigured)
  {
    if (isConfigured)
    {
      return new Markup(" [green]\u2713[/] [dim]Ready[/]");
    }

    return new Markup(
      " [yellow bold]Not configured[/]");
  }

  // Issue #7: Hint line text corrected to match UX spec, width-aware
  private static Markup BuildHintLine(int width)
  {
    if (width >= 120)
      return new Markup("  [dim italic]Type a message to start, or /help for available commands.[/]");
    if (width >= 80)
      return new Markup("  [dim italic]Type a message to start, or /help for commands.[/]");
    return new Markup("  [dim italic]Type a message, or /help[/]");
  }

  // Cap the info grid width so it stays left-aligned at wide terminals.
  // Grid internally calls Table.Expand(), which fills available width and
  // creates huge gaps between columns at 190+ columns. Wrapping in a
  // zero-padding, no-border Panel with an explicit Width constrains it.
  private const int MaxGridWidth = 120;

  private static IRenderable ConstrainGridWidth(Grid grid, int terminalWidth)
  {
    if (terminalWidth <= MaxGridWidth)
    {
      return grid;
    }

    var panel = new Panel(grid);
    panel.Border = BoxBorder.None;
    panel.Padding = new Padding(0, 0, 0, 0);
    panel.Width = MaxGridWidth;
    return panel;
  }

  // Issue #3: Format project name — _default becomes (default)
  private static string FormatProjectName(string projectName)
  {
    return projectName == DefaultProjectName ? "(default)" : projectName;
  }
}
