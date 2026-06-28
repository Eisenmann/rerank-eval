using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.App.ViewModels;

public record ModelMetricRow(
    Guid ModelResultId,
    string ModelId,
    double Ndcg10,
    double Mrr10,
    double Map,
    double P50Ms,
    double P90Ms,
    bool IsBest = false)
{
    public string RowBackground => IsBest ? "#1A4CAF50" : "Transparent";
}

public record ScatterPoint(
    string ShortLabel,
    double CanvasX, double CanvasY,
    double LabelX, double LabelY,
    double Ndcg10, double LatencyMs);

public record HistogramBin(
    string RangeLabel, int Count,
    double BarX, double BarTop, double BarHeight, double LabelX);

public partial class MetricsViewModel : ObservableObject
{
    private readonly IExperimentStore _store;
    private readonly IModelRegistry _registry;

    [ObservableProperty] private EvaluationRun? _selectedRun;
    [ObservableProperty] private ModelMetricRow? _selectedMetricRow;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _hasChartData;
    [ObservableProperty] private string _scatterXMinLabel = "";
    [ObservableProperty] private string _scatterXMaxLabel = "";
    [ObservableProperty] private string _scatterYMinLabel = "";
    [ObservableProperty] private string _scatterYMaxLabel = "";
    [ObservableProperty] private string _histogramMaxLabel = "";
    [ObservableProperty] private bool _hasHistogramData;

    public ObservableCollection<EvaluationRun> Runs { get; } = [];
    public ObservableCollection<ModelMetricRow> MetricRows { get; } = [];
    public ObservableCollection<ScatterPoint> ScatterPoints { get; } = [];
    public ObservableCollection<HistogramBin> HistogramBins { get; } = [];

    public MetricsViewModel(IExperimentStore store, IModelRegistry registry)
    {
        _store = store;
        _registry = registry;
        _ = LoadRunsAsync();
    }

    partial void OnSelectedRunChanged(EvaluationRun? value)
    {
        if (value != null) _ = LoadMetricsAsync(value.Id);
    }

    partial void OnSelectedMetricRowChanged(ModelMetricRow? value)
    {
        if (value != null) _ = LoadHistogramAsync(value.ModelResultId);
        else { HistogramBins.Clear(); HasHistogramData = false; }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadRunsAsync();

    public async Task ExportCsvAsync(string filePath)
    {
        var lines = new List<string> { "ModelId,NDCG@10,MRR@10,MAP,P50Ms,P90Ms" };
        lines.AddRange(MetricRows.Select(r =>
            $"{r.ModelId},{r.Ndcg10:F4},{r.Mrr10:F4},{r.Map:F4},{r.P50Ms:F1},{r.P90Ms:F1}"));
        await File.WriteAllLinesAsync(filePath, lines);
    }

    public async Task ExportJsonAsync(string filePath)
    {
        var data = MetricRows.Select(r => new
        {
            r.ModelId, NDCG10 = r.Ndcg10, MRR10 = r.Mrr10, MAP = r.Map,
            LatencyP50Ms = r.P50Ms, LatencyP90Ms = r.P90Ms
        });
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    private async Task LoadRunsAsync()
    {
        try
        {
            var runs = await _store.ListRunsAsync();
            Runs.Clear();
            foreach (var r in runs) Runs.Add(r);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task LoadMetricsAsync(Guid runId)
    {
        try
        {
            var results = await _store.GetModelResultsAsync(runId);
            var models = await _registry.ListAsync();
            var labels = models.ToDictionary(m => m.Id, m => m.HuggingFaceId);

            double bestNdcg = results.Count > 0 ? results.Max(r => r.NdcgAt.GetValueOrDefault(10)) : 0;

            MetricRows.Clear();
            foreach (var r in results)
            {
                var label = labels.TryGetValue(r.ModelId, out var hf) ? hf : r.ModelId.ToString()[..8];
                var ndcg = r.NdcgAt.GetValueOrDefault(10);
                MetricRows.Add(new ModelMetricRow(
                    r.Id, label, ndcg,
                    r.MrrAt.GetValueOrDefault(10), r.MapScore,
                    r.LatencyP50Ms, r.LatencyP90Ms,
                    IsBest: results.Count > 1 && Math.Abs(ndcg - bestNdcg) < 1e-9));
            }

            BuildScatterPlot(results, labels);
            HasChartData = MetricRows.Count > 0;
            HistogramBins.Clear();
            SelectedMetricRow = null;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task LoadHistogramAsync(Guid modelResultId)
    {
        try
        {
            var queries = await _store.GetQueryResultsAsync(modelResultId);
            BuildHistogram(queries.Select(q => q.NdcgAt10).ToList());
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private void BuildScatterPlot(IReadOnlyList<ModelEvalResult> results, Dictionary<Guid, string> labels)
    {
        ScatterPoints.Clear();
        if (results.Count == 0) return;

        // Canvas usable area: x=[45,385]=340 wide, y=[10,200]=190 tall
        const double xMin = 45, xMax = 385, yMin = 10, yMax = 200;
        const double w = xMax - xMin, h = yMax - yMin;

        double minLat = results.Min(r => r.LatencyP50Ms);
        double maxLat = results.Max(r => r.LatencyP50Ms);
        double minNdcg = results.Min(r => r.NdcgAt.GetValueOrDefault(10));
        double maxNdcg = results.Max(r => r.NdcgAt.GetValueOrDefault(10));

        double latRange = maxLat - minLat;
        double ndcgRange = maxNdcg - minNdcg;

        ScatterXMinLabel = $"{minLat:F0}ms";
        ScatterXMaxLabel = $"{maxLat:F0}ms";
        ScatterYMinLabel = $"{minNdcg:F2}";
        ScatterYMaxLabel = $"{maxNdcg:F2}";

        foreach (var r in results)
        {
            var label = labels.TryGetValue(r.ModelId, out var hf) ? hf : r.ModelId.ToString()[..8];
            var shortLabel = label.Length > 18 ? label[^18..] : label;
            var lat = r.LatencyP50Ms;
            var ndcg = r.NdcgAt.GetValueOrDefault(10);

            double cx = latRange < 0.001
                ? xMin + w / 2
                : xMin + (lat - minLat) / latRange * w;
            double cy = ndcgRange < 0.001
                ? yMin + h / 2
                : yMax - (ndcg - minNdcg) / ndcgRange * h;

            ScatterPoints.Add(new ScatterPoint(
                shortLabel,
                cx - 5, cy - 5,         // center the 10px ellipse
                cx + 7, cy - 14,         // label above-right
                ndcg, lat));
        }
    }

    private void BuildHistogram(IList<double> ndcgValues)
    {
        HistogramBins.Clear();
        if (ndcgValues.Count == 0) return;

        // 10 bins: [0,0.1), [0.1,0.2), ..., [0.9,1.0]
        var counts = new int[10];
        foreach (var v in ndcgValues)
        {
            int bin = Math.Min((int)(v * 10), 9);
            counts[bin]++;
        }

        int maxCount = counts.Max();
        HistogramMaxLabel = maxCount.ToString();

        // Canvas: x=[40,390]=350 wide, axis at y=155; bar max height=140
        for (int i = 0; i < 10; i++)
        {
            double barHeight = maxCount > 0 ? (double)counts[i] / maxCount * 140 : 0;
            double barX = 40 + i * 35 + 2;
            HistogramBins.Add(new HistogramBin(
                $".{i * 10:D2}",
                counts[i],
                barX, 155 - barHeight, barHeight, barX));
        }

        HasHistogramData = true;
    }
}
