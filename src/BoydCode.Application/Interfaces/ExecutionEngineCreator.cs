using BoydCode.Domain.Configuration;

namespace BoydCode.Application.Interfaces;

public delegate Task<IExecutionEngine> ExecutionEngineCreator(
    ExecutionConfig config,
    IReadOnlyList<ResolvedDirectory> directories,
    string projectName,
    CancellationToken ct);
