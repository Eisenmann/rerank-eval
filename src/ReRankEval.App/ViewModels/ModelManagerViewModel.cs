using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.App.ViewModels;

public partial class ModelManagerViewModel : ObservableObject
{
    private readonly IHFHubClient _hubClient;
    private readonly IModelRegistry _registry;

    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private string _downloadStatus = "";
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private string _errorMessage = "";

    public ObservableCollection<HFSearchResult> SearchResults { get; } = [];
    public ObservableCollection<ModelEntry> LocalModels { get; } = [];

    [ObservableProperty] private HFSearchResult? _selectedSearchResult;
    [ObservableProperty] private ModelEntry? _selectedLocalModel;

    private CancellationTokenSource? _downloadCts;

    public ModelManagerViewModel(IHFHubClient hubClient, IModelRegistry registry)
    {
        _hubClient = hubClient;
        _registry = registry;
        _ = LoadLocalModelsAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        IsSearching = true;
        ErrorMessage = "";
        SearchResults.Clear();

        try
        {
            var results = await _hubClient.SearchModelsAsync(SearchQuery);
            foreach (var r in results)
                SearchResults.Add(r);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (SelectedSearchResult is null) return;

        IsDownloading = true;
        DownloadProgress = 0;
        DownloadStatus = $"Starting download of {SelectedSearchResult.ModelId}...";
        ErrorMessage = "";
        _downloadCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<DownloadProgress>(p =>
            {
                DownloadProgress = p.Percentage;
                DownloadStatus = $"{p.FileName}: {p.Percentage:F1}%";
            });

            var entry = await _hubClient.DownloadModelAsync(
                SelectedSearchResult.ModelId, progress, _downloadCts.Token);

            await _registry.RegisterAsync(entry);
            LocalModels.Insert(0, entry);
            DownloadStatus = $"Downloaded {SelectedSearchResult.ModelId} successfully.";
        }
        catch (OperationCanceledException)
        {
            DownloadStatus = "Download cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Download failed: {ex.Message}";
            DownloadStatus = "";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    [RelayCommand]
    private async Task DeleteModelAsync()
    {
        if (SelectedLocalModel is null) return;

        try
        {
            await _registry.UnregisterAsync(SelectedLocalModel.Id);
            if (Directory.Exists(SelectedLocalModel.LocalPath))
                Directory.Delete(SelectedLocalModel.LocalPath, recursive: true);
            LocalModels.Remove(SelectedLocalModel);
            SelectedLocalModel = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Delete failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadLocalModelsAsync();

    private async Task LoadLocalModelsAsync()
    {
        try
        {
            var models = await _registry.ListAsync();
            LocalModels.Clear();
            foreach (var m in models)
                LocalModels.Add(m);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load local models: {ex.Message}";
        }
    }
}
