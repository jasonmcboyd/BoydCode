using BoydCode.Domain.Configuration;

namespace BoydCode.Application.Interfaces;

public interface IExecutionEngineFactory
{
  Task<IExecutionEngine> CreateAsync(
      ExecutionConfig config,
      IReadOnlyList<ResolvedDirectory> directories,
      string projectName,
      CancellationToken ct = default);
}
