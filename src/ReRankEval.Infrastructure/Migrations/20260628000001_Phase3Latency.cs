using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReRankEval.Infrastructure.Migrations
{
    public partial class Phase3Latency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "TensorCreationMeanMs",
                table: "ModelResults",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SessionRunMeanMs",
                table: "ModelResults",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PostprocessingMeanMs",
                table: "ModelResults",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TensorCreationMeanMs", table: "ModelResults");
            migrationBuilder.DropColumn(name: "SessionRunMeanMs",     table: "ModelResults");
            migrationBuilder.DropColumn(name: "PostprocessingMeanMs", table: "ModelResults");
        }
    }
}
