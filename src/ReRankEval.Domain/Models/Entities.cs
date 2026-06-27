using System.Text.Json.Serialization;

namespace ReRankEval.Domain.Models;

// ── Model management ─────────────────────────────────────────────────

public record ModelEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string HuggingFaceId { get; init; }
    public required string Revision { get; init; }
    public required string LocalPath { get; init; }
    public ModelArchitecture Architecture { get; init; }
    public string? OnnxPath { get; set; }
    public int MaxSequenceLength { get; init; } = 512;
    public long WeightsSizeBytes { get; init; }
    public DateTime DownloadedAt { get; init; } = DateTime.UtcNow;
    public List<Checkpoint> Checkpoints { get; init; } = [];
}

public enum ModelArchitecture { CrossEncoder, BiEncoder, ColBERT, Unknown }

public record Checkpoint
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ParentModelId { get; init; }
    public required string Path { get; init; }
    public required Guid TrainingRunId { get; init; }
    public float ValNdcgAt10 { get; init; }
    public int TrainingStep { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

// ── Dataset ──────────────────────────────────────────────────────────

public record Dataset
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public DatasetFormat Format { get; init; }
    public required string LocalPath { get; init; }
    public int QueryCount { get; init; }
    public float AvgDocsPerQuery { get; init; }
    public float AvgRelevantDocsPerQuery { get; init; }
    public DatasetSplit? Split { get; init; }
    public string? SourceBeirName { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public enum DatasetFormat { Jsonl, Csv, Beir }
public enum DatasetSplit { Full, Train, Validation, Test }

// ── Evaluation ───────────────────────────────────────────────────────

public record EvaluationRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required Guid DatasetId { get; init; }
    public required IReadOnlyList<Guid> ModelIds { get; init; }
    public required EvaluationConfig Config { get; init; }
    public RunStatus Status { get; set; } = RunStatus.Pending;
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ModelEvalResult> ModelResults { get; init; } = [];
}

public record EvaluationConfig
{
    public required IReadOnlyList<int> KValues { get; init; }
    public int BatchSize { get; init; } = 8;
    public ExecutionProvider Provider { get; init; } = ExecutionProvider.Cpu;
}

public enum ExecutionProvider { Cpu, Cuda, CoreMl, DirectMl }
public enum RunStatus { Pending, Running, Completed, Cancelled, Failed }

public record ModelEvalResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid RunId { get; init; }
    public required Guid ModelId { get; init; }
    public required Dictionary<int, double> NdcgAt { get; init; }
    public required Dictionary<int, double> MrrAt { get; init; }
    public double MapScore { get; init; }
    public required Dictionary<int, double> PrecisionAt { get; init; }
    public required Dictionary<int, double> RecallAt { get; init; }
    public double LatencyMeanMs { get; init; }
    public double LatencyP50Ms { get; init; }
    public double LatencyP90Ms { get; init; }
    public double LatencyP99Ms { get; init; }
    public double TokenizationMeanMs { get; init; }
    public double InferenceMeanMs { get; init; }
    public List<QueryResult> QueryResults { get; init; } = [];
}

public record QueryResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ModelResultId { get; init; }
    public required string QueryText { get; init; }
    public required IReadOnlyList<string> RankedDocIds { get; init; }
    public required IReadOnlyList<float> Scores { get; init; }
    public required IReadOnlyList<int> RelevanceLabels { get; init; }
    public double NdcgAt10 { get; init; }
    public double MrrAt10 { get; init; }
    public double LatencyMs { get; init; }
    public string? DomainTag { get; init; }
}

// ── Training ─────────────────────────────────────────────────────────

public record TrainingRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid BaseModelId { get; init; }
    public required Guid DatasetId { get; init; }
    public required TrainingConfig Config { get; init; }
    public RunStatus Status { get; set; } = RunStatus.Pending;
    public float? BestValNdcgAt10 { get; set; }
    public Guid? BestCheckpointId { get; set; }
    public int CurrentEpoch { get; set; }
    public int CurrentStep { get; set; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<StepMetric> StepMetrics { get; init; } = [];
}

public record TrainingConfig
{
    public float LearningRate { get; init; } = 2e-5f;
    public int Epochs { get; init; } = 3;
    public int BatchSize { get; init; } = 16;
    public int WarmupSteps { get; init; } = 100;
    public int FrozenLayers { get; init; } = -2;
    public LossFunction LossFunction { get; init; } = LossFunction.MarginRanking;
    public float MarginRankingMargin { get; init; } = 1.0f;
    public int CheckpointEverySteps { get; init; } = 500;
    public int EvalEverySteps { get; init; } = 500;
    public float WeightDecay { get; init; } = 0.01f;
}

public enum LossFunction { MarginRanking, BinaryCrossEntropy, CrossEntropy }

public record StepMetric
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid TrainingRunId { get; init; }
    public int Step { get; init; }
    public int Epoch { get; init; }
    public float TrainLoss { get; init; }
    public float? ValNdcgAt10 { get; init; }
    public float LearningRate { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

// ── Agent ────────────────────────────────────────────────────────────

public record AgentSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "New session";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public List<AgentMessage> Messages { get; init; } = [];
    public List<AgentAction> ExecutedActions { get; init; } = [];
}

public record AgentMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid SessionId { get; init; }
    public required AgentRole Role { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public enum AgentRole { User, Assistant, Tool }

public record AgentAction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid SessionId { get; init; }
    public required string ToolName { get; init; }
    public required string ParametersJson { get; init; }
    public string? ResultJson { get; set; }
    public ActionStatus Status { get; set; } = ActionStatus.Pending;
    public string? ErrorMessage { get; set; }
    public TimeSpan? Duration { get; set; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public enum ActionStatus { Pending, Running, Succeeded, Failed }

// ── Progress/event types ─────────────────────────────────────────────

public record DownloadProgress(string ModelId, string FileName, long BytesReceived, long TotalBytes)
{
    public double Percentage => TotalBytes > 0 ? (double)BytesReceived / TotalBytes * 100 : 0;
}

public record EvaluationProgress(int QueriesCompleted, int TotalQueries, string CurrentModelId, string Phase);

public record TrainingMetrics(int Step, int Epoch, float TrainLoss, float? ValNdcgAt10, float LearningRate);

public record DatasetEntry(string Query, IReadOnlyList<string> Documents, IReadOnlyList<int> Labels, string? DomainTag = null);

public record DatasetStats(int QueryCount, float AvgDocsPerQuery, float AvgRelevantDocsPerQuery, Dictionary<int, int> RelevanceDistribution);

public record TokenizerOutput(long[] InputIds, long[] AttentionMask, long[]? TokenTypeIds = null);

public record ValidationError(int RowIndex, string Field, string Message);

public record ValidationReport(bool IsValid, int TotalRows, int InvalidRows, IReadOnlyList<ValidationError> Errors);
