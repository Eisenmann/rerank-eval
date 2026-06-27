using ReRankEval.Domain.Models;
using System.Threading.Channels;

namespace ReRankEval.Domain.Interfaces;

public interface IFineTuningService
{
    Task<TrainingRun> TrainAsync(
        Guid baseModelId,
        Guid datasetId,
        TrainingConfig config,
        ChannelWriter<TrainingMetrics> metricsWriter,
        CancellationToken ct = default);

    Task PauseAsync(Guid trainingRunId, CancellationToken ct = default);
    Task ResumeAsync(Guid trainingRunId, CancellationToken ct = default);
}
