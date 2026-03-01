using BoydCode.Domain.Enums;
using BoydCode.Domain.Hooks;

namespace BoydCode.Application.Interfaces;

public interface IHookEngine
{
  Task<IReadOnlyList<HookResult>> RunHooksAsync(HookTiming timing, string toolName, string argumentsJson, CancellationToken ct = default);
}
