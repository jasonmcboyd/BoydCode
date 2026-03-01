using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;

namespace BoydCode.Application.Interfaces;

public interface IDirectoryGuard
{
  DirectoryAccessLevel GetAccessLevel(string absolutePath);
  void Configure(IReadOnlyList<ProjectDirectory> directories);
}
