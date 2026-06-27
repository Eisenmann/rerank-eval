using Microsoft.EntityFrameworkCore;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;
using ReRankEval.Infrastructure.Data;

namespace ReRankEval.Infrastructure.Services;

public sealed class SqliteExperimentStore : IExperimentStore
{
    private readonly IDbContextFactory<ExperimentDbContext> _factory;

    public SqliteExperimentStore(IDbContextFactory<ExperimentDbContext> factory)
    {
        _factory = factory;
    }

    // ── EvaluationRun ────────────────────────────────────────────────

    public async Task<EvaluationRun> SaveRunAsync(EvaluationRun run, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.EvalRuns.Add(run);
        await ctx.SaveChangesAsync(ct);
        return run;
    }

    public async Task<EvaluationRun?> GetRunAsync(Guid runId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.EvalRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
    }

    public async Task<IReadOnlyList<EvaluationRun>> ListRunsAsync(Guid? modelId = null, Guid? datasetId = null, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var query = ctx.EvalRuns.AsQueryable();
        if (datasetId.HasValue)
            query = query.Where(r => r.DatasetId == datasetId.Value);
        return await query.OrderByDescending(r => r.StartedAt).ToListAsync(ct);
    }

    public async Task UpdateRunStatusAsync(Guid runId, RunStatus status, string? errorMessage = null, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var run = await ctx.EvalRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        run.Status = status;
        run.ErrorMessage = errorMessage;
        if (status is RunStatus.Completed or RunStatus.Cancelled or RunStatus.Failed)
            run.CompletedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync(ct);
    }

    // ── ModelEvalResult ──────────────────────────────────────────────

    public async Task SaveModelResultAsync(ModelEvalResult result, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.ModelResults.Add(result);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ModelEvalResult>> GetModelResultsAsync(Guid runId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.ModelResults.Where(r => r.RunId == runId).ToListAsync(ct);
    }

    // ── QueryResult ──────────────────────────────────────────────────

    public async Task SaveQueryResultsAsync(IReadOnlyList<QueryResult> results, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.QueryResults.AddRange(results);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<QueryResult>> GetQueryResultsAsync(Guid modelResultId, int? take = null, bool worstFirst = false, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var query = ctx.QueryResults.Where(q => q.ModelResultId == modelResultId);
        query = worstFirst
            ? query.OrderBy(q => q.NdcgAt10)
            : query.OrderByDescending(q => q.NdcgAt10);
        if (take.HasValue)
            query = query.Take(take.Value);
        return await query.ToListAsync(ct);
    }

    // ── Dataset ──────────────────────────────────────────────────────

    public async Task<Dataset> SaveDatasetAsync(Dataset dataset, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.Datasets.Add(dataset);
        await ctx.SaveChangesAsync(ct);
        return dataset;
    }

    public async Task<Dataset?> GetDatasetAsync(Guid datasetId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.Datasets.FirstOrDefaultAsync(d => d.Id == datasetId, ct);
    }

    public async Task<IReadOnlyList<Dataset>> ListDatasetsAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.Datasets.OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
    }

    public async Task DeleteDatasetAsync(Guid datasetId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        await ctx.Datasets.Where(d => d.Id == datasetId).ExecuteDeleteAsync(ct);
    }

    // ── TrainingRun ──────────────────────────────────────────────────

    public async Task<TrainingRun> SaveTrainingRunAsync(TrainingRun run, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.TrainingRuns.Add(run);
        await ctx.SaveChangesAsync(ct);
        return run;
    }

    public async Task<TrainingRun?> GetTrainingRunAsync(Guid runId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.TrainingRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
    }

    public async Task<IReadOnlyList<TrainingRun>> ListTrainingRunsAsync(Guid? modelId = null, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var query = ctx.TrainingRuns.AsQueryable();
        if (modelId.HasValue)
            query = query.Where(r => r.BaseModelId == modelId.Value);
        return await query.OrderByDescending(r => r.StartedAt).ToListAsync(ct);
    }

    public async Task SaveStepMetricAsync(StepMetric metric, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.StepMetrics.Add(metric);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<StepMetric>> GetStepMetricsAsync(Guid trainingRunId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.StepMetrics
            .Where(s => s.TrainingRunId == trainingRunId)
            .OrderBy(s => s.Step)
            .ToListAsync(ct);
    }

    // ── AgentSession ─────────────────────────────────────────────────

    public async Task<AgentSession> SaveSessionAsync(AgentSession session, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.AgentSessions.Add(session);
        await ctx.SaveChangesAsync(ct);
        return session;
    }

    public async Task<AgentSession?> GetSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
    }

    public async Task<IReadOnlyList<AgentSession>> ListSessionsAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.AgentSessions.OrderByDescending(s => s.LastActiveAt).ToListAsync(ct);
    }

    public async Task SaveAgentMessageAsync(AgentMessage message, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.AgentMessages.Add(message);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task SaveAgentActionAsync(AgentAction action, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.AgentActions.Add(action);
        await ctx.SaveChangesAsync(ct);
    }
}
