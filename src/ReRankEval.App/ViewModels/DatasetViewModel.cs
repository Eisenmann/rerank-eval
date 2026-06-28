using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.App.ViewModels;

public record RelevanceLevelRow(string Label, int Count);

public partial class DatasetViewModel : ObservableObject
{
    private readonly IDatasetService _datasetService;

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Dataset? _selectedDataset;
    [ObservableProperty] private ValidationReport? _validationReport;
    [ObservableProperty] private bool _validationPassed;
    [ObservableProperty] private bool _hasRelevanceData;

    public ObservableCollection<Dataset> Datasets { get; } = [];
    public ObservableCollection<ValidationError> ValidationErrors { get; } = [];
    public ObservableCollection<RelevanceLevelRow> RelevanceDistributionRows { get; } = [];

    public DatasetViewModel(IDatasetService datasetService)
    {
        _datasetService = datasetService;
        _ = RefreshListAsync();
    }

    partial void OnSelectedDatasetChanged(Dataset? value)
    {
        ValidationReport = null;
        ValidationErrors.Clear();
        UpdateRelevanceDistribution(value);
    }

    [RelayCommand]
    public async Task LoadJsonlAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        IsLoading = true;
        StatusMessage = $"Loading {Path.GetFileName(filePath)}...";
        try
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var dataset = await _datasetService.LoadJsonlAsync(filePath, name);
            Datasets.Insert(0, dataset);
            SelectedDataset = dataset;
            StatusMessage = $"Loaded {dataset.QueryCount} queries.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task LoadCsvAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        IsLoading = true;
        StatusMessage = $"Loading {Path.GetFileName(filePath)}...";
        try
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var dataset = await _datasetService.LoadCsvAsync(filePath, name);
            Datasets.Insert(0, dataset);
            SelectedDataset = dataset;
            StatusMessage = $"Loaded {dataset.QueryCount} queries.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        if (SelectedDataset is null) return;
        IsLoading = true;
        StatusMessage = "Validating...";
        try
        {
            var report = await _datasetService.ValidateAsync(SelectedDataset.Id);
            ValidationReport = report;
            ValidationPassed = report.IsValid;
            ValidationErrors.Clear();
            foreach (var e in report.Errors) ValidationErrors.Add(e);
            StatusMessage = report.IsValid
                ? $"Valid — {report.TotalRows} rows, no errors."
                : $"{report.InvalidRows} of {report.TotalRows} rows have errors.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedDataset is null) return;
        try
        {
            await _datasetService.DeleteAsync(SelectedDataset.Id);
            Datasets.Remove(SelectedDataset);
            SelectedDataset = null;
            StatusMessage = "Dataset deleted.";
        }
        catch (Exception ex) { StatusMessage = $"Delete failed: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await RefreshListAsync();

    private async Task RefreshListAsync()
    {
        try
        {
            var list = await _datasetService.ListAsync();
            Datasets.Clear();
            foreach (var d in list) Datasets.Add(d);
        }
        catch (Exception ex) { StatusMessage = $"Load failed: {ex.Message}"; }
    }

    private void UpdateRelevanceDistribution(Dataset? dataset)
    {
        RelevanceDistributionRows.Clear();
        if (dataset is null) return;
        // Stats are computed separately via GetStatsAsync — show static info from Dataset record here
    }

    public async Task LoadStatsForSelectedAsync()
    {
        if (SelectedDataset is null) return;
        try
        {
            var stats = await _datasetService.GetStatsAsync(SelectedDataset.Id);
            RelevanceDistributionRows.Clear();
            foreach (var kv in stats.RelevanceDistribution.OrderBy(x => x.Key))
                RelevanceDistributionRows.Add(new RelevanceLevelRow($"Relevance {kv.Key}", kv.Value));
            HasRelevanceData = RelevanceDistributionRows.Count > 0;
        }
        catch { /* non-critical */ }
    }
}
