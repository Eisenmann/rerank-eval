using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ReRankEval.App.ViewModels;

public enum AppPage { Models, Datasets, Evaluation, Metrics, History, Analysis, FineTuning, Agent, Settings }

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty] private AppPage _currentPage = AppPage.Models;
    [ObservableProperty] private ObservableObject? _currentViewModel;

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
        NavigateTo(AppPage.Models);
    }

    [RelayCommand]
    private void NavigateTo(AppPage page)
    {
        CurrentPage = page;
        CurrentViewModel = page switch
        {
            AppPage.Models      => _services.GetRequiredService<ModelManagerViewModel>(),
            AppPage.Datasets    => _services.GetRequiredService<DatasetViewModel>(),
            AppPage.Evaluation  => _services.GetRequiredService<EvaluationViewModel>(),
            AppPage.Metrics     => _services.GetRequiredService<MetricsViewModel>(),
            AppPage.History     => _services.GetRequiredService<ExperimentHistoryViewModel>(),
            AppPage.Analysis    => _services.GetRequiredService<DeepAnalysisViewModel>(),
            AppPage.FineTuning  => _services.GetRequiredService<FineTuningViewModel>(),
            AppPage.Agent       => _services.GetRequiredService<AgentViewModel>(),
            AppPage.Settings    => _services.GetRequiredService<SettingsViewModel>(),
            _                   => CurrentViewModel
        };
    }
}
