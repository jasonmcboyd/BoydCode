using System.Globalization;
using BoydCode.Presentation.Console.Renderables;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

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

  // Color palette — Color.None inherits the terminal's default background
  private static readonly Attribute BoydArtAttr = new(ColorName16.BrightCyan, Color.None);
  private static readonly Attribute CodeArtAttr = new(ColorName16.BrightBlue, Color.None);
  private static readonly Attribute InfoLabelAttr = new(ColorName16.DarkGray, Color.None);
  private static readonly Attribute InfoValueAttr = new(ColorName16.Cyan, Color.None);
  private static readonly Attribute StatusReadyAttr = new(ColorName16.Green, Color.None);
  private static readonly Attribute StatusNotConfiguredAttr = new(ColorName16.Yellow, Color.None);
  private static readonly Attribute DimAttr = new(ColorName16.DarkGray, Color.None);
  private static readonly Attribute DefaultAttr = new(ColorName16.White, Color.None);
  private static readonly Attribute VersionAttr = new(ColorName16.DarkGray, Color.None);

  public static int MeasureBanner(BannerData data, int width)
  {
    if (data.Accessible)
    {
      return MeasureAccessible(data);
    }

    if (data.TerminalHeight < 10)
    {
      return 1; // Fallback: single line
    }

    var height = 0;
    var isNarrow = width < 80;
    var isMinimal = data.TerminalHeight is >= 10 and < 15;
    var canUseAsciiArt = data.SupportsUnicode && !isNarrow;
    var useFull = data.TerminalHeight >= 30 && canUseAsciiArt;
    var useCompact = data.TerminalHeight >= 15 && !useFull;

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

    if (data.TerminalHeight < 10)
    {
      DrawFallback(view, data, y, width);
      return;
    }

    var isNarrow = width < 80;
    var isMinimal = data.TerminalHeight is >= 10 and < 15;
    var canUseAsciiArt = data.SupportsUnicode && !isNarrow;
    var useFull = data.TerminalHeight >= 30 && canUseAsciiArt;
    var useCompact = data.TerminalHeight >= 15 && !useFull;

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
    if (data.IsConfigured && data.TerminalHeight >= 15) lines += 2; // blank + hint
    if (data.IsResumedSession && data.ResumeSessionId is not null) lines += 2; // blank + resume
    return lines;
  }

  private static void DrawAccessible(View view, BannerData data, int y, int width)
  {
    view.SetAttribute(DefaultAttr);
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

    if (data.IsConfigured && data.TerminalHeight >= 15)
    {
      y++; // blank
      DrawLine(view, y, "Type a message to start, or /help for available commands.", width); y++;
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
      view.SetAttribute(StatusReadyAttr);
      view.AddStr("\u2713 ");
      view.SetAttribute(BoydArtAttr);
      view.AddStr("BoydCode");
      view.SetAttribute(DimAttr);
      view.AddStr(Truncate($" ready ({data.ProviderName}, {data.ModelName}, {FormatProjectName(data.ProjectName)})", width - 12));
    }
    else
    {
      view.SetAttribute(BoydArtAttr);
      view.AddStr("BoydCode: ");
      view.SetAttribute(StatusNotConfiguredAttr);
      view.AddStr("Not configured.");
      view.SetAttribute(DimAttr);
      view.AddStr(" Run ");
      view.SetAttribute(DefaultAttr);
      view.AddStr("/provider setup");
      view.SetAttribute(DimAttr);
      view.AddStr(" or pass ");
      view.SetAttribute(DefaultAttr);
      view.AddStr("--api-key");
    }
  }

  // -----------------------------------------------
  //  Full wordmark (ASCII art + sidebar)
  // -----------------------------------------------

  private static int DrawFullWordmark(View view, BannerData data, int y, int width)
  {
    var capWidth = Math.Min(width, 80);
    var sidebar = width >= 100 ? SidebarWide : SidebarNarrow;
    var maxArtWidth = BoydArt.Max(a => a.Length);
    var gap = Math.Max(2, capWidth - maxArtWidth - sidebar.Max(s => s.Length) - 4);

    for (var i = 0; i < BoydArt.Length; i++)
    {
      view.SetAttribute(BoydArtAttr);
      view.Move(0, y);
      view.AddStr(Truncate(BoydArt[i], width));

      if (i < sidebar.Length)
      {
        var artPadding = new string(' ', maxArtWidth - BoydArt[i].Length + gap);
        view.SetAttribute(DimAttr);
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
      view.SetAttribute(CodeArtAttr);
      view.Move(0, y);
      view.AddStr(Truncate(row, width));
      y++;
    }

    // Version line
    view.SetAttribute(VersionAttr);
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
    view.SetAttribute(BoydArtAttr);
    view.AddStr("BOYD");
    view.SetAttribute(CodeArtAttr);
    view.AddStr("CODE");
    view.SetAttribute(VersionAttr);

    if (width >= 80)
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
    view.SetAttribute(DimAttr);
    view.Move(0, y);
    view.AddStr(new string('\u2500', Math.Min(width, 120)));
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
      view.SetAttribute(InfoLabelAttr);
      view.Move(1, y);
      view.AddStr("Project  ");
      view.SetAttribute(DimAttr);
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
    view.SetAttribute(InfoLabelAttr);
    view.Move(2, y);
    view.AddStr(label1.PadRight(10));
    view.SetAttribute(dimValue1 ? DimAttr : InfoValueAttr);
    view.AddStr(Truncate(value1, col1Width - 12));

    // Right pair (if room)
    if (width >= col1Width + 20)
    {
      view.SetAttribute(InfoLabelAttr);
      view.Move(col1Width + 2, y);
      view.AddStr(label2.PadRight(10));
      view.SetAttribute(dimValue2 ? DimAttr : InfoValueAttr);
      view.AddStr(Truncate(value2, width - col1Width - 14));
    }
  }

  private static void DrawInfoSingle(View view, int y, string label, string value, int maxWidth = int.MaxValue)
  {
    view.SetAttribute(InfoLabelAttr);
    view.Move(2, y);
    view.AddStr(label.PadRight(10));
    view.SetAttribute(InfoValueAttr);
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
      view.SetAttribute(StatusReadyAttr);
      view.AddStr("\u2713 ");
      view.SetAttribute(DimAttr);
      view.AddStr(Truncate($"Ready  {engineDesc}", width - 4));
    }
    else
    {
      view.SetAttribute(StatusNotConfiguredAttr);
      view.AddStr("Not configured  ");
      view.SetAttribute(DimAttr);
      view.AddStr("Run ");
      view.SetAttribute(DefaultAttr);
      view.AddStr("/provider setup");
      view.SetAttribute(DimAttr);
      view.AddStr(" or pass ");
      view.SetAttribute(DefaultAttr);
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
      view.SetAttribute(StatusReadyAttr);
      view.AddStr("\u2713 ");
      view.SetAttribute(DimAttr);
      view.AddStr("Ready");
    }
    else
    {
      view.SetAttribute(StatusNotConfiguredAttr);
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
    view.SetAttribute(DimAttr);
    view.Move(0, y);

    if (width >= 120)
    {
      view.AddStr("  Type a message to start, or /help for available commands.");
    }
    else if (width >= 80)
    {
      view.AddStr("  Type a message to start, or /help for commands.");
    }
    else
    {
      view.AddStr("  Type a message, or /help");
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

    view.SetAttribute(DimAttr);
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
