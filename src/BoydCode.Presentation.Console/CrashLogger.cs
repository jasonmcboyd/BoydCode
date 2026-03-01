namespace BoydCode.Presentation.Console;

internal static class CrashLogger
{
  private static readonly string LogDirectory =
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".boydcode", "logs");

  internal static string LogFilePath { get; } = Path.Combine(LogDirectory, "error.log");

  internal static void LogException(Exception exception)
  {
    try
    {
      Directory.CreateDirectory(LogDirectory);

      var entry = string.Join(
          Environment.NewLine,
          "================================================================================",
          $"[{DateTimeOffset.UtcNow:o}] UNHANDLED EXCEPTION",
          $"Type: {exception.GetType().FullName}",
          $"Message: {exception.Message}",
          "Stack Trace:",
          exception.StackTrace ?? "  (no stack trace)",
          "================================================================================",
          "");

      File.AppendAllText(LogFilePath, entry);
    }
    catch
    {
      // CrashLogger must never throw — swallow everything.
    }
  }
}
