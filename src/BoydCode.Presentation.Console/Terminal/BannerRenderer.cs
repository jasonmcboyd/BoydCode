using System.Globalization;
using BoydCode.Presentation.Console.Renderables;
using Terminal.Gui.ViewBase;

namespace BoydCode.Presentation.Console.Terminal;

internal static class BannerRenderer
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

  public static int MeasureBanner(BannerData data, int width)
  {
    if (data.Accessible)
    {
      return MeasureAccessible(data);
    }

    if (data.TerminalHeight < Theme.Layout.MinimalHeightThreshold)
    {
      return 1; // Fallback: single line
    }

    var height = 0;
    var isNarrow = width < Theme.Layout.StandardWidth;
    var isMinimal = data.TerminalHeight is >= 10 and < 15;
    var canUseAsciiArt = data.SupportsUnicode && !isNarrow;
    var useFull = data.TerminalHeight >= Theme.Layout.FullHeightThreshold && canUseAsciiArt;
    var useCompact = data.TerminalHeight >= Theme.Layout.CompactHeightThreshold && !useFull;

    // Leading blank (skip in minimal)
    if (!isMinimal) height++;

    // Wordmark
    if (useFull)
    {
      height += BoydArt.Length; // BOYD art
      height += CodeArt.Length; // CODE art
      height++; // Version line
    }
    else if (useCompact)
    {
      height++; // Compact wordmark line
    }

    // Rule separator (full/compact only)
    if (useFull || useCompact)
    {
      height += 2; // blank + rule
    }

    // Info grid spacing
    if (!isMinimal) height++;

    // Info rows
    if (isNarrow)
    {
      height += 4; // Provider, Model, Project, Engine (one per row)
    }
    else
    {
      height += 2; // Provider/Project + Model/Engine (paired rows)
    }
    if (data.DockerImage is not null) height++;
    height++; // cwd
    height += data.GitRepositories.Count;

    // Blank between info and status
    height++;

    // Status footer
    height++;

    // Hint line (full/compact, wide, configured)
    if (data.IsConfigured && (useFull || useCompact) && !isNarrow)
    {
      height += 2; // blank + hint
    }

    // Resume notice
    if (data.IsResumedSession && data.ResumeSessionId is not null && !isMinimal)
    {
      if (!(data.IsConfigured && (useFull || useCompact) && !isNarrow))
      {
        height++; // blank before resume
      }
      height++; // resume line
    }

    // Trailing blank (skip in minimal)
    if (!isMinimal) height++;

    return Math.Max(height, 1);
  }

  public static void DrawBanner(View view, BannerData data, int y, int width)
  {
    if (data.Accessible)
    {
      DrawAccessible(view, data, y, width);
      return;
    }

    if (data.TerminalHeight < Theme.Layout.MinimalHeightThreshold)
    {
      DrawFallback(view, data, y, width);
      return;
    }

    var isNarrow = width < Theme.Layout.StandardWidth;
    var isMinimal = data.TerminalHeight is >= 10 and < 15;
    var canUseAsciiArt = data.SupportsUnicode && !isNarrow;
    var useFull = data.TerminalHeight >= Theme.Layout.FullHeightThreshold && canUseAsciiArt;
    var useCompact = data.TerminalHeight >= Theme.Layout.CompactHeightThreshold && !useFull;

    // Leading blank (skip in minimal)
    if (!isMinimal) y++;

    // Wordmark
    if (useFull)
    {
      y = DrawFullWordmark(view, data, y, width);
    }
    else if (useCompact)
    {
      y = DrawCompactWordmark(view, data, y, width);
    }

    // Rule separator (full/compact only)
    if (useFull || useCompact)
    {
      y++; // blank line
      DrawRule(view, y, width);
      y++;
    }

    // Info grid spacing
    if (!isMinimal) y++;

    // Info grid
    if (isNarrow)
    {
      y = DrawCompactInfoGrid(view, data, y, width);
    }
    else
    {
      y = DrawInfoGrid(view, data, y, width);
    }

    // Blank between info and status
    y++;

    // Status footer
    if (isNarrow)
    {
      y = DrawCompactStatusFooter(view, data.IsConfigured, y);
    }
    else
    {
      y = DrawStatusFooter(view, data.IsConfigured, data.ExecutionMode, y, width);
    }

    // Hint line
    if (data.IsConfigured && (useFull || useCompact) && !isNarrow)
    {
      y++; // blank
      DrawHintLine(view, width, y);
      y++;
    }

    // Resume notice
    if (data.IsResumedSession && data.ResumeSessionId is not null && !isMinimal)
    {
      if (!(data.IsConfigured && (useFull || useCompact) && !isNarrow))
      {
        y++; // blank before resume
      }
      DrawResumeLine(view, data, y, width);
    }
  }

  // -----------------------------------------------
  //  Accessible mode
  // -----------------------------------------------

  private static int MeasureAccessible(BannerData data)
  {
    var lines = 2; // "BOYDCODE v..." + blank
    lines += 4; // Provider, Model, Project, Engine
    if (data.DockerImage is not null) lines++;
    lines++; // Directory
    lines += data.GitRepositories.Count;
    lines++; // blank
    lines++; // status
    if (data.IsConfigured && data.TerminalHeight >= Theme.Layout.CompactHeightThreshold) lines += 2; // blank + hint
    if (data.IsResumedSession && data.ResumeSessionId is not null) lines += 2; // blank + resume
    return lines;
  }

  private static void DrawAccessible(View view, BannerData data, int y, int width)
  {
    view.SetAttribute(Theme.Semantic.Default);
    DrawLine(view, y, $"BOYDCODE v{data.Version}", width);
    y++;
    y++; // blank

    DrawLine(view, y, $"Provider: {data.ProviderName}", width); y++;
    DrawLine(view, y, $"Model: {data.ModelName}", width); y++;
    DrawLine(view, y, $"Project: {FormatProjectName(data.ProjectName)}", width); y++;
    DrawLine(view, y, $"Engine: {data.ExecutionMode}", width); y++;

    if (data.DockerImage is not null)
    {
      DrawLine(view, y, $"Docker: {data.DockerImage}", width); y++;
    }

    DrawLine(view, y, $"Directory: {data.WorkingDirectory}", width); y++;

    foreach (var repo in data.GitRepositories)
    {
      var branchText = repo.Branch ?? repo.RepoRoot;
      DrawLine(view, y, $"Git: {branchText}", width); y++;
    }

    y++; // blank

    if (data.IsConfigured)
    {
      var engineDesc = data.ExecutionMode == "Container"
        ? "Commands execute inside a Docker container."
        : "Commands run in a constrained PowerShell runspace.";
      DrawLine(view, y, $"[OK] Ready. {engineDesc}", width); y++;
    }
    else
    {
      DrawLine(view, y, "Not configured. Run /provider setup or pass --api-key", width); y++;
    }

    if (data.IsConfigured && data.TerminalHeight >= Theme.Layout.CompactHeightThreshold)
    {
      y++; // blank
      DrawLine(view, y, Theme.Text.BannerHintWide, width); y++;
    }

    if (data.IsResumedSession && data.ResumeSessionId is not null)
    {
      y++; // blank
      var shortId = data.ResumeSessionId.Length > 8 ? data.ResumeSessionId[..8] : data.ResumeSessionId;
      var timestamp = data.ResumeTimestamp?.LocalDateTime
        .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";
      DrawLine(view, y, $"Resumed session {shortId} ({data.ResumeMessageCount} messages from {timestamp})", width);
    }
  }

  // -----------------------------------------------
  //  Fallback (tiny terminal)
  // -----------------------------------------------

  private static void DrawFallback(View view, BannerData data, int y, int width)
  {
    view.Move(0, y);
    view.AddStr("  ");

    if (data.IsConfigured)
    {
      view.SetAttribute(Theme.Banner.StatusReady);
      view.AddStr($"{Theme.Symbols.Check} ");
      view.SetAttribute(Theme.Banner.BoydArt);
      view.AddStr("BoydCode");
      view.SetAttribute(Theme.Semantic.Muted);
      view.AddStr(Truncate($" ready ({data.ProviderName}, {data.ModelName}, {FormatProjectName(data.ProjectName)})", width - 12));
    }
    else
    {
      view.SetAttribute(Theme.Banner.BoydArt);
      view.AddStr("BoydCode: ");
      view.SetAttribute(Theme.Banner.StatusNotConfigured);
      view.AddStr("Not configured.");
      view.SetAttribute(Theme.Semantic.Muted);
      view.AddStr(" Run ");
      view.SetAttribute(Theme.Semantic.Default);
      view.AddStr("/provider setup");
      view.SetAttribute(Theme.Semantic.Muted);
      view.AddStr(" or pass ");
      view.SetAttribute(Theme.Semantic.Default);
      view.AddStr("--api-key");
    }
  }

  // -----------------------------------------------
  //  Full wordmark (ASCII art + sidebar)
  // -----------------------------------------------

  private static int DrawFullWordmark(View view, BannerData data, int y, int width)
  {
    var capWidth = Math.Min(width, Theme.Layout.StandardWidth);
    var sidebar = width >= 100 ? SidebarWide : SidebarNarrow;
    var maxArtWidth = BoydArt.Max(a => a.Length);
    var gap = Math.Max(2, capWidth - maxArtWidth - sidebar.Max(s => s.Length) - 4);

    for (var i = 0; i < BoydArt.Length; i++)
    {
      view.SetAttribute(Theme.Banner.BoydArt);
      view.Move(0, y);
      view.AddStr(Truncate(BoydArt[i], width));

      if (i < sidebar.Length)
      {
        var artPadding = new string(' ', maxArtWidth - BoydArt[i].Length + gap);
        view.SetAttribute(Theme.Semantic.Muted);
        var sidebarText = artPadding + sidebar[i];
        var remainingWidth = width - BoydArt[i].Length;
        if (remainingWidth > 0)
        {
          view.AddStr(Truncate(sidebarText, remainingWidth));
        }
      }

      y++;
    }

    foreach (var row in CodeArt)
    {
      view.SetAttribute(Theme.Banner.CodeArt);
      view.Move(0, y);
      view.AddStr(Truncate(row, width));
      y++;
    }

    // Version line
    view.SetAttribute(Theme.Banner.Version);
    view.Move(0, y);
    view.AddStr(Truncate($"  v{data.Version}  Artificial Intelligence, Personal Edition", width));
    y++;

    return y;
  }

  // -----------------------------------------------
  //  Compact wordmark
  // -----------------------------------------------

  private static int DrawCompactWordmark(View view, BannerData data, int y, int width)
  {
    view.Move(0, y);
    view.AddStr("  ");
    view.SetAttribute(Theme.Banner.BoydArt);
    view.AddStr("BOYD");
    view.SetAttribute(Theme.Banner.CodeArt);
    view.AddStr("CODE");
    view.SetAttribute(Theme.Banner.Version);

    if (width >= Theme.Layout.StandardWidth)
    {
      view.AddStr($"  v{data.Version}  AI Coding Assistant");
    }
    else
    {
      view.AddStr($"  v{data.Version}");
    }

    y++;
    return y;
  }

  // -----------------------------------------------
  //  Rule separator
  // -----------------------------------------------

  private static void DrawRule(View view, int y, int width)
  {
    view.SetAttribute(Theme.Semantic.Muted);
    view.Move(0, y);
    view.AddStr(new string(Theme.Symbols.Rule, width));
  }

  // -----------------------------------------------
  //  Info grid (wide, >= 80 cols)
  // -----------------------------------------------

  private static int DrawInfoGrid(View view, BannerData data, int y, int width)
  {
    var projectDisplay = data.ProjectName == DefaultProjectName
      ? "(default)"
      : data.ProjectName;
    var projectIsDim = data.ProjectName == DefaultProjectName;

    // Row 1: Provider / Project
    DrawInfoPair(view, y, width,
      "Provider", data.ProviderName, false,
      "Project", projectDisplay, projectIsDim);
    y++;

    // Row 2: Model / Engine
    DrawInfoPair(view, y, width,
      "Model", data.ModelName, false,
      "Engine", data.ExecutionMode, false);
    y++;

    // Docker
    if (data.DockerImage is not null)
    {
      DrawInfoSingle(view, y, "Docker", data.DockerImage, width);
      y++;
    }

    // cwd
    DrawInfoSingle(view, y, "cwd", data.WorkingDirectory, width);
    y++;

    // Git repos
    foreach (var repo in data.GitRepositories)
    {
      if (repo.Branch is not null)
      {
        DrawInfoSingle(view, y, "Git", $"{repo.RepoRoot} ({repo.Branch})", width);
      }
      else
      {
        DrawInfoSingle(view, y, "Git", repo.RepoRoot, width);
      }
      y++;
    }

    return y;
  }

  // -----------------------------------------------
  //  Info grid (narrow, < 80 cols)
  // -----------------------------------------------

  private static int DrawCompactInfoGrid(View view, BannerData data, int y, int width)
  {
    DrawInfoSingle(view, y, "Provider", data.ProviderName, width); y++;
    DrawInfoSingle(view, y, "Model", data.ModelName, width); y++;

    if (data.ProjectName == DefaultProjectName)
    {
      view.SetAttribute(Theme.Banner.InfoLabel);
      view.Move(1, y);
      view.AddStr("Project  ");
      view.SetAttribute(Theme.Semantic.Muted);
      view.AddStr("(default)");
    }
    else
    {
      DrawInfoSingle(view, y, "Project", data.ProjectName, width);
    }
    y++;

    DrawInfoSingle(view, y, "Engine", data.ExecutionMode, width); y++;

    if (data.DockerImage is not null)
    {
      DrawInfoSingle(view, y, "Docker", data.DockerImage, width); y++;
    }

    DrawInfoSingle(view, y, "cwd", data.WorkingDirectory, width); y++;

    foreach (var repo in data.GitRepositories)
    {
      if (repo.Branch is not null)
      {
        DrawInfoSingle(view, y, "Git", $"{repo.RepoRoot} ({repo.Branch})", width);
      }
      else
      {
        DrawInfoSingle(view, y, "Git", repo.RepoRoot, width);
      }
      y++;
    }

    return y;
  }

  // -----------------------------------------------
  //  Info row drawing helpers
  // -----------------------------------------------

  private static void DrawInfoPair(View view, int y, int width,
    string label1, string value1, bool dimValue1,
    string label2, string value2, bool dimValue2)
  {
    var col1Width = Math.Min(width / 2, 50);

    // Left pair
    view.SetAttribute(Theme.Banner.InfoLabel);
    view.Move(2, y);
    view.AddStr(label1.PadRight(Theme.Layout.InfoLabelPad));
    view.SetAttribute(dimValue1 ? Theme.Semantic.Muted : Theme.Banner.InfoValue);
    view.AddStr(Truncate(value1, col1Width - 12));

    // Right pair (if room)
    if (width >= col1Width + 20)
    {
      view.SetAttribute(Theme.Banner.InfoLabel);
      view.Move(col1Width + 2, y);
      view.AddStr(label2.PadRight(Theme.Layout.InfoLabelPad));
      view.SetAttribute(dimValue2 ? Theme.Semantic.Muted : Theme.Banner.InfoValue);
      view.AddStr(Truncate(value2, width - col1Width - 14));
    }
  }

  private static void DrawInfoSingle(View view, int y, string label, string value, int maxWidth = int.MaxValue)
  {
    view.SetAttribute(Theme.Banner.InfoLabel);
    view.Move(2, y);
    view.AddStr(label.PadRight(Theme.Layout.InfoLabelPad));
    view.SetAttribute(Theme.Banner.InfoValue);
    var availableWidth = Math.Max(0, maxWidth - 12); // 2 indent + 10 padded label
    view.AddStr(Truncate(value, availableWidth));
  }

  // -----------------------------------------------
  //  Status footer
  // -----------------------------------------------

  private static int DrawStatusFooter(View view, bool isConfigured, string executionMode, int y, int width)
  {
    view.Move(0, y);
    view.AddStr("  ");

    if (isConfigured)
    {
      var engineDesc = executionMode == "Container"
        ? "Commands execute inside a Docker container."
        : "Commands run in a constrained PowerShell runspace.";
      view.SetAttribute(Theme.Banner.StatusReady);
      view.AddStr($"{Theme.Symbols.Check} ");
      view.SetAttribute(Theme.Semantic.Muted);
      view.AddStr(Truncate($"Ready  {engineDesc}", width - 4));
    }
    else
    {
      view.SetAttribute(Theme.Banner.StatusNotConfigured);
      view.AddStr("Not configured  ");
      view.SetAttribute(Theme.Semantic.Muted);
      view.AddStr("Run ");
      view.SetAttribute(Theme.Semantic.Default);
      view.AddStr("/provider setup");
      view.SetAttribute(Theme.Semantic.Muted);
      view.AddStr(" or pass ");
      view.SetAttribute(Theme.Semantic.Default);
      view.AddStr("--api-key");
    }

    y++;
    return y;
  }

  private static int DrawCompactStatusFooter(View view, bool isConfigured, int y)
  {
    view.Move(0, y);
    view.AddStr(" ");

    if (isConfigured)
    {
      view.SetAttribute(Theme.Banner.StatusReady);
      view.AddStr($"{Theme.Symbols.Check} ");
      view.SetAttribute(Theme.Semantic.Muted);
      view.AddStr("Ready");
    }
    else
    {
      view.SetAttribute(Theme.Banner.StatusNotConfigured);
      view.AddStr("Not configured");
    }

    y++;
    return y;
  }

  // -----------------------------------------------
  //  Hint line
  // -----------------------------------------------

  private static void DrawHintLine(View view, int width, int y)
  {
    view.SetAttribute(Theme.Semantic.Muted);
    view.Move(0, y);

    if (width >= Theme.Layout.FullWidth)
    {
      view.AddStr($"  {Theme.Text.BannerHintWide}");
    }
    else if (width >= Theme.Layout.StandardWidth)
    {
      view.AddStr($"  {Theme.Text.BannerHintMedium}");
    }
    else
    {
      view.AddStr($"  {Theme.Text.BannerHintNarrow}");
    }
  }

  // -----------------------------------------------
  //  Resume line
  // -----------------------------------------------

  private static void DrawResumeLine(View view, BannerData data, int y, int width)
  {
    var shortId = data.ResumeSessionId!.Length > 8
      ? data.ResumeSessionId[..8]
      : data.ResumeSessionId;
    var timestamp = data.ResumeTimestamp?.LocalDateTime
      .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";

    view.SetAttribute(Theme.Semantic.Muted);
    view.Move(0, y);
    view.AddStr(Truncate(
      $"  Resumed session {shortId} ({data.ResumeMessageCount} messages from {timestamp})",
      width));
  }

  // -----------------------------------------------
  //  Text helpers
  // -----------------------------------------------

  private static void DrawLine(View view, int y, string text, int width)
  {
    view.Move(0, y);
    view.AddStr(Truncate(text, width));
  }

  private static string Truncate(string text, int maxWidth)
  {
    if (maxWidth <= 0) return string.Empty;
    return text.Length <= maxWidth ? text : text[..maxWidth];
  }

  private static string FormatProjectName(string projectName)
  {
    return projectName == DefaultProjectName ? "(default)" : projectName;
  }
}
