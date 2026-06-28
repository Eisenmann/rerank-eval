using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.Agent.Plugins;

public sealed class FineTuningPlugin
{
    private readonly IFineTuningService _fineTuning;
    private readonly IExperimentStore _store;
    private readonly IModelRegistry _registry;

    public FineTuningPlugin(IFineTuningService fineTuning, IExperimentStore store, IModelRegistry registry)
    {
        _fineTuning = fineTuning;
        _store = store;
        _registry = registry;
    }

    [KernelFunction, Description("List all training runs with their status and best validation NDCG.")]
    public async Task<string> ListTrainingRunsAsync()
    {
        var runs = await _store.ListTrainingRunsAsync();
        if (!runs.Any())
            return "No training runs found. Use the Fine-Tuning page to start training.";

        var models = await _registry.ListAsync();
        var modelMap = models.ToDictionary(m => m.Id, m => m.HuggingFaceId);

        var sb = new StringBuilder();
        sb.AppendLine("| Run ID | Base Model | Status | Best NDCG@10 | Started |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var r in runs.OrderByDescending(r => r.StartedAt))
        {
            var label = modelMap.TryGetValue(r.BaseModelId, out var hfId) ? hfId : r.BaseModelId.ToString()[..8];
            var ndcg = r.BestValNdcgAt10.HasValue ? r.BestValNdcgAt10.Value.ToString("F4") : "-";
            sb.AppendLine($"| {r.Id.ToString()[..8]} | {label} | {r.Status} | {ndcg} | {r.StartedAt:yyyy-MM-dd HH:mm} |");
        }
        return sb.ToString();
    }

    [KernelFunction, Description("Get the current status and latest metrics of a training run.")]
    public async Task<string> GetTrainingStatusAsync(
        [Description("Training run ID (GUID string or 8-char prefix)")] string trainingRunId)
    {
        IReadOnlyList<TrainingRun> allRuns;
        TrainingRun? run = null;

        if (Guid.TryParse(trainingRunId, out var fullId))
        {
            run = await _store.GetTrainingRunAsync(fullId);
        }
        else
        {
            allRuns = await _store.ListTrainingRunsAsync();
            run = allRuns.FirstOrDefault(r => r.Id.ToString().StartsWith(trainingRunId, StringComparison.OrdinalIgnoreCase));
        }

        if (run == null)
            return $"Training run '{trainingRunId}' not found.";

        var metrics = await _store.GetStepMetricsAsync(run.Id);
        var latest = metrics.OrderByDescending(m => m.Step).FirstOrDefault();

        var sb = new StringBuilder();
        sb.AppendLine($"## Training Run {run.Id.ToString()[..8]}");
        sb.AppendLine($"- **Status:** {run.Status}");
        sb.AppendLine($"- **Epoch:** {run.CurrentEpoch} / {run.Config.Epochs}");
        sb.AppendLine($"- **Step:** {run.CurrentStep}");
        if (run.BestValNdcgAt10.HasValue)
            sb.AppendLine($"- **Best Val NDCG@10:** {run.BestValNdcgAt10:F4}");
        if (latest != null)
        {
            sb.AppendLine($"- **Latest train loss:** {latest.TrainLoss:F4}");
            sb.AppendLine($"- **Latest LR:** {latest.LearningRate:E2}");
        }
        if (run.Status == RunStatus.Failed && !string.IsNullOrEmpty(run.ErrorMessage))
            sb.AppendLine($"- **Error:** {run.ErrorMessage}");
        return sb.ToString();
    }
}
