using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public interface IModelRegistry
{
    Task<ModelEntry> RegisterAsync(ModelEntry model, CancellationToken ct = default);
    Task UnregisterAsync(Guid modelId, CancellationToken ct = default);
    Task<IReadOnlyList<ModelEntry>> ListAsync(CancellationToken ct = default);
    Task<ModelEntry?> GetAsync(Guid modelId, CancellationToken ct = default);
    Task<ModelEntry?> GetByHuggingFaceIdAsync(string huggingFaceId, CancellationToken ct = default);
    Task SetOnnxPathAsync(Guid modelId, string onnxPath, CancellationToken ct = default);
    Task AddCheckpointAsync(Guid modelId, Checkpoint checkpoint, CancellationToken ct = default);
}
