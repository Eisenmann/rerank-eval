using ReRankEval.Domain.Interfaces;
using System.Text.Json;

namespace ReRankEval.Infrastructure.Services;

/// <summary>
/// File-based credential store at ~/.rerank_eval/credentials.json.
/// Credentials are stored as plaintext JSON; rely on OS file-system permissions for protection.
/// </summary>
public sealed class FileCredentialStore : ICredentialStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileCredentialStore(string appDataDir)
    {
        _filePath = Path.Combine(appDataDir, "credentials.json");
    }

    public async Task SaveAsync(string key, string secret, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadStoreAsync();
            store[key] = secret;
            await WriteStoreAsync(store);
        }
        finally { _lock.Release(); }
    }

    public async Task<string?> LoadAsync(string key, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadStoreAsync();
            return store.TryGetValue(key, out var value) ? value : null;
        }
        finally { _lock.Release(); }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var store = await LoadStoreAsync();
            if (store.Remove(key))
                await WriteStoreAsync(store);
        }
        finally { _lock.Release(); }
    }

    private async Task<Dictionary<string, string>> LoadStoreAsync()
    {
        if (!File.Exists(_filePath)) return [];
        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch { return []; }
    }

    private async Task WriteStoreAsync(Dictionary<string, string> store)
    {
        var json = JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }
}
