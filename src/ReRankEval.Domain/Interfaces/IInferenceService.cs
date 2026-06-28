using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public record InferenceRequest(string Query, IReadOnlyList<string> Documents, int BatchSize = 8);

public record InferenceTimingResult(
    IReadOnlyList<float> Scores,
    double TokenizationMs,
    double TensorCreationMs,
    double SessionRunMs,
    double PostprocessingMs);

public interface IInferenceService : IDisposable
{
    void LoadModel(ModelEntry model, ExecutionProvider provider = ExecutionProvider.Cpu);
    Task<IReadOnlyList<float>> ScoreAsync(InferenceRequest request, CancellationToken ct = default);
    Task<InferenceTimingResult> ScoreWithTimingAsync(InferenceRequest request, CancellationToken ct = default);
}
