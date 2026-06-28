using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;
using System.Text.Json;

namespace ReRankEval.Infrastructure.Services;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions s_opts = new() { WriteIndented = true };

    public AppSettingsService(string appDataDir)
    {
        _filePath = Path.Combine(appDataDir, "settings.json");
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath)) return new AppSettings();
            var json = await File.ReadAllTextAsync(_filePath, ct);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
        finally { _lock.Release(); }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(settings, s_opts);
            await File.WriteAllTextAsync(_filePath, json, ct);
        }
        finally { _lock.Release(); }
    }
}
