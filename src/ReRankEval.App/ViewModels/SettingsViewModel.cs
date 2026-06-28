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
    [ObservableProperty] private string _localEndpoint = "http://localhost:1234/v1";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;

    public bool IsAzure  => LlmProvider == "Azure OpenAI";
    public bool IsOllama => LlmProvider == "Ollama";
    public bool IsLocal  => LlmProvider == "Local";
    public bool ShowApiKey => LlmProvider is not "Ollama" and not "Local";

    public string[] Providers { get; } = ["OpenAI", "Azure OpenAI", "Ollama", "Local"];

    public SettingsViewModel(IAppSettingsService settingsService, ICredentialStore credentialStore)
    {
        _settingsService = settingsService;
        _credentialStore = credentialStore;
        _ = LoadAsync();
    }

    partial void OnLlmProviderChanged(string value)
    {
        OnPropertyChanged(nameof(IsAzure));
        OnPropertyChanged(nameof(IsOllama));
        OnPropertyChanged(nameof(IsLocal));
        OnPropertyChanged(nameof(ShowApiKey));
        _ = LoadApiKeyForProviderAsync(value);
    }

    private async Task LoadAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        LlmProvider = settings.LlmProvider;
        ModelId = settings.ModelId;
        AzureEndpoint = settings.AzureEndpoint;
        AzureDeploymentName = settings.AzureDeploymentName;
        LocalEndpoint = string.IsNullOrWhiteSpace(settings.LocalEndpoint)
            ? "http://localhost:1234/v1"
            : settings.LocalEndpoint;
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
                AzureDeploymentName = AzureDeploymentName,
                LocalEndpoint = LocalEndpoint
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
        StatusMessage = "Validating configuration…";
        try
        {
            if (string.IsNullOrWhiteSpace(ModelId))
            {
                StatusMessage = "Model ID is required.";
                return;
            }

            switch (LlmProvider)
            {
                case "OpenAI" when string.IsNullOrWhiteSpace(ApiKey):
                    StatusMessage = "OpenAI API key is required.";
                    return;

                case "Azure OpenAI" when string.IsNullOrWhiteSpace(ApiKey) ||
                    string.IsNullOrWhiteSpace(AzureEndpoint) || string.IsNullOrWhiteSpace(AzureDeploymentName):
                    StatusMessage = "Azure OpenAI requires API key, endpoint, and deployment name.";
                    return;

                case "Local" when string.IsNullOrWhiteSpace(LocalEndpoint):
                    StatusMessage = "Local endpoint URL is required (e.g. http://localhost:1234/v1).";
                    return;

                case "Local" when !Uri.TryCreate(LocalEndpoint, UriKind.Absolute, out var u) ||
                    u.Scheme is not "http" and not "https":
                    StatusMessage = "Local endpoint must be a valid http/https URL.";
                    return;
            }

            await Task.Delay(100);
            StatusMessage = LlmProvider switch
            {
                "Ollama" => "Configuration looks valid. Make sure Ollama is running and the model is pulled.",
                "Local"  => $"Configuration looks valid. Make sure your server is running at {LocalEndpoint}.",
                _        => $"Configuration looks valid for {LlmProvider}. Open AI Agent to test."
            };
        }
        finally
        {
            IsBusy = false;
        }
    }
}
