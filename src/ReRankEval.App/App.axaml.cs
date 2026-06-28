using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReRankEval.Agent;
using ReRankEval.App.ViewModels;
using ReRankEval.App.Views;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Services;
using ReRankEval.Infrastructure.Data;
using ReRankEval.Infrastructure.Services;
using Serilog;

namespace ReRankEval.App;

public class ReRankApp : Avalonia.Application
{
    private IHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".rerank_eval");
        Directory.CreateDirectory(appDataDir);

        ConfigureLogging(appDataDir);

        _host = CreateHost(appDataDir);
        await _host.StartAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = _host.Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainVm };
            desktop.ShutdownRequested += async (_, _) => await _host.StopAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IHost CreateHost(string appDataDir)
    {
        var dbPath = Path.Combine(appDataDir, "experiments.db");
        var modelsDir = Path.Combine(appDataDir, "models");
        var datasetsDir = Path.Combine(appDataDir, "datasets");

        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddDbContextFactory<ExperimentDbContext>(opts =>
                    opts.UseSqlite($"Data Source={dbPath}"));

                // Core singletons
                services.AddSingleton<IExperimentStore, SqliteExperimentStore>();
                services.AddSingleton<IModelRegistry, LocalModelRegistry>();
                services.AddSingleton<IMetricsCalculator, MetricsCalculator>();
                services.AddSingleton<ITokenizerService, HFTokenizerService>();
                services.AddSingleton<IAnalyticsService, AnalyticsService>();
                services.AddSingleton<IAnalysisService, AnalysisService>();

                // Phase 4 — credential store and settings
                services.AddSingleton<ICredentialStore>(_ => new FileCredentialStore(appDataDir));
                services.AddSingleton<IAppSettingsService>(_ => new AppSettingsService(appDataDir));

                // Fine-tuning
                services.AddSingleton<IFineTuningService, TorchSharpFineTuningService>();

                services.AddHttpClient(nameof(HuggingFaceHubClient), c =>
                {
                    c.Timeout = TimeSpan.FromMinutes(60);
                    c.DefaultRequestHeaders.UserAgent.ParseAdd("RerankEval/1.0");
                });

                services.AddSingleton<IHFHubClient>(sp =>
                {
                    var http = sp.GetRequiredService<IHttpClientFactory>()
                        .CreateClient(nameof(HuggingFaceHubClient));
                    var logger = sp.GetRequiredService<ILogger<HuggingFaceHubClient>>();
                    return new HuggingFaceHubClient(http, logger, modelsDir);
                });

                services.AddScoped<IInferenceService, OnnxInferenceService>(sp =>
                    new OnnxInferenceService(
                        sp.GetRequiredService<ITokenizerService>(),
                        sp.GetRequiredService<ILogger<OnnxInferenceService>>()));

                services.AddScoped<IDatasetService, DatasetService>(sp =>
                    new DatasetService(
                        sp.GetRequiredService<IExperimentStore>(),
                        sp.GetRequiredService<ILogger<DatasetService>>(),
                        datasetsDir));

                services.AddScoped<IEvaluationService, EvaluationService>();

                // Phase 4 — Semantic Kernel agent orchestrator (replaces stub)
                services.AddSingleton<IAgentOrchestrator>(sp =>
                    new SemanticKernelAgentOrchestrator(
                        sp.GetRequiredService<IExperimentStore>(),
                        sp.GetRequiredService<IModelRegistry>(),
                        sp.GetRequiredService<IHFHubClient>(),
                        sp.GetRequiredService<IAnalysisService>(),
                        sp.GetRequiredService<IFineTuningService>(),
                        sp.GetRequiredService<IAppSettingsService>(),
                        sp.GetRequiredService<ICredentialStore>(),
                        sp.GetRequiredService<ILogger<SemanticKernelAgentOrchestrator>>(),
                        appDataDir));

                // ViewModels
                services.AddSingleton<MainWindowViewModel>();
                services.AddTransient<ModelManagerViewModel>();
                services.AddTransient<DatasetViewModel>();
                services.AddTransient<EvaluationViewModel>();
                services.AddTransient<MetricsViewModel>();
                services.AddTransient<ExperimentHistoryViewModel>();
                services.AddTransient<DeepAnalysisViewModel>();
                services.AddTransient<FineTuningViewModel>();
                services.AddTransient<AgentViewModel>();
                services.AddTransient<SettingsViewModel>();
            })
            .Build();
    }

    private static void ConfigureLogging(string appDataDir)
    {
        var logsDir = Path.Combine(appDataDir, "logs");
        Directory.CreateDirectory(logsDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(logsDir, "app_.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }
}
