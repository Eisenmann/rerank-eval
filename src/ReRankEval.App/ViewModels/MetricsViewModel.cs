using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.App.ViewModels;

public partial class MetricsViewModel : ObservableObject
{
    private readonly IExperimentStore _store;

    [ObservableProperty] private EvaluationRun? _selectedRun;
    [ObservableProperty] private string _errorMessage = "";

    public ObservableCollection<EvaluationRun> Runs { get; } = [];
    public ObservableCollection<ModelMetricRow> MetricRows { get; } = [];

    public MetricsViewModel(IExperimentStore store)
    {
        _store = store;
        _ = LoadRunsAsync();
    }

    partial void OnSelectedRunChanged(EvaluationRun? value)
    {
        if (value != null)
            _ = LoadMetricsAsync(value.Id);
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadRunsAsync();

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
            MetricRows.Clear();
            foreach (var r in results)
            {
                MetricRows.Add(new ModelMetricRow(
                    r.ModelId.ToString(),
                    r.NdcgAt.GetValueOrDefault(10),
                    r.MrrAt.GetValueOrDefault(10),
                    r.MapScore,
                    r.LatencyP50Ms,
                    r.LatencyP90Ms));
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}

public record ModelMetricRow(
    string ModelId,
    double Ndcg10,
    double Mrr10,
    double Map,
    double P50Ms,
    double P90Ms);
