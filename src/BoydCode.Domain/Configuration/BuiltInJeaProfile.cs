using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Configuration;

public static class BuiltInJeaProfile
{
  public const string Name = "_builtin";
  public const string GlobalName = "_global";

  public static JeaProfile Instance { get; } = new(
      Name: Name,
      LanguageMode: PSLanguageModeName.ConstrainedLanguage,
      Modules: [],
      Entries:
      [
          // Navigation and info
          new("Get-Command", IsDenied: false),
            new("Get-Location", IsDenied: false),
            new("Set-Location", IsDenied: false),
            new("Get-ChildItem", IsDenied: false),
            new("Get-Item", IsDenied: false),
            new("Get-ItemProperty", IsDenied: false),
            new("Test-Path", IsDenied: false),
            new("Resolve-Path", IsDenied: false),
            new("Join-Path", IsDenied: false),
            new("Split-Path", IsDenied: false),
            // Read operations
            new("Get-Content", IsDenied: false),
            new("Select-String", IsDenied: false),
            new("Measure-Object", IsDenied: false),
            // Text processing
            new("Select-Object", IsDenied: false),
            new("Where-Object", IsDenied: false),
            new("ForEach-Object", IsDenied: false),
            new("Sort-Object", IsDenied: false),
            new("Group-Object", IsDenied: false),
            new("Format-Table", IsDenied: false),
            new("Format-List", IsDenied: false),
            new("Out-String", IsDenied: false),
            // Environment
            new("Get-Process", IsDenied: false),
            new("Get-Date", IsDenied: false),
            new("Get-Variable", IsDenied: false),
            // JSON
            new("ConvertTo-Json", IsDenied: false),
            new("ConvertFrom-Json", IsDenied: false),
            // Output
            new("Write-Output", IsDenied: false),
            new("Write-Host", IsDenied: false),
      ]);
}
