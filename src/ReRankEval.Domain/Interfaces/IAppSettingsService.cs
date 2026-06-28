using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public interface IAppSettingsService
{
    Task<AppSettings> GetSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default);
}
