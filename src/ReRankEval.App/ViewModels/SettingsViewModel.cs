using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settingsService;
    private readonly ICredentialStore _credentialStore;

    [ObservableProperty] private string _llmProvider = "OpenAI";
    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string _modelId = "gpt-4o-mini";
    [ObservableProperty] private string _azureEndpoint = "";
    [ObservableProperty] private string _azureDeploymentName = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;

    public bool IsAzure => LlmProvider == "Azure OpenAI";
    public bool ShowApiKey => LlmProvider != "Ollama";
    public string[] Providers { get; } = ["OpenAI", "Azure OpenAI", "Ollama"];

    public SettingsViewModel(IAppSettingsService settingsService, ICredentialStore credentialStore)
    {
        _settingsService = settingsService;
        _credentialStore = credentialStore;
        _ = LoadAsync();
    }

    partial void OnLlmProviderChanged(string value)
    {
        OnPropertyChanged(nameof(IsAzure));
        OnPropertyChanged(nameof(ShowApiKey));
        // Load the API key for the new provider
        _ = LoadApiKeyForProviderAsync(value);
    }

    private async Task LoadAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        LlmProvider = settings.LlmProvider;
        ModelId = settings.ModelId;
        AzureEndpoint = settings.AzureEndpoint;
        AzureDeploymentName = settings.AzureDeploymentName;
        ApiKey = await _credentialStore.LoadAsync($"llm:{settings.LlmProvider}") ?? "";
    }

    private async Task LoadApiKeyForProviderAsync(string provider)
    {
        ApiKey = await _credentialStore.LoadAsync($"llm:{provider}") ?? "";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = "";
        try
        {
            var settings = new AppSettings
            {
                LlmProvider = LlmProvider,
                ModelId = ModelId,
                AzureEndpoint = AzureEndpoint,
                AzureDeploymentName = AzureDeploymentName
            };
            await _settingsService.SaveSettingsAsync(settings);
            if (!string.IsNullOrWhiteSpace(ApiKey))
                await _credentialStore.SaveAsync($"llm:{LlmProvider}", ApiKey);
            StatusMessage = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = "Testing connection...";
        try
        {
            // Simple validation: check that required fields are set
            if (LlmProvider == "OpenAI" && string.IsNullOrWhiteSpace(ApiKey))
            {
                StatusMessage = "OpenAI API key is required.";
                return;
            }
            if (LlmProvider == "Azure OpenAI" && (string.IsNullOrWhiteSpace(ApiKey) ||
                string.IsNullOrWhiteSpace(AzureEndpoint) || string.IsNullOrWhiteSpace(AzureDeploymentName)))
            {
                StatusMessage = "Azure OpenAI requires API key, endpoint, and deployment name.";
                return;
            }
            if (string.IsNullOrWhiteSpace(ModelId))
            {
                StatusMessage = "Model ID is required.";
                return;
            }

            // Actual connection test would require building a kernel and making a call
            // For now, validate config fields are set
            await Task.Delay(300); // simulate
            StatusMessage = $"Configuration looks valid for {LlmProvider}. Save and try the AI Agent page to test.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
