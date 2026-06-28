using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.Agent.Plugins;

public sealed class EvaluationPlugin
{
    private readonly IExperimentStore _store;
    private readonly IModelRegistry _registry;

    public EvaluationPlugin(IExperimentStore store, IModelRegistry registry)
    {
        _store = store;
        _registry = registry;
    }

    [KernelFunction, Description("List recent evaluation runs with their status and top-level metrics.")]
    public async Task<string> ListRunsAsync(
        [Description("Maximum number of runs to return (default 10)")] int maxResults = 10)
    {
        var runs = await _store.ListRunsAsync();
        var recent = runs.OrderByDescending(r => r.StartedAt).Take(maxResults);

        var sb = new StringBuilder();
        sb.AppendLine("| Run Name | Status | Started | Models |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var run in recent)
        {
            sb.AppendLine($"| {run.Name} | {run.Status} | {run.StartedAt:yyyy-MM-dd HH:mm} | {run.ModelIds.Count} |");
        }
        return sb.Length == 0 ? "No evaluation runs found." : sb.ToString();
    }

    [KernelFunction, Description("Get a detailed summary of a specific evaluation run including per-model NDCG, MRR, and latency metrics.")]
    public async Task<string> GetRunSummaryAsync(
        [Description("Evaluation run ID (GUID string)")] string runId)
    {
        if (!Guid.TryParse(runId, out var id))
            return $"Invalid run ID format: '{runId}'";

        var run = await _store.GetRunAsync(id);
        if (run == null)
            return $"Run '{runId}' not found.";

        var models = await _registry.ListAsync();
        var modelMap = models.ToDictionary(m => m.Id, m => m.HuggingFaceId);

        var results = await _store.GetModelResultsAsync(id);

        var sb = new StringBuilder();
        sb.AppendLine($"## Evaluation Run: {run.Name}");
        sb.AppendLine($"**Status:** {run.Status}  **Started:** {run.StartedAt:yyyy-MM-dd HH:mm}");
        if (run.CompletedAt.HasValue)
            sb.AppendLine($"**Duration:** {(run.CompletedAt.Value - run.StartedAt).TotalMinutes:F1} min");
        sb.AppendLine();
        sb.AppendLine("| Model | NDCG@10 | MRR@10 | MAP | P50 ms |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var r in results.OrderByDescending(r => r.NdcgAt.GetValueOrDefault(10)))
        {
            var label = modelMap.TryGetValue(r.ModelId, out var hfId) ? hfId : r.ModelId.ToString()[..8];
            sb.AppendLine($"| {label} | {r.NdcgAt.GetValueOrDefault(10):F4} | {r.MrrAt.GetValueOrDefault(10):F4} | {r.MapScore:F4} | {r.LatencyP50Ms:F1} |");
        }
        return sb.ToString();
    }
}
