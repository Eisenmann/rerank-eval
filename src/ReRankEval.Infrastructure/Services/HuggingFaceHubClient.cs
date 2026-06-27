using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.Infrastructure.Services;

public sealed class HuggingFaceHubClient : IHFHubClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HuggingFaceHubClient> _logger;
    private readonly string _baseModelDir;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public HuggingFaceHubClient(HttpClient http, ILogger<HuggingFaceHubClient> logger, string baseModelDir)
    {
        _http = http;
        _logger = logger;
        _baseModelDir = baseModelDir;
    }

    public async Task<IReadOnlyList<HFSearchResult>> SearchModelsAsync(
        string query, IReadOnlyList<string>? tags = null, CancellationToken ct = default)
    {
        var url = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(query)}&filter=cross-encoder&limit=20";
        if (tags != null)
            url += "&" + string.Join("&", tags.Select(t => $"filter={Uri.EscapeDataString(t)}"));

        try
        {
            var resp = await _http.GetFromJsonAsync<HFApiModelListResponse[]>(url, _json, ct);
            return resp?.Select(r => new HFSearchResult(
                r.ModelId ?? r.Id ?? "",
                r.PipelineTag,
                r.Downloads ?? 0,
                r.Tags ?? []
            )).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HF Hub search failed for query={Query}", query);
            return [];
        }
    }

    public async Task<HFModelInfo> GetModelInfoAsync(string modelId, CancellationToken ct = default)
    {
        var url = $"https://huggingface.co/api/models/{modelId}";
        var resp = await _http.GetFromJsonAsync<HFApiModelDetailResponse>(url, _json, ct)
            ?? throw new InvalidOperationException($"Could not fetch model info for {modelId}");

        var files = resp.Siblings?.Select(s => new HFFileInfo(s.RFilename, s.Size ?? 0, s.LfsOid)).ToList()
            ?? new List<HFFileInfo>();
        var totalSize = files.Sum(f => f.SizeBytes);

        return new HFModelInfo(
            modelId,
            resp.Sha ?? "main",
            resp.PipelineTag,
            resp.Tags ?? [],
            files,
            totalSize
        );
    }

    public async Task<ModelEntry> DownloadModelAsync(
        string modelId, IProgress<DownloadProgress> progress, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting download of {ModelId}", modelId);
        var info = await GetModelInfoAsync(modelId, ct);
        var localDir = Path.Combine(_baseModelDir, modelId.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(localDir);

        var requiredFiles = new[] { "config.json", "tokenizer.json", "tokenizer_config.json", "special_tokens_map.json" };
        var weightFiles = info.Files
            .Where(f => f.FileName.EndsWith(".safetensors") || f.FileName == "pytorch_model.bin")
            .ToList();

        var allFilesToDownload = info.Files
            .Where(f => requiredFiles.Contains(f.FileName) || weightFiles.Contains(f))
            .ToList();

        long totalBytes = allFilesToDownload.Sum(f => f.SizeBytes);
        long downloadedBytes = 0;

        var counter = new DownloadCounter { BytesDownloaded = downloadedBytes };

        foreach (var file in allFilesToDownload)
        {
            var destPath = Path.Combine(localDir, file.FileName);
            if (File.Exists(destPath))
            {
                counter.BytesDownloaded += file.SizeBytes;
                progress.Report(new DownloadProgress(modelId, file.FileName, counter.BytesDownloaded, totalBytes));
                continue;
            }

            var fileUrl = $"https://huggingface.co/{modelId}/resolve/main/{file.FileName}";
            await DownloadFileWithResumeAsync(fileUrl, destPath, modelId, file.FileName,
                totalBytes, counter, progress, ct);
        }

        var architecture = DetectArchitecture(Path.Combine(localDir, "config.json"));
        var entry = new ModelEntry
        {
            HuggingFaceId = modelId,
            Revision = info.Revision,
            LocalPath = localDir,
            Architecture = architecture,
            WeightsSizeBytes = totalBytes,
            MaxSequenceLength = 512
        };

        _logger.LogInformation("Download complete for {ModelId}", modelId);
        return entry;
    }

    private sealed class DownloadCounter { public long BytesDownloaded; }

    private async Task DownloadFileWithResumeAsync(
        string url, string destPath, string modelId, string fileName,
        long totalBytes, DownloadCounter counter,
        IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        var tempPath = destPath + ".tmp";
        long existingBytes = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existingBytes > 0)
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(tempPath, existingBytes > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write);

        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            counter.BytesDownloaded += read;
            progress.Report(new DownloadProgress(modelId, fileName, counter.BytesDownloaded, totalBytes));
        }

        fileStream.Close();
        File.Move(tempPath, destPath, overwrite: true);
    }

    private static ModelArchitecture DetectArchitecture(string configPath)
    {
        if (!File.Exists(configPath)) return ModelArchitecture.Unknown;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (doc.RootElement.TryGetProperty("architectures", out var arches))
            {
                var archString = arches.EnumerateArray().FirstOrDefault().GetString()?.ToLowerInvariant() ?? "";
                if (archString.Contains("crossencoder") || archString.Contains("sequence_classification"))
                    return ModelArchitecture.CrossEncoder;
                if (archString.Contains("biencoder") || archString.Contains("sentence"))
                    return ModelArchitecture.BiEncoder;
            }
        }
        catch { /* ignore parse errors */ }
        return ModelArchitecture.CrossEncoder; // default assumption for rerankers
    }

    // ── JSON response DTOs ──────────────────────────────────────────

    private sealed class HFApiModelListResponse
    {
        [JsonPropertyName("modelId")] public string? ModelId { get; init; }
        [JsonPropertyName("id")] public string? Id { get; init; }
        [JsonPropertyName("pipeline_tag")] public string? PipelineTag { get; init; }
        [JsonPropertyName("downloads")] public long? Downloads { get; init; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; init; }
    }

    private sealed class HFApiModelDetailResponse
    {
        [JsonPropertyName("sha")] public string? Sha { get; init; }
        [JsonPropertyName("pipeline_tag")] public string? PipelineTag { get; init; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; init; }
        [JsonPropertyName("siblings")] public List<HFSibling>? Siblings { get; init; }
    }

    private sealed class HFSibling
    {
        [JsonPropertyName("rfilename")] public string RFilename { get; init; } = "";
        [JsonPropertyName("size")] public long? Size { get; init; }
        [JsonPropertyName("lfs")] public HFLfs? Lfs { get; init; }
        public string? LfsOid => Lfs?.Oid;
    }

    private sealed class HFLfs
    {
        [JsonPropertyName("oid")] public string? Oid { get; init; }
    }
}
