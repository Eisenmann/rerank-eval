using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.Infrastructure.Services;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IExperimentStore _store;
    private readonly IModelRegistry _registry;

    public AnalyticsService(IExperimentStore store, IModelRegistry registry)
    {
        _store = store;
        _registry = registry;
    }

    public async Task<IReadOnlyList<NdcgTrendPoint>> GetNdcgTrendAsync(
        Guid modelId, Guid? datasetId = null, CancellationToken ct = default)
    {
        var runs = await _store.ListRunsAsync(datasetId: datasetId, ct: ct);
        var points = new List<NdcgTrendPoint>();

        foreach (var run in runs
            .Where(r => r.Status == RunStatus.Completed && r.ModelIds.Contains(modelId))
            .OrderBy(r => r.StartedAt))
        {
            var results = await _store.GetModelResultsAsync(run.Id, ct);
            var modelResult = results.FirstOrDefault(r => r.ModelId == modelId);
            if (modelResult is null) continue;
            points.Add(new NdcgTrendPoint(run.Name, run.StartedAt, modelResult.NdcgAt.GetValueOrDefault(10), run.Id));
        }

        return points;
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetModelLeaderboardAsync(
        Guid datasetId, CancellationToken ct = default)
    {
        var runs = await _store.ListRunsAsync(datasetId: datasetId, ct: ct);
        var completed = runs.Where(r => r.Status == RunStatus.Completed).ToList();

        var agg = new Dictionary<Guid, (double sumNdcg, double sumMrr, double sumMap, double sumLat, int count)>();

        foreach (var run in completed)
        {
            var results = await _store.GetModelResultsAsync(run.Id, ct);
            foreach (var r in results)
            {
                agg.TryGetValue(r.ModelId, out var cur);
                agg[r.ModelId] = (
                    cur.sumNdcg + r.NdcgAt.GetValueOrDefault(10),
                    cur.sumMrr + r.MrrAt.GetValueOrDefault(10),
                    cur.sumMap + r.MapScore,
                    cur.sumLat + r.LatencyP50Ms,
                    cur.count + 1);
            }
        }

        var models = await _registry.ListAsync(ct);
        var labels = models.ToDictionary(m => m.Id, m => m.HuggingFaceId);

        return agg
            .Select(kv =>
            {
                var (sN, sM, sA, sL, n) = kv.Value;
                var label = labels.TryGetValue(kv.Key, out var hf) ? hf : kv.Key.ToString()[..8];
                return new LeaderboardEntry(kv.Key, label, sN / n, sM / n, sA / n, sL / n, n);
            })
            .OrderByDescending(e => e.Ndcg10)
            .ToList();
    }
}
