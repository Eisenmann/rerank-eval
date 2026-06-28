using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.App.ViewModels;

public record TrendDot(double CanvasX, double CanvasY, string RunName, double Ndcg10);

public partial class ExperimentHistoryViewModel : ObservableObject
{
    private readonly IAnalyticsService _analytics;
    private readonly IExperimentStore _store;

    [ObservableProperty] private Dataset? _selectedDataset;
    [ObservableProperty] private LeaderboardEntry? _selectedLeaderboardEntry;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _hasTrendData;
    [ObservableProperty] private string _trendYMinLabel = "";
    [ObservableProperty] private string _trendYMaxLabel = "";
    [ObservableProperty] private string _trendRunCountLabel = "";

    public ObservableCollection<Dataset> Datasets { get; } = [];
    public ObservableCollection<LeaderboardEntry> Leaderboard { get; } = [];
    public ObservableCollection<TrendDot> TrendDots { get; } = [];
    public ObservableCollection<Point> TrendPolylinePoints { get; } = [];

    public ExperimentHistoryViewModel(IAnalyticsService analytics, IExperimentStore store)
    {
        _analytics = analytics;
        _store = store;
        _ = LoadDatasetsAsync();
    }

    partial void OnSelectedDatasetChanged(Dataset? value)
    {
        if (value != null) _ = LoadLeaderboardAsync(value.Id);
        else
        {
            Leaderboard.Clear();
            ClearTrend();
        }
    }

    partial void OnSelectedLeaderboardEntryChanged(LeaderboardEntry? value)
    {
        if (value != null) _ = LoadTrendAsync(value.ModelId);
        else ClearTrend();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadDatasetsAsync();

    private async Task LoadDatasetsAsync()
    {
        try
        {
            var datasets = await _store.ListDatasetsAsync();
            Datasets.Clear();
            foreach (var d in datasets) Datasets.Add(d);
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task LoadLeaderboardAsync(Guid datasetId)
    {
        StatusMessage = "Loading leaderboard...";
        try
        {
            var entries = await _analytics.GetModelLeaderboardAsync(datasetId);
            Leaderboard.Clear();
            foreach (var e in entries) Leaderboard.Add(e);
            StatusMessage = $"{entries.Count} model(s) evaluated on this dataset.";
            ClearTrend();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task LoadTrendAsync(Guid modelId)
    {
        StatusMessage = "Loading trend...";
        try
        {
            var datasetId = SelectedDataset?.Id;
            var trend = await _analytics.GetNdcgTrendAsync(modelId, datasetId);

            ClearTrend();
            if (trend.Count == 0) { StatusMessage = "No trend data."; return; }

            BuildTrendChart(trend);
            StatusMessage = $"{trend.Count} run(s).";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private void BuildTrendChart(IReadOnlyList<NdcgTrendPoint> trend)
    {
        // Canvas usable: x=[40,490]=450 wide, y=[10,155]=145 tall; axis at y=160
        const double xLeft = 40, xRight = 490, yTop = 10, yBottom = 155;
        const double w = xRight - xLeft, h = yBottom - yTop;

        double minNdcg = trend.Min(p => p.Ndcg10);
        double maxNdcg = trend.Max(p => p.Ndcg10);
        double range = maxNdcg - minNdcg;

        TrendYMinLabel = $"{minNdcg:F2}";
        TrendYMaxLabel = $"{maxNdcg:F2}";
        TrendRunCountLabel = $"{trend.Count} runs";

        TrendPolylinePoints.Clear();
        TrendDots.Clear();

        for (int i = 0; i < trend.Count; i++)
        {
            double cx = trend.Count == 1
                ? xLeft + w / 2
                : xLeft + (double)i / (trend.Count - 1) * w;
            double cy = range < 1e-9
                ? yTop + h / 2
                : yBottom - (trend[i].Ndcg10 - minNdcg) / range * h;

            TrendPolylinePoints.Add(new Point(cx, cy));
            TrendDots.Add(new TrendDot(cx - 4, cy - 4, trend[i].RunName, trend[i].Ndcg10));
        }

        HasTrendData = true;
    }

    private void ClearTrend()
    {
        TrendPolylinePoints.Clear();
        TrendDots.Clear();
        HasTrendData = false;
    }
}
