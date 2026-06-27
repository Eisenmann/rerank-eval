using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public record EvaluationRequest(
    string Name,
    IReadOnlyList<Guid> ModelIds,
    Guid DatasetId,
    IReadOnlyList<int> KValues,
    int BatchSize = 8,
    ExecutionProvider Provider = ExecutionProvider.Cpu);

public interface IEvaluationService
{
    Task<EvaluationRun> RunAsync(
        EvaluationRequest request,
        IProgress<EvaluationProgress> progress,
        CancellationToken ct = default);
}
