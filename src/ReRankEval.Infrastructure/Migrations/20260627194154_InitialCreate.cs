using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReRankEval.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToolName = table.Column<string>(type: "TEXT", nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActiveAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Checkpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentModelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    TrainingRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ValNdcgAt10 = table.Column<float>(type: "REAL", nullable: false),
                    TrainingStep = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checkpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Datasets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", nullable: false),
                    QueryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AvgDocsPerQuery = table.Column<float>(type: "REAL", nullable: false),
                    AvgRelevantDocsPerQuery = table.Column<float>(type: "REAL", nullable: false),
                    Split = table.Column<string>(type: "TEXT", nullable: true),
                    SourceBeirName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Datasets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvalRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DatasetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelIds = table.Column<string>(type: "TEXT", nullable: false),
                    Config_KValues = table.Column<string>(type: "TEXT", nullable: false),
                    Config_BatchSize = table.Column<int>(type: "INTEGER", nullable: false),
                    Config_Provider = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NdcgAt = table.Column<string>(type: "TEXT", nullable: false),
                    MrrAt = table.Column<string>(type: "TEXT", nullable: false),
                    MapScore = table.Column<double>(type: "REAL", nullable: false),
                    PrecisionAt = table.Column<string>(type: "TEXT", nullable: false),
                    RecallAt = table.Column<string>(type: "TEXT", nullable: false),
                    LatencyMeanMs = table.Column<double>(type: "REAL", nullable: false),
                    LatencyP50Ms = table.Column<double>(type: "REAL", nullable: false),
                    LatencyP90Ms = table.Column<double>(type: "REAL", nullable: false),
                    LatencyP99Ms = table.Column<double>(type: "REAL", nullable: false),
                    TokenizationMeanMs = table.Column<double>(type: "REAL", nullable: false),
                    InferenceMeanMs = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Models",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HuggingFaceId = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<string>(type: "TEXT", nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", nullable: false),
                    Architecture = table.Column<string>(type: "TEXT", nullable: false),
                    OnnxPath = table.Column<string>(type: "TEXT", nullable: true),
                    MaxSequenceLength = table.Column<int>(type: "INTEGER", nullable: false),
                    WeightsSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Models", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueryResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModelResultId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QueryText = table.Column<string>(type: "TEXT", nullable: false),
                    RankedDocIds = table.Column<string>(type: "TEXT", nullable: false),
                    Scores = table.Column<string>(type: "TEXT", nullable: false),
                    RelevanceLabels = table.Column<string>(type: "TEXT", nullable: false),
                    NdcgAt10 = table.Column<double>(type: "REAL", nullable: false),
                    MrrAt10 = table.Column<double>(type: "REAL", nullable: false),
                    LatencyMs = table.Column<double>(type: "REAL", nullable: false),
                    DomainTag = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueryResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StepMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrainingRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Step = table.Column<int>(type: "INTEGER", nullable: false),
                    Epoch = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainLoss = table.Column<float>(type: "REAL", nullable: false),
                    ValNdcgAt10 = table.Column<float>(type: "REAL", nullable: true),
                    LearningRate = table.Column<float>(type: "REAL", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BaseModelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DatasetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Config_LearningRate = table.Column<float>(type: "REAL", nullable: false),
                    Config_Epochs = table.Column<int>(type: "INTEGER", nullable: false),
                    Config_BatchSize = table.Column<int>(type: "INTEGER", nullable: false),
                    Config_WarmupSteps = table.Column<int>(type: "INTEGER", nullable: false),
                    Config_FrozenLayers = table.Column<int>(type: "INTEGER", nullable: false),
                    Config_LossFunction = table.Column<string>(type: "TEXT", nullable: false),
                    Config_MarginRankingMargin = table.Column<float>(type: "REAL", nullable: false),
                    Config_CheckpointEverySteps = table.Column<int>(type: "INTEGER", nullable: false),
                    Config_EvalEverySteps = table.Column<int>(type: "INTEGER", nullable: false),
                    Config_WeightDecay = table.Column<float>(type: "REAL", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    BestValNdcgAt10 = table.Column<float>(type: "REAL", nullable: true),
                    BestCheckpointId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CurrentEpoch = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentStep = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_SessionId",
                table: "AgentActions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentMessages_SessionId",
                table: "AgentMessages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Checkpoints_ParentModelId",
                table: "Checkpoints",
                column: "ParentModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelResults_RunId_ModelId",
                table: "ModelResults",
                columns: new[] { "RunId", "ModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_QueryResults_ModelResultId",
                table: "QueryResults",
                column: "ModelResultId");

            migrationBuilder.CreateIndex(
                name: "IX_StepMetrics_TrainingRunId_Step",
                table: "StepMetrics",
                columns: new[] { "TrainingRunId", "Step" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentActions");

            migrationBuilder.DropTable(
                name: "AgentMessages");

            migrationBuilder.DropTable(
                name: "AgentSessions");

            migrationBuilder.DropTable(
                name: "Checkpoints");

            migrationBuilder.DropTable(
                name: "Datasets");

            migrationBuilder.DropTable(
                name: "EvalRuns");

            migrationBuilder.DropTable(
                name: "ModelResults");

            migrationBuilder.DropTable(
                name: "Models");

            migrationBuilder.DropTable(
                name: "QueryResults");

            migrationBuilder.DropTable(
                name: "StepMetrics");

            migrationBuilder.DropTable(
                name: "TrainingRuns");
        }
    }
}
