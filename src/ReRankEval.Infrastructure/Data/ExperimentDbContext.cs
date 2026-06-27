using Microsoft.EntityFrameworkCore;
using ReRankEval.Domain.Models;
using System.Text.Json;

namespace ReRankEval.Infrastructure.Data;

public class ExperimentDbContext : DbContext
{
    public ExperimentDbContext(DbContextOptions<ExperimentDbContext> options) : base(options) { }

    public DbSet<ModelEntry> Models { get; set; } = null!;
    public DbSet<Checkpoint> Checkpoints { get; set; } = null!;
    public DbSet<Dataset> Datasets { get; set; } = null!;
    public DbSet<EvaluationRun> EvalRuns { get; set; } = null!;
    public DbSet<ModelEvalResult> ModelResults { get; set; } = null!;
    public DbSet<QueryResult> QueryResults { get; set; } = null!;
    public DbSet<TrainingRun> TrainingRuns { get; set; } = null!;
    public DbSet<StepMetric> StepMetrics { get; set; } = null!;
    public DbSet<AgentSession> AgentSessions { get; set; } = null!;
    public DbSet<AgentMessage> AgentMessages { get; set; } = null!;
    public DbSet<AgentAction> AgentActions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ModelEntry>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Architecture).HasConversion<string>();
            e.Ignore(m => m.Checkpoints);
        });

        b.Entity<Checkpoint>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.ParentModelId);
        });

        b.Entity<Dataset>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Format).HasConversion<string>();
            e.Property(d => d.Split).HasConversion<string>();
        });

        b.Entity<EvaluationRun>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Status).HasConversion<string>();
            e.Property(r => r.ModelIds)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => (IReadOnlyList<Guid>)JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null)!);
            e.OwnsOne(r => r.Config, cfg =>
            {
                cfg.Property(c => c.KValues)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => (IReadOnlyList<int>)JsonSerializer.Deserialize<List<int>>(v, (JsonSerializerOptions?)null)!);
                cfg.Property(c => c.Provider).HasConversion<string>();
            });
            e.Ignore(r => r.ModelResults);
        });

        b.Entity<ModelEvalResult>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => new { r.RunId, r.ModelId });

            var opts = (JsonSerializerOptions?)null;
            e.Property(r => r.NdcgAt)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, opts),
                    v => JsonSerializer.Deserialize<Dictionary<int, double>>(v, opts)!);
            e.Property(r => r.MrrAt)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, opts),
                    v => JsonSerializer.Deserialize<Dictionary<int, double>>(v, opts)!);
            e.Property(r => r.PrecisionAt)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, opts),
                    v => JsonSerializer.Deserialize<Dictionary<int, double>>(v, opts)!);
            e.Property(r => r.RecallAt)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, opts),
                    v => JsonSerializer.Deserialize<Dictionary<int, double>>(v, opts)!);
            e.Ignore(r => r.QueryResults);
        });

        b.Entity<QueryResult>(e =>
        {
            e.HasKey(q => q.Id);
            e.HasIndex(q => q.ModelResultId);

            var opts = (JsonSerializerOptions?)null;
            e.Property(q => q.RankedDocIds)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, opts),
                    v => (IReadOnlyList<string>)JsonSerializer.Deserialize<List<string>>(v, opts)!);
            e.Property(q => q.Scores)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, opts),
                    v => (IReadOnlyList<float>)JsonSerializer.Deserialize<List<float>>(v, opts)!);
            e.Property(q => q.RelevanceLabels)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, opts),
                    v => (IReadOnlyList<int>)JsonSerializer.Deserialize<List<int>>(v, opts)!);
        });

        b.Entity<TrainingRun>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Status).HasConversion<string>();
            e.OwnsOne(r => r.Config, cfg =>
            {
                cfg.Property(c => c.LossFunction).HasConversion<string>();
            });
            e.Ignore(r => r.StepMetrics);
        });

        b.Entity<StepMetric>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.TrainingRunId, s.Step });
        });

        b.Entity<AgentSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.Ignore(s => s.Messages);
            e.Ignore(s => s.ExecutedActions);
        });

        b.Entity<AgentMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.SessionId);
            e.Property(m => m.Role).HasConversion<string>();
        });

        b.Entity<AgentAction>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.SessionId);
            e.Property(a => a.Status).HasConversion<string>();
        });
    }
}
