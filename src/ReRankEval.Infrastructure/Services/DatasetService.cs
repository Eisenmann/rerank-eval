using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.Infrastructure.Services;

public sealed class DatasetService : IDatasetService
{
    private readonly IExperimentStore _store;
    private readonly ILogger<DatasetService> _logger;
    private readonly string _datasetsDir;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<Guid, List<DatasetEntry>> _cache = new();

    public DatasetService(IExperimentStore store, ILogger<DatasetService> logger, string datasetsDir)
    {
        _store = store;
        _logger = logger;
        _datasetsDir = datasetsDir;
        Directory.CreateDirectory(datasetsDir);
    }

    public async Task<Dataset> LoadJsonlAsync(string filePath, string name, CancellationToken ct = default)
    {
        _logger.LogInformation("Loading JSONL dataset from {Path}", filePath);
        var entries = await ParseJsonlAsync(filePath, ct);

        var dataset = CreateDataset(name, filePath, DatasetFormat.Jsonl, entries);
        _cache[dataset.Id] = entries;

        CopyToStore(filePath, dataset.LocalPath);
        return await _store.SaveDatasetAsync(dataset, ct);
    }

    public async Task<Dataset> LoadCsvAsync(string filePath, string name, CancellationToken ct = default)
    {
        _logger.LogInformation("Loading CSV dataset from {Path}", filePath);
        var entries = await ParseCsvAsync(filePath, ct);

        var dataset = CreateDataset(name, filePath, DatasetFormat.Csv, entries);
        _cache[dataset.Id] = entries;

        CopyToStore(filePath, dataset.LocalPath);
        return await _store.SaveDatasetAsync(dataset, ct);
    }

    public async Task<ValidationReport> ValidateAsync(Guid datasetId, CancellationToken ct = default)
    {
        var entries = await GetEntriesAsync(datasetId, ct);
        var errors = new List<ValidationError>();
        var rowIndex = 0;

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Query))
                errors.Add(new ValidationError(rowIndex, "query", "Query is empty"));
            if (entry.Documents.Count == 0)
                errors.Add(new ValidationError(rowIndex, "docs", "No documents"));
            if (entry.Labels.Count != entry.Documents.Count)
                errors.Add(new ValidationError(rowIndex, "labels", $"Labels count {entry.Labels.Count} != documents count {entry.Documents.Count}"));
            if (entry.Labels.Any(l => l < 0))
                errors.Add(new ValidationError(rowIndex, "labels", "Negative relevance label"));
            rowIndex++;
        }

        return new ValidationReport(errors.Count == 0, entries.Count, errors.Count, errors);
    }

    public async Task<DatasetStats> GetStatsAsync(Guid datasetId, CancellationToken ct = default)
    {
        var entries = await GetEntriesAsync(datasetId, ct);
        var relevanceDist = entries
            .SelectMany(e => e.Labels)
            .GroupBy(l => l)
            .ToDictionary(g => g.Key, g => g.Count());

        return new DatasetStats(
            entries.Count,
            entries.Count > 0 ? (float)entries.Average(e => e.Documents.Count) : 0,
            entries.Count > 0 ? (float)entries.Average(e => e.Labels.Count(l => l > 0)) : 0,
            relevanceDist);
    }

    public async Task<IReadOnlyList<DatasetEntry>> GetEntriesAsync(Guid datasetId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(datasetId, out var cached))
            return cached;

        var dataset = await _store.GetDatasetAsync(datasetId, ct)
            ?? throw new InvalidOperationException($"Dataset {datasetId} not found");

        List<DatasetEntry> entries = dataset.Format == DatasetFormat.Csv
            ? await ParseCsvAsync(dataset.LocalPath, ct)
            : await ParseJsonlAsync(dataset.LocalPath, ct);

        _cache[datasetId] = entries;
        return entries;
    }

    public Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken ct = default) =>
        _store.ListDatasetsAsync(ct);

    public Task DeleteAsync(Guid datasetId, CancellationToken ct = default) =>
        _store.DeleteDatasetAsync(datasetId, ct);

    private static async Task<List<DatasetEntry>> ParseJsonlAsync(string filePath, CancellationToken ct)
    {
        var entries = new List<DatasetEntry>();
        foreach (var line in await File.ReadAllLinesAsync(filePath, ct))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var obj = JsonSerializer.Deserialize<JsonlEntry>(line, _json);
            if (obj is null) continue;
            entries.Add(new DatasetEntry(obj.Query ?? "", obj.Docs ?? [], obj.Labels ?? [], obj.DomainTag));
        }
        return entries;
    }

    private static async Task<List<DatasetEntry>> ParseCsvAsync(string filePath, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(filePath, ct);
        if (lines.Length < 2) return [];

        // Expect header: query,document,relevance[,domain_tag]
        var entries = new List<DatasetEntry>();
        string? currentQuery = null;
        var currentDocs = new List<string>();
        var currentLabels = new List<int>();
        string? currentDomain = null;

        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',', 4);
            if (parts.Length < 3) continue;

            var query = parts[0].Trim('"');
            var doc = parts[1].Trim('"');
            var label = int.TryParse(parts[2].Trim('"'), out var l) ? l : 0;
            var domain = parts.Length > 3 ? parts[3].Trim('"') : null;

            if (currentQuery != null && query != currentQuery)
            {
                entries.Add(new DatasetEntry(currentQuery, currentDocs.ToArray(), currentLabels.ToArray(), currentDomain));
                currentDocs.Clear();
                currentLabels.Clear();
            }

            currentQuery = query;
            currentDomain = domain;
            currentDocs.Add(doc);
            currentLabels.Add(label);
        }

        if (currentQuery != null && currentDocs.Count > 0)
            entries.Add(new DatasetEntry(currentQuery, currentDocs.ToArray(), currentLabels.ToArray(), currentDomain));

        return entries;
    }

    private Dataset CreateDataset(string name, string sourcePath, DatasetFormat format, List<DatasetEntry> entries)
    {
        var id = Guid.NewGuid();
        var destDir = Path.Combine(_datasetsDir, id.ToString());
        Directory.CreateDirectory(destDir);
        var destFile = Path.Combine(destDir, Path.GetFileName(sourcePath));

        return new Dataset
        {
            Id = id,
            Name = name,
            Format = format,
            LocalPath = destFile,
            QueryCount = entries.Count,
            AvgDocsPerQuery = entries.Count > 0 ? (float)entries.Average(e => e.Documents.Count) : 0,
            AvgRelevantDocsPerQuery = entries.Count > 0 ? (float)entries.Average(e => e.Labels.Count(l => l > 0)) : 0
        };
    }

    private static void CopyToStore(string source, string dest)
    {
        if (source != dest)
            File.Copy(source, dest, overwrite: true);
    }

    private sealed class JsonlEntry
    {
        [JsonPropertyName("query")] public string? Query { get; init; }
        [JsonPropertyName("docs")] public List<string>? Docs { get; init; }
        [JsonPropertyName("labels")] public List<int>? Labels { get; init; }
        [JsonPropertyName("domain_tag")] public string? DomainTag { get; init; }
    }
}
