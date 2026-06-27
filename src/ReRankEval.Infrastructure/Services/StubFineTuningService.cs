using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;
using System.Threading.Channels;

namespace ReRankEval.Infrastructure.Services;

/// <summary>
/// Stub implementation — Phase 3 will replace with TorchSharp training loop.
/// </summary>
public sealed class StubFineTuningService : IFineTuningService
{
    private readonly IExperimentStore _store;

    public StubFineTuningService(IExperimentStore store)
    {
        _store = store;
    }

    public async Task<TrainingRun> TrainAsync(
        Guid baseModelId,
        Guid datasetId,
        TrainingConfig config,
        ChannelWriter<TrainingMetrics> metricsWriter,
        CancellationToken ct = default)
    {
        var run = new TrainingRun
        {
            BaseModelId = baseModelId,
            DatasetId = datasetId,
            Config = config,
            Status = RunStatus.Failed,
            ErrorMessage = "Fine-tuning is not yet implemented. Install TorchSharp in Phase 3."
        };
        return await _store.SaveTrainingRunAsync(run, ct);
    }

    public Task PauseAsync(Guid trainingRunId, CancellationToken ct = default) => Task.CompletedTask;
    public Task ResumeAsync(Guid trainingRunId, CancellationToken ct = default) => Task.CompletedTask;
}
