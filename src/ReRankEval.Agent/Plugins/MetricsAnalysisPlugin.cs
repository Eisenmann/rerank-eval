using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using ReRankEval.Domain.Interfaces;

namespace ReRankEval.Agent.Plugins;

public sealed class MetricsAnalysisPlugin
{
    private readonly IAnalysisService _analysis;
    private readonly IExperimentStore _store;
    private readonly IModelRegistry _registry;

    public MetricsAnalysisPlugin(IAnalysisService analysis, IExperimentStore store, IModelRegistry registry)
    {
        _analysis = analysis;
        _store = store;
        _registry = registry;
    }

    [KernelFunction, Description("Compare all models evaluated in a specific run using a Markdown table.")]
    public async Task<string> CompareModelsAsync(
        [Description("Evaluation run ID (GUID string)")] string runId)
    {
        if (!Guid.TryParse(runId, out var id))
            return $"Invalid run ID: '{runId}'";

        var results = await _store.GetModelResultsAsync(id);
        if (!results.Any())
            return $"No model results found for run {runId}.";

        var models = await _registry.ListAsync();
        var modelMap = models.ToDictionary(m => m.Id, m => m.HuggingFaceId);

        var correlations = await _analysis.GetRankCorrelationsAsync(id);

        var sb = new StringBuilder();
        sb.AppendLine("### Model Comparison");
        sb.AppendLine();
        sb.AppendLine("| Model | NDCG@10 | NDCG@5 | MRR@10 | MAP | P50ms | P99ms |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var r in results.OrderByDescending(r => r.NdcgAt.GetValueOrDefault(10)))
        {
            var label = modelMap.TryGetValue(r.ModelId, out var hfId) ? hfId : r.ModelId.ToString()[..8];
            sb.AppendLine($"| {label} | {r.NdcgAt.GetValueOrDefault(10):F4} | {r.NdcgAt.GetValueOrDefault(5):F4} | {r.MrrAt.GetValueOrDefault(10):F4} | {r.MapScore:F4} | {r.LatencyP50Ms:F1} | {r.LatencyP99Ms:F1} |");
        }

        if (correlations.Any())
        {
            sb.AppendLine();
            sb.AppendLine("### Rank Correlations");
            sb.AppendLine("| Model A | Model B | Spearman ρ | Kendall τ |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var c in correlations)
                sb.AppendLine($"| {c.Model1Label} | {c.Model2Label} | {c.SpearmanRho:F3} | {c.KendallTau:F3} |");
        }

        return sb.ToString();
    }

    [KernelFunction, Description("Get the worst-performing queries for a specific model result, useful for error analysis.")]
    public async Task<string> GetWorstQueriesAsync(
        [Description("Model result ID (GUID string)")] string modelResultId,
        [Description("Number of worst queries to return (default 10)")] int count = 10)
    {
        if (!Guid.TryParse(modelResultId, out var id))
            return $"Invalid model result ID: '{modelResultId}'";

        var queries = await _analysis.GetWorstQueriesAsync(id, count);
        if (!queries.Any())
            return "No query results found for this model result.";

        var sb = new StringBuilder();
        sb.AppendLine($"### Worst {count} Queries");
        sb.AppendLine("| Query | NDCG@10 | MRR@10 | Latency ms |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var q in queries)
        {
            var shortQuery = q.QueryText.Length > 60 ? q.QueryText[..57] + "..." : q.QueryText;
            sb.AppendLine($"| {shortQuery} | {q.NdcgAt10:F4} | {q.MrrAt10:F4} | {q.LatencyMs:F1} |");
        }
        return sb.ToString();
    }

    [KernelFunction, Description("Get a calibration summary showing how well model scores predict relevance for a model result.")]
    public async Task<string> GetCalibrationSummaryAsync(
        [Description("Model result ID (GUID string)")] string modelResultId)
    {
        if (!Guid.TryParse(modelResultId, out var id))
            return $"Invalid model result ID: '{modelResultId}'";

        var buckets = await _analysis.GetCalibrationDataAsync(id, 10);
        if (!buckets.Any())
            return "No calibration data available.";

        var sb = new StringBuilder();
        sb.AppendLine("### Calibration (Score Bucket → Fraction Relevant)");
        sb.AppendLine("| Score Range | Fraction Relevant | Sample Count |");
        sb.AppendLine("|---|---|---|");
        foreach (var b in buckets)
            sb.AppendLine($"| {b.ScoreLow:F2}–{b.ScoreHigh:F2} | {b.ActualRelevanceFraction:F3} | {b.Count} |");

        var mid = buckets.Where(b => b.Count > 0).ToList();
        var overallCalibration = mid.Count > 0
            ? mid.Average(b => Math.Abs((b.ScoreLow + b.ScoreHigh) / 2.0 - b.ActualRelevanceFraction))
            : 0.0;
        sb.AppendLine();
        sb.AppendLine($"**Mean calibration error:** {overallCalibration:F3}");
        return sb.ToString();
    }
}
