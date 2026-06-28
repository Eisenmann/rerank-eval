using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using ReRankEval.Domain.Interfaces;

namespace ReRankEval.Agent.Plugins;

public sealed class ReportingPlugin
{
    private readonly IExperimentStore _store;
    private readonly IModelRegistry _registry;
    private readonly string _exportsDir;

    public ReportingPlugin(IExperimentStore store, IModelRegistry registry, string appDataDir)
    {
        _store = store;
        _registry = registry;
        _exportsDir = Path.Combine(appDataDir, "exports");
        Directory.CreateDirectory(_exportsDir);
    }

    [KernelFunction, Description("Generate a comprehensive Markdown evaluation report for a run and save it to the exports directory.")]
    public async Task<string> GenerateEvaluationReportAsync(
        [Description("Evaluation run ID (GUID string)")] string runId)
    {
        if (!Guid.TryParse(runId, out var id))
            return $"Invalid run ID: '{runId}'";

        var run = await _store.GetRunAsync(id);
        if (run == null)
            return $"Run '{runId}' not found.";

        var results = await _store.GetModelResultsAsync(id);
        var models = await _registry.ListAsync();
        var modelMap = models.ToDictionary(m => m.Id, m => m.HuggingFaceId);

        var sb = new StringBuilder();
        sb.AppendLine($"# Evaluation Report: {run.Name}");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC  ");
        sb.AppendLine($"**Status:** {run.Status}  ");
        sb.AppendLine($"**Started:** {run.StartedAt:yyyy-MM-dd HH:mm}  ");
        if (run.CompletedAt.HasValue)
            sb.AppendLine($"**Completed:** {run.CompletedAt.Value:yyyy-MM-dd HH:mm}  ");
        sb.AppendLine();

        sb.AppendLine("## Model Results");
        sb.AppendLine();
        sb.AppendLine("| Model | NDCG@10 | NDCG@5 | MRR@10 | MAP | P50ms | P99ms |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var r in results.OrderByDescending(r => r.NdcgAt.GetValueOrDefault(10)))
        {
            var label = modelMap.TryGetValue(r.ModelId, out var hfId) ? hfId : r.ModelId.ToString()[..8];
            sb.AppendLine($"| {label} | {r.NdcgAt.GetValueOrDefault(10):F4} | {r.NdcgAt.GetValueOrDefault(5):F4} | {r.MrrAt.GetValueOrDefault(10):F4} | {r.MapScore:F4} | {r.LatencyP50Ms:F1} | {r.LatencyP99Ms:F1} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Latency Breakdown");
        sb.AppendLine();
        sb.AppendLine("| Model | Tokenization | Tensor Creation | ONNX Run | Postprocessing |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var r in results.OrderByDescending(r => r.NdcgAt.GetValueOrDefault(10)))
        {
            var label = modelMap.TryGetValue(r.ModelId, out var hfId) ? hfId : r.ModelId.ToString()[..8];
            sb.AppendLine($"| {label} | {r.TokenizationMeanMs:F2}ms | {r.TensorCreationMeanMs:F2}ms | {r.SessionRunMeanMs:F2}ms | {r.PostprocessingMeanMs:F2}ms |");
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var safeName = string.Concat(run.Name.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        var fileName = $"report_{safeName}_{timestamp}.md";
        var filePath = Path.Combine(_exportsDir, fileName);

        await File.WriteAllTextAsync(filePath, sb.ToString());
        return $"Report saved to: {filePath}";
    }

    [KernelFunction, Description("List the most recent evaluation reports saved to the exports directory.")]
    public Task<string> ListRecentReportsAsync(
        [Description("Maximum number of reports to return (default 5)")] int maxResults = 5)
    {
        if (!Directory.Exists(_exportsDir))
            return Task.FromResult("No reports found.");

        var files = Directory.GetFiles(_exportsDir, "report_*.md")
            .Select(f => new FileInfo(f))
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .Take(maxResults)
            .ToList();

        if (!files.Any())
            return Task.FromResult("No reports found in exports directory.");

        var sb = new StringBuilder();
        sb.AppendLine("### Recent Reports");
        foreach (var fi in files)
            sb.AppendLine($"- `{fi.Name}` ({fi.LastWriteTimeUtc:yyyy-MM-dd HH:mm} UTC, {fi.Length / 1024.0:F1} KB)");
        return Task.FromResult(sb.ToString());
    }
}
