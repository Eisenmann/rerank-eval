using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public interface IExperimentStore
{
    // Evaluation runs
    Task<EvaluationRun> SaveRunAsync(EvaluationRun run, CancellationToken ct = default);
    Task<EvaluationRun?> GetRunAsync(Guid runId, CancellationToken ct = default);
    Task<IReadOnlyList<EvaluationRun>> ListRunsAsync(Guid? modelId = null, Guid? datasetId = null, CancellationToken ct = default);
    Task UpdateRunStatusAsync(Guid runId, RunStatus status, string? errorMessage = null, CancellationToken ct = default);

    // Model results
    Task SaveModelResultAsync(ModelEvalResult result, CancellationToken ct = default);
    Task<IReadOnlyList<ModelEvalResult>> GetModelResultsAsync(Guid runId, CancellationToken ct = default);

    // Query results
    Task SaveQueryResultsAsync(IReadOnlyList<QueryResult> results, CancellationToken ct = default);
    Task<IReadOnlyList<QueryResult>> GetQueryResultsAsync(Guid modelResultId, int? take = null, bool worstFirst = false, CancellationToken ct = default);

    // Datasets
    Task<Dataset> SaveDatasetAsync(Dataset dataset, CancellationToken ct = default);
    Task<Dataset?> GetDatasetAsync(Guid datasetId, CancellationToken ct = default);
    Task<IReadOnlyList<Dataset>> ListDatasetsAsync(CancellationToken ct = default);
    Task DeleteDatasetAsync(Guid datasetId, CancellationToken ct = default);

    // Training runs
    Task<TrainingRun> SaveTrainingRunAsync(TrainingRun run, CancellationToken ct = default);
    Task<TrainingRun?> GetTrainingRunAsync(Guid runId, CancellationToken ct = default);
    Task<IReadOnlyList<TrainingRun>> ListTrainingRunsAsync(Guid? modelId = null, CancellationToken ct = default);
    Task SaveStepMetricAsync(StepMetric metric, CancellationToken ct = default);
    Task<IReadOnlyList<StepMetric>> GetStepMetricsAsync(Guid trainingRunId, CancellationToken ct = default);

    // Agent sessions
    Task<AgentSession> SaveSessionAsync(AgentSession session, CancellationToken ct = default);
    Task<AgentSession?> GetSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentSession>> ListSessionsAsync(CancellationToken ct = default);
    Task SaveAgentMessageAsync(AgentMessage message, CancellationToken ct = default);
    Task SaveAgentActionAsync(AgentAction action, CancellationToken ct = default);
}
