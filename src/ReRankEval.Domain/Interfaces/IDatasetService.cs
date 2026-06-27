using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public interface IDatasetService
{
    Task<Dataset> LoadJsonlAsync(string filePath, string name, CancellationToken ct = default);
    Task<Dataset> LoadCsvAsync(string filePath, string name, CancellationToken ct = default);
    Task<ValidationReport> ValidateAsync(Guid datasetId, CancellationToken ct = default);
    Task<DatasetStats> GetStatsAsync(Guid datasetId, CancellationToken ct = default);
    Task<IReadOnlyList<DatasetEntry>> GetEntriesAsync(Guid datasetId, CancellationToken ct = default);
    Task<IReadOnlyList<Dataset>> ListAsync(CancellationToken ct = default);
    Task DeleteAsync(Guid datasetId, CancellationToken ct = default);
}
