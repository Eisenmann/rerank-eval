using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.Infrastructure.Services;

public sealed class AnalysisService : IAnalysisService
{
    private readonly IExperimentStore _store;
    private readonly IModelRegistry _registry;
    private readonly IMetricsCalculator _metrics;

    public AnalysisService(IExperimentStore store, IModelRegistry registry, IMetricsCalculator metrics)
    {
        _store = store;
        _registry = registry;
        _metrics = metrics;
    }

    public async Task<IReadOnlyList<QueryResult>> GetWorstQueriesAsync(
        Guid modelResultId, int take = 50, CancellationToken ct = default)
    {
        return await _store.GetQueryResultsAsync(modelResultId, take, worstFirst: true, ct);
    }

    public async Task<IReadOnlyList<CalibrationBucket>> GetCalibrationDataAsync(
        Guid modelResultId, int buckets = 10, CancellationToken ct = default)
    {
        var queryResults = await _store.GetQueryResultsAsync(modelResultId, ct: ct);
        var scores = new List<float>();
        var labels = new List<int>();

        foreach (var qr in queryResults)
        {
            scores.AddRange(qr.Scores);
            labels.AddRange(qr.RelevanceLabels);
        }

        if (scores.Count == 0) return [];
        return _metrics.CalibrationBuckets(scores, labels, buckets);
    }

    public async Task<IReadOnlyList<ModelCorrelation>> GetRankCorrelationsAsync(
        Guid runId, CancellationToken ct = default)
    {
        var modelResults = await _store.GetModelResultsAsync(runId, ct);
        if (modelResults.Count < 2) return [];

        var allModels = await _registry.ListAsync(ct);
        var labels = allModels.ToDictionary(m => m.Id, m => m.HuggingFaceId);

        var queryData = new Dictionary<Guid, List<QueryResult>>();
        foreach (var mr in modelResults)
        {
            var qrs = await _store.GetQueryResultsAsync(mr.Id, ct: ct);
            queryData[mr.Id] = qrs.ToList();
        }

        var correlations = new List<ModelCorrelation>();

        for (var i = 0; i < modelResults.Count; i++)
        {
            for (var j = i + 1; j < modelResults.Count; j++)
            {
                var m1 = modelResults[i];
                var m2 = modelResults[j];
                var qrs1 = queryData[m1.Id];
                var qrs2 = queryData[m2.Id];

                var rhoValues = new List<double>();
                var tauValues = new List<double>();

                var queries1 = qrs1.ToDictionary(q => q.QueryText);
                foreach (var qr2 in qrs2)
                {
                    if (!queries1.TryGetValue(qr2.QueryText, out var qr1)) continue;
                    if (qr1.Scores.Count < 2) continue;

                    var s1 = qr1.Scores.Select(s => (double)s).ToList();
                    var s2 = qr2.Scores.Select(s => (double)s).ToList();
                    var len = Math.Min(s1.Count, s2.Count);

                    rhoValues.Add(_metrics.SpearmanRho(s1.Take(len).ToList(), s2.Take(len).ToList()));
                    tauValues.Add(_metrics.KendallTau(s1.Take(len).ToList(), s2.Take(len).ToList()));
                }

                if (rhoValues.Count == 0) continue;

                var l1 = labels.TryGetValue(m1.ModelId, out var lb1) ? lb1 : m1.ModelId.ToString()[..8];
                var l2 = labels.TryGetValue(m2.ModelId, out var lb2) ? lb2 : m2.ModelId.ToString()[..8];
                correlations.Add(new ModelCorrelation(l1, l2, rhoValues.Average(), tauValues.Average()));
            }
        }

        return correlations;
    }

    public async Task<IReadOnlyList<DomainBreakdownRow>> GetDomainBreakdownAsync(
        Guid modelResultId, CancellationToken ct = default)
    {
        var queryResults = await _store.GetQueryResultsAsync(modelResultId, ct: ct);
        var tagged = queryResults.Where(q => !string.IsNullOrEmpty(q.DomainTag));

        return tagged
            .GroupBy(q => q.DomainTag!)
            .Select(g => new DomainBreakdownRow(
                g.Key,
                g.Average(q => q.NdcgAt10),
                g.Average(q => q.MrrAt10),
                g.Count()))
            .OrderBy(r => r.DomainTag)
            .ToList();
    }
}
