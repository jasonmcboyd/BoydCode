using BoydCode.Domain.Configuration;

namespace BoydCode.Application.Interfaces;

public interface ISettingsProvider
{
  AppSettings GetSettings();
  string? GetSystemPromptExtensions(string workingDirectory);
}
