using Microsoft.EntityFrameworkCore;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;
using ReRankEval.Infrastructure.Data;

namespace ReRankEval.Infrastructure.Services;

public sealed class LocalModelRegistry : IModelRegistry
{
    private readonly IDbContextFactory<ExperimentDbContext> _factory;

    public LocalModelRegistry(IDbContextFactory<ExperimentDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<ModelEntry> RegisterAsync(ModelEntry model, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.Models.Add(model);
        await ctx.SaveChangesAsync(ct);
        return model;
    }

    public async Task UnregisterAsync(Guid modelId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        await ctx.Models.Where(m => m.Id == modelId).ExecuteDeleteAsync(ct);
        await ctx.Checkpoints.Where(c => c.ParentModelId == modelId).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<ModelEntry>> ListAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.Models.OrderByDescending(m => m.DownloadedAt).ToListAsync(ct);
    }

    public async Task<ModelEntry?> GetAsync(Guid modelId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.Models.FirstOrDefaultAsync(m => m.Id == modelId, ct);
    }

    public async Task<ModelEntry?> GetByHuggingFaceIdAsync(string huggingFaceId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        return await ctx.Models.FirstOrDefaultAsync(m => m.HuggingFaceId == huggingFaceId, ct);
    }

    public async Task SetOnnxPathAsync(Guid modelId, string onnxPath, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var model = await ctx.Models.FirstOrDefaultAsync(m => m.Id == modelId, ct);
        if (model is null) return;
        model.OnnxPath = onnxPath;
        await ctx.SaveChangesAsync(ct);
    }

    public async Task AddCheckpointAsync(Guid modelId, Checkpoint checkpoint, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.Checkpoints.Add(checkpoint);
        await ctx.SaveChangesAsync(ct);
    }
}
