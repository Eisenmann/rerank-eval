namespace ReRankEval.Domain.Interfaces;

public interface ICredentialStore
{
    Task SaveAsync(string key, string secret, CancellationToken ct = default);
    Task<string?> LoadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
