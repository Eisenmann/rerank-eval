using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.App.ViewModels;

public partial class EvaluationViewModel : ObservableObject
{
    private readonly IModelRegistry _registry;
    private readonly IDatasetService _datasetService;
    private readonly IEvaluationService _evalService;
    private readonly IExperimentStore _store;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private string _runName = "Eval-" + DateTime.Now.ToString("yyyyMMdd-HHmm");
    [ObservableProperty] private string _kValues = "1,5,10";
    [ObservableProperty] private int _batchSize = 8;
    [ObservableProperty] private ExecutionProvider _selectedProvider = ExecutionProvider.Cpu;

    [ObservableProperty] private Dataset? _selectedDataset;
    [ObservableProperty] private EvaluationRun? _selectedRun;

    public ObservableCollection<ModelEntry> AvailableModels { get; } = [];
    public ObservableCollection<ModelEntry> SelectedModels { get; } = [];
    public ObservableCollection<Dataset> Datasets { get; } = [];
    public ObservableCollection<EvaluationRun> RunHistory { get; } = [];
    public ExecutionProvider[] Providers { get; } = Enum.GetValues<ExecutionProvider>();

    private CancellationTokenSource? _runCts;

    public EvaluationViewModel(
        IModelRegistry registry,
        IDatasetService datasetService,
        IEvaluationService evalService,
        IExperimentStore store)
    {
        _registry = registry;
        _datasetService = datasetService;
        _evalService = evalService;
        _store = store;
        _ = LoadDataAsync();
    }

    [RelayCommand]
    private async Task RunEvaluationAsync()
    {
        if (SelectedModels.Count == 0 || SelectedDataset is null) return;

        IsRunning = true;
        Progress = 0;
        ProgressText = "Starting evaluation...";
        ErrorMessage = "";
        _runCts = new CancellationTokenSource();

        try
        {
            var kVals = KValues.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.Parse(s.Trim())).ToList();

            var request = new EvaluationRequest(
                RunName,
                SelectedModels.Select(m => m.Id).ToList(),
                SelectedDataset.Id,
                kVals,
                BatchSize,
                SelectedProvider);

            var progressReporter = new Progress<EvaluationProgress>(p =>
            {
                Progress = (double)p.QueriesCompleted / p.TotalQueries * 100;
                ProgressText = $"[{p.CurrentModelId}] {p.Phase}: {p.QueriesCompleted}/{p.TotalQueries}";
            });

            var run = await _evalService.RunAsync(request, progressReporter, _runCts.Token);
            RunHistory.Insert(0, run);
            SelectedRun = run;
            ProgressText = "Evaluation complete!";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Evaluation cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Evaluation failed: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void CancelRun() => _runCts?.Cancel();

    [RelayCommand]
    private void AddModel(ModelEntry model)
    {
        if (!SelectedModels.Contains(model))
            SelectedModels.Add(model);
    }

    [RelayCommand]
    private void RemoveModel(ModelEntry model) => SelectedModels.Remove(model);

    [RelayCommand]
    private async Task RefreshAsync() => await LoadDataAsync();

    private async Task LoadDataAsync()
    {
        try
        {
            var models = await _registry.ListAsync();
            AvailableModels.Clear();
            foreach (var m in models) AvailableModels.Add(m);

            var datasets = await _datasetService.ListAsync();
            Datasets.Clear();
            foreach (var d in datasets) Datasets.Add(d);

            var runs = await _store.ListRunsAsync();
            RunHistory.Clear();
            foreach (var r in runs) RunHistory.Add(r);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Load failed: {ex.Message}";
        }
    }
}
