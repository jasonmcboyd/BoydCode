using BoydCode.Domain.Enums;

namespace BoydCode.Domain.Configuration;

public sealed record ProjectDirectory(
    string Path,
    DirectoryAccessLevel AccessLevel = DirectoryAccessLevel.ReadWrite);
