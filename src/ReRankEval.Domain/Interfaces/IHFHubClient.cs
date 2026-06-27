using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public record HFModelInfo(
    string ModelId,
    string Revision,
    string? PipelineTag,
    IReadOnlyList<string> Tags,
    IReadOnlyList<HFFileInfo> Files,
    long TotalSizeBytes);

public record HFFileInfo(string FileName, long SizeBytes, string? Sha256);

public record HFSearchResult(string ModelId, string? PipelineTag, long Downloads, IReadOnlyList<string> Tags);

public interface IHFHubClient
{
    Task<IReadOnlyList<HFSearchResult>> SearchModelsAsync(string query, IReadOnlyList<string>? tags = null, CancellationToken ct = default);
    Task<HFModelInfo> GetModelInfoAsync(string modelId, CancellationToken ct = default);
    Task<ModelEntry> DownloadModelAsync(string modelId, IProgress<DownloadProgress> progress, CancellationToken ct = default);
}
