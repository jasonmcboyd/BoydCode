using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace BoydCode.Presentation.Console.Terminal;

internal static class Theme
{
  // ─── Semantic Colors ───────────────────────────────────────
  // The 7 core colors. Role-specific attributes that match a
  // semantic color reference it directly rather than duplicating.

  internal static class Semantic
  {
    internal static readonly Attribute Success = new(ColorName16.Green, Color.None);
    internal static readonly Attribute Error = new(ColorName16.BrightRed, Color.None);
    internal static readonly Attribute Warning = new(ColorName16.Yellow, Color.None);
    internal static readonly Attribute Info = new(ColorName16.Cyan, Color.None);
    internal static readonly Attribute Accent = new(ColorName16.Blue, Color.None);
    internal static readonly Attribute Muted = new(ColorName16.DarkGray, Color.None);
    internal static readonly Attribute Default = new(ColorName16.White, Color.None);
  }

  // ─── User Message Block ────────────────────────────────────

  internal static class User
  {
    internal static readonly Color Background = new(50, 50, 50);
    internal static readonly Attribute Text = new(ColorName16.White, Background);
    internal static readonly Attribute Prefix = new(ColorName16.DarkGray, Background);
  }

  // ─── Tool Call Badge ───────────────────────────────────────

  internal static class ToolBox
  {
    internal static Attribute Border => Semantic.Muted;
  }

  // ─── Chat Input ────────────────────────────────────────────

  internal static class Input
  {
    internal static readonly Attribute Prompt = new(ColorName16.Blue, Color.None, TextStyle.Bold);
    internal static readonly Attribute Text = new(ColorName16.White, Color.None);
    internal static readonly Attribute Cursor = new(ColorName16.White, Color.None, TextStyle.Underline);
    internal static readonly Attribute CursorDim = new(ColorName16.DarkGray, Color.None, TextStyle.Underline);
    internal static readonly Attribute Disabled = new(ColorName16.DarkGray, Color.None);
    internal static readonly Attribute Clear = new(ColorName16.White, Color.None);
    internal static readonly Attribute ScrollIndicator = new(ColorName16.DarkGray, Color.None);
  }

  // ─── Status Bar ────────────────────────────────────────────

  internal static class StatusBar
  {
    internal static readonly Color Background = new(30, 30, 30);
    internal static readonly Attribute Status = new(ColorName16.White, Background);
    internal static readonly Attribute Hint = new(ColorName16.DarkGray, Background);
    internal static readonly Attribute Fill = new(Background, Background);
  }

  // ─── Banner ────────────────────────────────────────────────

  internal static class Banner
  {
    internal static readonly Attribute BoydArt = new(ColorName16.BrightCyan, Color.None);
    internal static readonly Attribute CodeArt = new(ColorName16.BrightBlue, Color.None);
    internal static Attribute InfoLabel => Semantic.Muted;
    internal static Attribute InfoValue => Semantic.Info;
    internal static Attribute StatusReady => Semantic.Success;
    internal static Attribute StatusNotConfigured => Semantic.Warning;
    internal static Attribute Version => Semantic.Muted;
  }

  // ─── Modal ─────────────────────────────────────────────────

  internal static class Modal
  {
    internal static readonly Scheme BorderScheme = new(
      new Attribute(ColorName16.Blue, Color.None));
  }

  // ─── Chart Colors ──────────────────────────────────────────

  internal static class Chart
  {
    internal static readonly Color Tools = new(147, 112, 219);
    internal static readonly Color FreeSpace = new(128, 128, 128);
    internal static readonly Color Buffer = new(255, 140, 0);

    internal static readonly Attribute ToolsAttr = new(Tools, Color.None);
    internal static readonly Attribute FreeSpaceAttr = new(FreeSpace, Color.None);
    internal static readonly Attribute BufferAttr = new(Buffer, Color.None);
  }

  // ─── Interactive List ─────────────────────────────────────

  internal static class List
  {
    internal static readonly Color SelectedBg = new Color(ColorName16.Blue);
    internal static readonly Attribute SelectedBackground = new(SelectedBg, SelectedBg);
    internal static readonly Attribute SelectedText = new(ColorName16.White, SelectedBg);
    internal static readonly Attribute AlternateRow = new(ColorName16.White, Color.None);
    internal static Attribute ActionBar => Semantic.Muted;
  }

  // ─── Focus Indicators ────────────────────────────────────

  internal static class Focus
  {
    internal static readonly Attribute Border = new(ColorName16.Blue, Color.None);
  }

  // ─── Unicode Symbols ───────────────────────────────────────

  internal static class Symbols
  {
    internal const char Check = '\u2713';
    internal const char Cross = '\u2717';
    internal const char Rule = '\u2500';
    internal const char Arrow = '\u25b6';
    internal const char BoxTopLeft = '\u250c';
    internal const char BoxTopRight = '\u2510';
    internal const char BoxBottomLeft = '\u2514';
    internal const char BoxBottomRight = '\u2518';
    internal const char BoxVertical = '\u2502';
    internal const char ArrowLeft = '\u2190';
    internal const char ArrowRight = '\u2192';
    internal const char FullBlock = '\u2588';
    internal const char LightShade = '\u2591';
    internal const char BlackSquare = '\u25a0';

    internal static readonly char[] SpinnerFrames =
      ['\u280b', '\u2819', '\u2839', '\u2838', '\u283c', '\u2834', '\u2826', '\u2827', '\u2807', '\u280f'];
  }

  // ─── Layout Constants ──────────────────────────────────────

  internal static class Layout
  {
    internal const int FullWidth = 120;
    internal const int StandardWidth = 80;
    internal const int MinimalHeightThreshold = 10;
    internal const int CompactHeightThreshold = 15;
    internal const int FullHeightThreshold = 30;
    internal const int SpinnerIntervalMs = 100;
    internal const int CursorBlinkMs = 500;
    internal const int CancelWindowMs = 1000;
    internal const int MaxInputHistory = 100;
    internal const int MaxConversationBlocks = 2000;
    internal const int CommandPad = 24;
    internal const int InfoLabelPad = 10;
  }

  // ─── Text Constants ────────────────────────────────────────

  internal static class Text
  {
    internal const string PromptPrefix = "> ";
    internal const string ThinkingLabel = "Thinking...";
    internal const string StreamingLabel = "Streaming...";
    internal const string ExecutingLabel = "Executing...";
    internal const string EscToDismiss = "Esc to dismiss";
    internal const string CancelHint = "Press Esc again to cancel";
    internal const string ExpandHint = "/expand to show full output";

    internal const string HintsWide = "Esc:Cancel  PgUp/PgDn:Scroll  /help:Commands  /quit:Exit";
    internal const string HintsMedium = "Esc:Cancel  PgUp/Dn:Scroll  /quit:Exit";
    internal const string HintsNarrow = "/help  /quit";

    internal const string BannerHintWide = "Type a message to start, or /help for available commands.";
    internal const string BannerHintMedium = "Type a message to start, or /help for commands.";
    internal const string BannerHintNarrow = "Type a message, or /help";
  }
}
