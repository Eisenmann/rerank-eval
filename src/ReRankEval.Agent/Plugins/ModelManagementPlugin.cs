using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using ReRankEval.Domain.Interfaces;

namespace ReRankEval.Agent.Plugins;

public sealed class ModelManagementPlugin
{
    private readonly IModelRegistry _registry;
    private readonly IHFHubClient _hubClient;

    public ModelManagementPlugin(IModelRegistry registry, IHFHubClient hubClient)
    {
        _registry = registry;
        _hubClient = hubClient;
    }

    [KernelFunction, Description("List all locally downloaded reranker models with their metadata.")]
    public async Task<string> ListModelsAsync()
    {
        var models = await _registry.ListAsync();
        if (!models.Any())
            return "No models downloaded yet. Use SearchHuggingFace to find and download a model.";

        var items = models.Select(m => new
        {
            m.Id,
            m.HuggingFaceId,
            m.Architecture,
            SizeMb = Math.Round(m.WeightsSizeBytes / 1_000_000.0, 1),
            m.MaxSequenceLength,
            DownloadedAt = m.DownloadedAt.ToString("yyyy-MM-dd")
        });
        return JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
    }

    [KernelFunction, Description("Search HuggingFace Hub for reranker models matching a query.")]
    public async Task<string> SearchHuggingFaceAsync(
        [Description("Search query, e.g. 'cross-encoder ms-marco'")] string query,
        [Description("Maximum number of results to return (default 5)")] int maxResults = 5)
    {
        try
        {
            var results = await _hubClient.SearchModelsAsync(query);
            var top = results.Take(maxResults).ToList();
            if (!top.Any())
                return $"No models found for query '{query}'.";

            return JsonSerializer.Serialize(top.Select(r => new
            {
                r.ModelId,
                r.PipelineTag,
                r.Downloads,
                Tags = string.Join(", ", r.Tags)
            }), new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Search failed: {ex.Message}";
        }
    }

    [KernelFunction, Description("Download a reranker model from HuggingFace Hub by its model ID.")]
    public async Task<string> DownloadModelAsync(
        [Description("HuggingFace model ID, e.g. 'cross-encoder/ms-marco-MiniLM-L-6-v2'")] string modelId)
    {
        try
        {
            var progress = new Progress<Domain.Models.DownloadProgress>();
            await _hubClient.DownloadModelAsync(modelId, progress);
            return $"Model '{modelId}' downloaded successfully.";
        }
        catch (Exception ex)
        {
            return $"Download failed for '{modelId}': {ex.Message}";
        }
    }
}
