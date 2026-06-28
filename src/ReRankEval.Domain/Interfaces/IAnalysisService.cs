using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public interface IAnalysisService
{
    Task<IReadOnlyList<QueryResult>> GetWorstQueriesAsync(
        Guid modelResultId,
        int take = 50,
        CancellationToken ct = default);

    Task<IReadOnlyList<CalibrationBucket>> GetCalibrationDataAsync(
        Guid modelResultId,
        int buckets = 10,
        CancellationToken ct = default);

    Task<IReadOnlyList<ModelCorrelation>> GetRankCorrelationsAsync(
        Guid runId,
        CancellationToken ct = default);

    Task<IReadOnlyList<DomainBreakdownRow>> GetDomainBreakdownAsync(
        Guid modelResultId,
        CancellationToken ct = default);
}
