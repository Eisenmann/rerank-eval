using System.Text.Json;
using ReRankEval.Domain.Models;

namespace ReRankEval.Infrastructure.Services;

public sealed class FineTuningDatasetValidator
{
    public async Task<(FineTuningValidationReport Report, IReadOnlyList<FineTuningExample> Examples)> ValidateAsync(
        string filePath,
        FineTuningDataFormat format,
        CancellationToken ct = default)
    {
        return format switch
        {
            FineTuningDataFormat.TripletJsonl  => await ValidateTripletJsonlAsync(filePath, ct),
            FineTuningDataFormat.PairwiseJsonl => await ValidatePairwiseJsonlAsync(filePath, ct),
            FineTuningDataFormat.Csv           => await ValidateCsvAsync(filePath, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static async Task<(FineTuningValidationReport, IReadOnlyList<FineTuningExample>)> ValidateTripletJsonlAsync(
        string filePath, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(filePath, ct);
        var errors = new List<string>();
        var examples = new List<FineTuningExample>();
        var queries = new HashSet<string>();
        int rowIndex = 0;

        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            rowIndex++;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var query = root.TryGetProperty("query", out var q) ? q.GetString() : null;
                var positive = root.TryGetProperty("positive", out var p) ? p.GetString() : null;
                var negative = root.TryGetProperty("negative", out var n) ? n.GetString() : null;

                if (string.IsNullOrEmpty(query))   errors.Add($"Row {rowIndex}: missing 'query'");
                if (string.IsNullOrEmpty(positive)) errors.Add($"Row {rowIndex}: missing 'positive'");
                if (string.IsNullOrEmpty(negative)) errors.Add($"Row {rowIndex}: missing 'negative'");

                if (!string.IsNullOrEmpty(query) && !string.IsNullOrEmpty(positive) && !string.IsNullOrEmpty(negative))
                {
                    examples.Add(new FineTuningExample(query!, positive!, negative!));
                    queries.Add(query!);
                }
            }
            catch (JsonException)
            {
                errors.Add($"Row {rowIndex}: invalid JSON");
            }
        }

        var report = new FineTuningValidationReport(
            IsValid: errors.Count == 0,
            TotalRows: rowIndex,
            InvalidRows: errors.Count,
            UniqueQueries: queries.Count,
            Errors: errors);

        return (report, examples);
    }

    private static async Task<(FineTuningValidationReport, IReadOnlyList<FineTuningExample>)> ValidatePairwiseJsonlAsync(
        string filePath, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(filePath, ct);
        var errors = new List<string>();
        var examples = new List<FineTuningExample>();
        var queries = new HashSet<string>();
        int rowIndex = 0;

        foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            rowIndex++;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var query = root.TryGetProperty("query", out var q) ? q.GetString() : null;
                var doc1 = root.TryGetProperty("doc1", out var d1) ? d1.GetString() : null;
                var doc2 = root.TryGetProperty("doc2", out var d2) ? d2.GetString() : null;
                var hasLabel = root.TryGetProperty("label", out var lbl);
                int label = hasLabel ? lbl.GetInt32() : -1;

                if (string.IsNullOrEmpty(query)) errors.Add($"Row {rowIndex}: missing 'query'");
                if (string.IsNullOrEmpty(doc1))  errors.Add($"Row {rowIndex}: missing 'doc1'");
                if (string.IsNullOrEmpty(doc2))  errors.Add($"Row {rowIndex}: missing 'doc2'");
                if (!hasLabel || label is not (0 or 1)) errors.Add($"Row {rowIndex}: 'label' must be 0 or 1");

                if (!string.IsNullOrEmpty(query) && !string.IsNullOrEmpty(doc1) && !string.IsNullOrEmpty(doc2) && label is 0 or 1)
                {
                    // label=1 means doc1 is better; label=0 means doc2 is better
                    var (pos, neg) = label == 1 ? (doc1!, doc2!) : (doc2!, doc1!);
                    examples.Add(new FineTuningExample(query!, pos, neg));
                    queries.Add(query!);
                }
            }
            catch (JsonException)
            {
                errors.Add($"Row {rowIndex}: invalid JSON");
            }
        }

        var report = new FineTuningValidationReport(
            IsValid: errors.Count == 0,
            TotalRows: rowIndex,
            InvalidRows: errors.Count,
            UniqueQueries: queries.Count,
            Errors: errors);

        return (report, examples);
    }

    private static async Task<(FineTuningValidationReport, IReadOnlyList<FineTuningExample>)> ValidateCsvAsync(
        string filePath, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(filePath, ct);
        var errors = new List<string>();
        var queries = new HashSet<string>();
        var rows = new List<(string Query, string Doc, int Relevance)>();
        int rowIndex = 0;

        foreach (var line in lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            rowIndex++;
            var parts = line.Split(',', 3);
            if (parts.Length < 3)
            {
                errors.Add($"Row {rowIndex}: expected 3 columns (query,document,relevance)");
                continue;
            }

            var query = parts[0].Trim('"', ' ');
            var doc = parts[1].Trim('"', ' ');
            if (!int.TryParse(parts[2].Trim(), out var relevance))
            {
                errors.Add($"Row {rowIndex}: relevance must be integer");
                continue;
            }

            rows.Add((query, doc, relevance));
            queries.Add(query);
        }

        // Convert to triplets by pairing positive (relevance>0) and negative (relevance=0) per query
        var examples = new List<FineTuningExample>();
        foreach (var g in rows.GroupBy(r => r.Query))
        {
            var positives = g.Where(r => r.Relevance > 0).Select(r => r.Doc).ToList();
            var negatives = g.Where(r => r.Relevance == 0).Select(r => r.Doc).ToList();
            var pairs = Math.Min(positives.Count, negatives.Count);
            for (int i = 0; i < pairs; i++)
                examples.Add(new FineTuningExample(g.Key, positives[i], negatives[i]));
        }

        var report = new FineTuningValidationReport(
            IsValid: errors.Count == 0,
            TotalRows: rowIndex,
            InvalidRows: errors.Count,
            UniqueQueries: queries.Count,
            Errors: errors);

        return (report, examples);
    }
}
