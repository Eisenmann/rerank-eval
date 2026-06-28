using Microsoft.Extensions.Logging;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;
using System.Diagnostics;

namespace ReRankEval.Infrastructure.Services;

public sealed class EvaluationService : IEvaluationService
{
    private readonly IModelRegistry _modelRegistry;
    private readonly IDatasetService _datasetService;
    private readonly IMetricsCalculator _metrics;
    private readonly IExperimentStore _store;
    private readonly ILogger<EvaluationService> _logger;

    private readonly Dictionary<Guid, IInferenceService> _inferenceServices = new();

    public EvaluationService(
        IModelRegistry modelRegistry,
        IDatasetService datasetService,
        IMetricsCalculator metrics,
        IExperimentStore store,
        ILogger<EvaluationService> logger)
    {
        _modelRegistry = modelRegistry;
        _datasetService = datasetService;
        _metrics = metrics;
        _store = store;
        _logger = logger;
    }

    public async Task<EvaluationRun> RunAsync(
        EvaluationRequest request,
        IProgress<EvaluationProgress> progress,
        CancellationToken ct = default)
    {
        var models = await LoadModelsAsync(request.ModelIds, ct);
        var entries = await _datasetService.GetEntriesAsync(request.DatasetId, ct);

        var run = new EvaluationRun
        {
            Name = request.Name,
            DatasetId = request.DatasetId,
            ModelIds = request.ModelIds,
            Config = new EvaluationConfig
            {
                KValues = request.KValues,
                BatchSize = request.BatchSize,
                Provider = request.Provider
            },
            Status = RunStatus.Running
        };

        await _store.SaveRunAsync(run, ct);

        try
        {
            var modelTasks = models.Select(m =>
                EvaluateModelAsync(m, entries, run.Id, request, progress, ct)).ToList();

            await Task.WhenAll(modelTasks);

            run.Status = RunStatus.Completed;
            run.CompletedAt = DateTime.UtcNow;
            await _store.UpdateRunStatusAsync(run.Id, RunStatus.Completed, ct: ct);
        }
        catch (OperationCanceledException)
        {
            run.Status = RunStatus.Cancelled;
            await _store.UpdateRunStatusAsync(run.Id, RunStatus.Cancelled, ct: ct);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evaluation run {RunId} failed", run.Id);
            run.Status = RunStatus.Failed;
            run.ErrorMessage = ex.Message;
            await _store.UpdateRunStatusAsync(run.Id, RunStatus.Failed, ex.Message, ct);
            throw;
        }

        return run;
    }

    private async Task EvaluateModelAsync(
        ModelEntry model,
        IReadOnlyList<DatasetEntry> entries,
        Guid runId,
        EvaluationRequest request,
        IProgress<EvaluationProgress> progress,
        CancellationToken ct)
    {
        _logger.LogInformation("Evaluating model {ModelId} on {QueryCount} queries", model.HuggingFaceId, entries.Count);

        if (!_inferenceServices.TryGetValue(model.Id, out var inference))
            throw new InvalidOperationException($"No inference service for model {model.HuggingFaceId}");

        inference.LoadModel(model, request.Provider);

        var queryResults = new List<QueryResult>();
        var latencies = new List<double>();
        double totalTokMs = 0, totalTensorMs = 0, totalSessionMs = 0, totalPostMs = 0;
        int queriesCompleted = 0;

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            var sw = Stopwatch.StartNew();
            var req = new InferenceRequest(entry.Query, entry.Documents, request.BatchSize);
            var timing = await inference.ScoreWithTimingAsync(req, ct);
            sw.Stop();

            totalTokMs    += timing.TokenizationMs;
            totalTensorMs += timing.TensorCreationMs;
            totalSessionMs += timing.SessionRunMs;
            totalPostMs   += timing.PostprocessingMs;
            latencies.Add(sw.Elapsed.TotalMilliseconds);

            var ranked = timing.Scores.Zip(entry.Labels)
                .OrderByDescending(x => x.First)
                .ToList();
            var rankedLabels = ranked.Select(x => x.Second).ToList();
            var rankedIds = Enumerable.Range(0, entry.Documents.Count)
                .OrderByDescending(i => timing.Scores[i])
                .Select(i => i.ToString())
                .ToList();

            queryResults.Add(new QueryResult
            {
                ModelResultId = Guid.NewGuid(),
                QueryText = entry.Query,
                RankedDocIds = rankedIds,
                Scores = timing.Scores.ToList(),
                RelevanceLabels = rankedLabels,
                NdcgAt10 = _metrics.NdcgAtK(rankedLabels, 10),
                MrrAt10 = _metrics.MrrAtK(rankedLabels, 10),
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                DomainTag = entry.DomainTag
            });

            queriesCompleted++;
            progress.Report(new EvaluationProgress(queriesCompleted, entries.Count, model.HuggingFaceId, "Scoring"));
        }

        var ndcgAt = new Dictionary<int, double>();
        var mrrAt = new Dictionary<int, double>();
        var precAt = new Dictionary<int, double>();
        var recAt = new Dictionary<int, double>();

        foreach (var k in request.KValues)
        {
            ndcgAt[k] = queryResults.Average(q => _metrics.NdcgAtK(q.RelevanceLabels, k));
            mrrAt[k]  = queryResults.Average(q => _metrics.MrrAtK(q.RelevanceLabels, k));
            precAt[k] = queryResults.Average(q => _metrics.PrecisionAtK(q.RelevanceLabels, k));
            recAt[k]  = queryResults.Average(q =>
                _metrics.RecallAtK(q.RelevanceLabels, q.RelevanceLabels.Count(l => l > 0), k));
        }

        var sortedLat = latencies.OrderBy(l => l).ToArray();
        var n = entries.Count;
        var modelResult = new ModelEvalResult
        {
            RunId = runId,
            ModelId = model.Id,
            NdcgAt = ndcgAt,
            MrrAt = mrrAt,
            MapScore = queryResults.Average(q => _metrics.MapAtK(q.RelevanceLabels, 10)),
            PrecisionAt = precAt,
            RecallAt = recAt,
            LatencyMeanMs = latencies.Average(),
            LatencyP50Ms = Percentile(sortedLat, 0.50),
            LatencyP90Ms = Percentile(sortedLat, 0.90),
            LatencyP99Ms = Percentile(sortedLat, 0.99),
            TokenizationMeanMs   = totalTokMs    / n,
            TensorCreationMeanMs = totalTensorMs / n,
            SessionRunMeanMs     = totalSessionMs / n,
            PostprocessingMeanMs = totalPostMs   / n
        };

        await _store.SaveModelResultAsync(modelResult, ct);

        var finalQueryResults = queryResults.Select(q => q with { ModelResultId = modelResult.Id }).ToList();
        await _store.SaveQueryResultsAsync(finalQueryResults, ct);

        _logger.LogInformation("Model {ModelId} NDCG@10={Ndcg:F4}", model.HuggingFaceId, ndcgAt.GetValueOrDefault(10));
    }

    private async Task<List<ModelEntry>> LoadModelsAsync(IReadOnlyList<Guid> modelIds, CancellationToken ct)
    {
        var models = new List<ModelEntry>();
        foreach (var id in modelIds)
        {
            var model = await _modelRegistry.GetAsync(id, ct)
                ?? throw new InvalidOperationException($"Model {id} not found in registry");
            models.Add(model);

            if (!_inferenceServices.ContainsKey(id))
                _inferenceServices[id] = new OnnxInferenceService(
                    new HFTokenizerService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<OnnxInferenceService>.Instance);
        }
        return models;
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        var idx = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Length - 1)];
    }
}
