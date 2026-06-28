using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public interface IAnalyticsService
{
    Task<IReadOnlyList<NdcgTrendPoint>> GetNdcgTrendAsync(Guid modelId, Guid? datasetId = null, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntry>> GetModelLeaderboardAsync(Guid datasetId, CancellationToken ct = default);
}
