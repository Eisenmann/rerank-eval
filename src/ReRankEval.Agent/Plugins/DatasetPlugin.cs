using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using ReRankEval.Domain.Interfaces;

namespace ReRankEval.Agent.Plugins;

public sealed class DatasetPlugin
{
    private readonly IExperimentStore _store;

    public DatasetPlugin(IExperimentStore store)
    {
        _store = store;
    }

    [KernelFunction, Description("List all available evaluation datasets.")]
    public async Task<string> ListDatasetsAsync()
    {
        var datasets = await _store.ListDatasetsAsync();
        if (!datasets.Any())
            return "No datasets loaded. Import a JSONL or BEIR dataset from the Datasets page.";

        var sb = new StringBuilder();
        sb.AppendLine("| Name | Format | Queries | Avg Docs/Query | Added |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var d in datasets.OrderByDescending(d => d.CreatedAt))
            sb.AppendLine($"| {d.Name} | {d.Format} | {d.QueryCount} | {d.AvgDocsPerQuery:F1} | {d.CreatedAt:yyyy-MM-dd} |");
        return sb.ToString();
    }

    [KernelFunction, Description("Get detailed information about a specific dataset.")]
    public async Task<string> GetDatasetInfoAsync(
        [Description("Dataset ID (GUID string) or dataset name")] string datasetIdOrName)
    {
        var datasets = await _store.ListDatasetsAsync();

        var dataset = Guid.TryParse(datasetIdOrName, out var id)
            ? await _store.GetDatasetAsync(id)
            : datasets.FirstOrDefault(d => d.Name.Equals(datasetIdOrName, StringComparison.OrdinalIgnoreCase));

        if (dataset == null)
            return $"Dataset '{datasetIdOrName}' not found.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Dataset: {dataset.Name}");
        sb.AppendLine($"- **Format:** {dataset.Format}");
        sb.AppendLine($"- **Queries:** {dataset.QueryCount}");
        sb.AppendLine($"- **Avg docs/query:** {dataset.AvgDocsPerQuery:F1}");
        sb.AppendLine($"- **Avg relevant docs/query:** {dataset.AvgRelevantDocsPerQuery:F2}");
        if (!string.IsNullOrEmpty(dataset.SourceBeirName))
            sb.AppendLine($"- **BEIR source:** {dataset.SourceBeirName}");
        if (dataset.Split.HasValue)
            sb.AppendLine($"- **Split:** {dataset.Split}");
        sb.AppendLine($"- **Path:** {dataset.LocalPath}");
        return sb.ToString();
    }
}
