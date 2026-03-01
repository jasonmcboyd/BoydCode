using BoydCode.Domain.Configuration;
using BoydCode.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BoydCode.Infrastructure.Container.Tests;

public sealed class VolumeMountBuilderTests
{
  [Fact]
  public void Build_ReadWriteDirectory_ProducesRwMount()
  {
    // Arrange
    var directories = new[]
    {
            new ResolvedDirectory(
                Path: "/tmp/myproject",
                AccessLevel: DirectoryAccessLevel.ReadWrite,
                Exists: true,
                IsGitRepository: false)
        };

    // Act
    var mounts = VolumeMountBuilder.Build(directories);

    // Assert
    mounts.Should().ContainSingle()
        .Which.Should().Contain(":rw");
  }

  [Fact]
  public void Build_ReadOnlyDirectory_ProducesRoMount()
  {
    // Arrange
    var directories = new[]
    {
            new ResolvedDirectory(
                Path: "/tmp/myproject",
                AccessLevel: DirectoryAccessLevel.ReadOnly,
                Exists: true,
                IsGitRepository: false)
        };

    // Act
    var mounts = VolumeMountBuilder.Build(directories);

    // Assert
    mounts.Should().ContainSingle()
        .Which.Should().Contain(":ro");
  }

  [Fact]
  public void Build_NonExistentDirectory_IsSkipped()
  {
    // Arrange
    var directories = new[]
    {
            new ResolvedDirectory(
                Path: "/tmp/does-not-exist",
                AccessLevel: DirectoryAccessLevel.ReadWrite,
                Exists: false,
                IsGitRepository: false)
        };

    // Act
    var mounts = VolumeMountBuilder.Build(directories);

    // Assert
    mounts.Should().BeEmpty();
  }

  [Fact]
  public void Build_NoneAccessLevel_IsSkipped()
  {
    // Arrange
    var directories = new[]
    {
            new ResolvedDirectory(
                Path: "/tmp/restricted",
                AccessLevel: DirectoryAccessLevel.None,
                Exists: true,
                IsGitRepository: false)
        };

    // Act
    var mounts = VolumeMountBuilder.Build(directories);

    // Assert
    mounts.Should().BeEmpty();
  }

  [Fact]
  public void Build_MultipleDirectories_ProducesMultipleMounts()
  {
    // Arrange
    var directories = new[]
    {
            new ResolvedDirectory(
                Path: "/tmp/alpha",
                AccessLevel: DirectoryAccessLevel.ReadWrite,
                Exists: true,
                IsGitRepository: false),
            new ResolvedDirectory(
                Path: "/tmp/beta",
                AccessLevel: DirectoryAccessLevel.ReadOnly,
                Exists: true,
                IsGitRepository: false),
            new ResolvedDirectory(
                Path: "/tmp/gamma",
                AccessLevel: DirectoryAccessLevel.ReadWrite,
                Exists: true,
                IsGitRepository: true)
        };

    // Act
    var mounts = VolumeMountBuilder.Build(directories);

    // Assert
    mounts.Should().HaveCount(3);
  }

  [Fact]
  public void BuildPathMapping_MapsHostToContainerPaths()
  {
    // Arrange
    var directories = new[]
    {
            new ResolvedDirectory(
                Path: "/tmp/myproject",
                AccessLevel: DirectoryAccessLevel.ReadWrite,
                Exists: true,
                IsGitRepository: false),
            new ResolvedDirectory(
                Path: "/tmp/shared-libs",
                AccessLevel: DirectoryAccessLevel.ReadOnly,
                Exists: true,
                IsGitRepository: false)
        };

    // Act
    var mapping = VolumeMountBuilder.BuildPathMapping(directories);

    // Assert
    mapping.Should().HaveCount(2);
    mapping["/tmp/myproject"].Should().Be("/project/myproject");
    mapping["/tmp/shared-libs"].Should().Be("/project/shared-libs");
  }

  [Fact]
  public void ResolveContainerPath_ExactMatch_ReturnsContainerPath()
  {
    // Arrange
    var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      ["/tmp/myproject"] = "/project/myproject"
    };

    // Act
    var result = VolumeMountBuilder.ResolveContainerPath("/tmp/myproject", mapping);

    // Assert
    result.Should().Be("/project/myproject");
  }

  [Fact]
  public void ResolveContainerPath_Subdirectory_ReturnsAppendedPath()
  {
    // Arrange
    var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      [@"C:\src\myproject"] = "/project/myproject"
    };

    // Act
    var result = VolumeMountBuilder.ResolveContainerPath(
        @"C:\src\myproject\src\bar", mapping);

    // Assert
    result.Should().Be("/project/myproject/src/bar");
  }

  [Fact]
  public void ResolveContainerPath_Unmapped_ReturnsNull()
  {
    // Arrange
    var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
      ["/tmp/myproject"] = "/project/myproject"
    };

    // Act
    var result = VolumeMountBuilder.ResolveContainerPath("/tmp/other", mapping);

    // Assert
    result.Should().BeNull();
  }

  [Fact]
  public void Build_DuplicateLeafNames_Disambiguates()
  {
    // Arrange -- two directories with the same leaf folder name "src"
    var directories = new[]
    {
            new ResolvedDirectory(
                Path: "/tmp/alpha/src",
                AccessLevel: DirectoryAccessLevel.ReadWrite,
                Exists: true,
                IsGitRepository: false),
            new ResolvedDirectory(
                Path: "/tmp/beta/src",
                AccessLevel: DirectoryAccessLevel.ReadWrite,
                Exists: true,
                IsGitRepository: false)
        };

    // Act
    var mounts = VolumeMountBuilder.Build(directories);

    // Assert -- both should be mounted with different container paths
    mounts.Should().HaveCount(2);
    mounts[0].Should().NotBe(mounts[1],
        "duplicate leaf names must produce distinct mount paths");
  }
}
