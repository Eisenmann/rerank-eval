using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;
using System.Collections.ObjectModel;
using System.Threading.Channels;

namespace ReRankEval.App.ViewModels;

public partial class FineTuningViewModel : ObservableObject
{
    private readonly IFineTuningService _finetuning;
    private readonly IModelRegistry _registry;
    private readonly IExperimentStore _store;

    // ── Step visibility ──────────────────────────────────────────────

    [ObservableProperty] private bool _isStep1 = true;
    [ObservableProperty] private bool _isStep2;
    [ObservableProperty] private bool _isStep3;

    private int _currentStep = 1;
    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            SetProperty(ref _currentStep, value);
            IsStep1 = value == 1;
            IsStep2 = value == 2;
            IsStep3 = value == 3;
        }
    }

    // ── Step 1: dataset ───────────────────────────────────────────────

    [ObservableProperty] private string _dataFilePath = string.Empty;
    [ObservableProperty] private FineTuningDataFormat _selectedFormat = FineTuningDataFormat.TripletJsonl;
    [ObservableProperty] private FineTuningValidationReport? _validationReport;
    [ObservableProperty] private bool _isValidating;
    [ObservableProperty] private bool _step1Valid;
    [ObservableProperty] private ObservableCollection<string> _validationErrors = [];

    public IReadOnlyList<FineTuningDataFormat> AllFormats { get; } =
        Enum.GetValues<FineTuningDataFormat>();

    // ── Step 2: hyperparameters ───────────────────────────────────────

    [ObservableProperty] private ObservableCollection<ModelEntry> _availableModels = [];
    [ObservableProperty] private ModelEntry? _selectedBaseModel;
    [ObservableProperty] private string _learningRate = "2e-5";
    [ObservableProperty] private int _epochs = 3;
    [ObservableProperty] private int _batchSize = 16;
    [ObservableProperty] private int _frozenLayers = -2;
    [ObservableProperty] private LossFunction _selectedLossFunction = LossFunction.MarginRanking;
    [ObservableProperty] private int _checkpointEvery = 500;
    [ObservableProperty] private bool _isStartingTraining;

    public IReadOnlyList<LossFunction> AllLossFunctions { get; } =
        Enum.GetValues<LossFunction>();

    // ── Step 3: monitor ───────────────────────────────────────────────

    [ObservableProperty] private bool _isTraining;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _pauseButtonLabel = "Pause";
    [ObservableProperty] private int _monitorCurrentEpoch;
    [ObservableProperty] private int _monitorTotalEpochs;
    [ObservableProperty] private int _monitorCurrentStep;
    [ObservableProperty] private float _currentLoss;
    [ObservableProperty] private float _bestValNdcg;
    [ObservableProperty] private string _trainingStatus = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _logLines = [];
    [ObservableProperty] private ObservableCollection<Point> _lossPolylinePoints = [];
    [ObservableProperty] private bool _hasLossData;

    private TrainingRun? _activeRun;
    private CancellationTokenSource? _trainingCts;

    // Canvas for loss curve: x=[40,490]=450, y=[10,155]=145
    private const double LossCanvasX0 = 40, LossCanvasX1 = 490;
    private const double LossCanvasY0 = 10, LossCanvasY1 = 155;
    private readonly List<(int Step, float Loss)> _lossHistory = [];
    private float _maxLoss = 1f;

    public FineTuningViewModel(IFineTuningService finetuning, IModelRegistry registry, IExperimentStore store)
    {
        _finetuning = finetuning;
        _registry = registry;
        _store = store;
    }

    [RelayCommand]
    private async Task LoadModelsAsync()
    {
        var models = await _registry.ListAsync();
        AvailableModels.Clear();
        foreach (var m in models) AvailableModels.Add(m);
    }

    // ── Step 1 actions ────────────────────────────────────────────────

    // Called from code-behind after file picker
    public void SetDataFilePath(string path)
    {
        DataFilePath = path;
        ValidationReport = null;
        Step1Valid = false;
        ValidationErrors.Clear();
    }

    [RelayCommand]
    private async Task ValidateDatasetAsync()
    {
        if (string.IsNullOrEmpty(DataFilePath)) return;
        IsValidating = true;
        Step1Valid = false;
        ValidationErrors.Clear();

        try
        {
            var validator = new Infrastructure.Services.FineTuningDatasetValidator();
            var (report, _) = await validator.ValidateAsync(DataFilePath, SelectedFormat);
            ValidationReport = report;
            foreach (var e in report.Errors.Take(20))
                ValidationErrors.Add(e);
            Step1Valid = report.IsValid && report.TotalRows > 0;
        }
        catch (Exception ex)
        {
            ValidationErrors.Add($"Validation failed: {ex.Message}");
        }
        finally { IsValidating = false; }
    }

    [RelayCommand(CanExecute = nameof(Step1Valid))]
    private void GoToStep2()
    {
        CurrentStep = 2;
        _ = LoadModelsAsync();
    }

    [RelayCommand]
    private void BackToStep1() => CurrentStep = 1;

    // ── Step 2 actions ────────────────────────────────────────────────

    [RelayCommand]
    private async Task StartTrainingAsync()
    {
        if (SelectedBaseModel is null || string.IsNullOrEmpty(DataFilePath)) return;

        if (!float.TryParse(LearningRate.Replace("e", "E"), out var lr))
            lr = 2e-5f;

        var config = new TrainingConfig
        {
            LearningRate = lr,
            Epochs = Epochs,
            BatchSize = BatchSize,
            FrozenLayers = FrozenLayers,
            LossFunction = SelectedLossFunction,
            CheckpointEverySteps = CheckpointEvery
        };

        CurrentStep = 3;
        IsTraining = true;
        IsPaused = false;
        MonitorTotalEpochs = Epochs;
        MonitorCurrentEpoch = 0;
        MonitorCurrentStep = 0;
        BestValNdcg = 0;
        TrainingStatus = "Starting…";
        LogLines.Clear();
        LossPolylinePoints.Clear();
        _lossHistory.Clear();
        _maxLoss = 1f;
        HasLossData = false;

        _trainingCts = new CancellationTokenSource();
        var channel = Channel.CreateUnbounded<TrainingMetrics>();

        // Kick off monitoring in background
        _ = MonitorTrainingAsync(channel.Reader, _trainingCts.Token);

        // Find or create a dataset entry
        var datasets = await _store.ListDatasetsAsync();
        var dataset = datasets.FirstOrDefault(d => d.LocalPath == DataFilePath);
        if (dataset is null)
        {
            dataset = await _store.SaveDatasetAsync(new Dataset
            {
                Name = Path.GetFileNameWithoutExtension(DataFilePath),
                Format = DatasetFormat.Jsonl,
                LocalPath = DataFilePath,
                QueryCount = ValidationReport?.UniqueQueries ?? 0
            });
        }

        try
        {
            _activeRun = await _finetuning.TrainAsync(
                SelectedBaseModel.Id,
                dataset.Id,
                config,
                channel.Writer,
                _trainingCts.Token);

            TrainingStatus = _activeRun.Status switch
            {
                RunStatus.Completed  => $"Training complete — best NDCG@10: {_activeRun.BestValNdcgAt10:F4}",
                RunStatus.Cancelled  => "Training cancelled",
                RunStatus.Failed     => $"Training failed: {_activeRun.ErrorMessage}",
                _                    => "Done"
            };
        }
        catch (OperationCanceledException)
        {
            TrainingStatus = "Training cancelled";
        }
        catch (Exception ex)
        {
            TrainingStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsTraining = false;
        }
    }

    private async Task MonitorTrainingAsync(ChannelReader<TrainingMetrics> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var m in reader.ReadAllAsync(ct))
            {
                MonitorCurrentEpoch = m.Epoch;
                MonitorCurrentStep = m.Step;
                CurrentLoss = m.TrainLoss;

                if (m.TrainLoss > 0)
                {
                    _lossHistory.Add((m.Step, m.TrainLoss));
                    _maxLoss = Math.Max(_maxLoss, m.TrainLoss);
                    RebuildLossCurve();
                }

                if (m.ValNdcgAt10.HasValue)
                {
                    BestValNdcg = Math.Max(BestValNdcg, m.ValNdcgAt10.Value);
                    Log($"Step {m.Step} | Epoch {m.Epoch} | loss={m.TrainLoss:F4} | val NDCG@10={m.ValNdcgAt10.Value:F4} | lr={m.LearningRate:E2}");
                }
                else if (m.Step % 50 == 0)
                {
                    Log($"Step {m.Step} | Epoch {m.Epoch} | loss={m.TrainLoss:F4} | lr={m.LearningRate:E2}");
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void RebuildLossCurve()
    {
        if (_lossHistory.Count < 2) return;
        int n = _lossHistory.Count;
        var pts = new ObservableCollection<Point>();
        for (int i = 0; i < n; i++)
        {
            var (step, loss) = _lossHistory[i];
            double x = LossCanvasX0 + (double)i / (n - 1) * (LossCanvasX1 - LossCanvasX0);
            double y = LossCanvasY1 - (loss / _maxLoss) * (LossCanvasY1 - LossCanvasY0);
            pts.Add(new Point(x, y));
        }
        LossPolylinePoints = pts;
        HasLossData = true;
    }

    [RelayCommand]
    private void PauseTraining()
    {
        if (!IsTraining || _activeRun is null) return;
        IsPaused = !IsPaused;
        PauseButtonLabel = IsPaused ? "Resume" : "Pause";
        if (IsPaused)
            _ = _finetuning.PauseAsync(_activeRun.Id);
        else
            _ = _finetuning.ResumeAsync(_activeRun.Id);
    }

    [RelayCommand]
    private void CancelTraining()
    {
        _trainingCts?.Cancel();
        IsTraining = false;
        TrainingStatus = "Cancelling…";
    }

    [RelayCommand]
    private void BackToStep2()
    {
        if (IsTraining) return;
        CurrentStep = 2;
    }

    private void Log(string msg)
    {
        if (LogLines.Count > 200) LogLines.RemoveAt(0);
        LogLines.Add(msg);
    }
}
