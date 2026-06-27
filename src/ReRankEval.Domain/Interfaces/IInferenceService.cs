using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public record InferenceRequest(string Query, IReadOnlyList<string> Documents, int BatchSize = 8);

public interface IInferenceService : IDisposable
{
    void LoadModel(ModelEntry model, ExecutionProvider provider = ExecutionProvider.Cpu);
    Task<IReadOnlyList<float>> ScoreAsync(InferenceRequest request, CancellationToken ct = default);
    Task<(IReadOnlyList<float> Scores, double TokenizationMs, double InferenceMs)> ScoreWithTimingAsync(
        InferenceRequest request, CancellationToken ct = default);
}
