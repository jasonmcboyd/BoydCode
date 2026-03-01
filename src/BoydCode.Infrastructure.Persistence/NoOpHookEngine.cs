using BoydCode.Application.Interfaces;
using BoydCode.Domain.Enums;
using BoydCode.Domain.Hooks;

namespace BoydCode.Infrastructure.Persistence;

/// <summary>
/// Placeholder hook engine that returns no results. Phase 3 will add
/// actual hook command execution support.
/// </summary>
public sealed class NoOpHookEngine : IHookEngine
{
  public Task<IReadOnlyList<HookResult>> RunHooksAsync(
      HookTiming timing,
      string toolName,
      string argumentsJson,
      CancellationToken ct = default)
  {
    return Task.FromResult<IReadOnlyList<HookResult>>(Array.Empty<HookResult>());
  }
}
