using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddAiRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    TotalTokens = table.Column<int>(type: "integer", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiRuns_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiRuns_WorkspaceId_CreatedAt",
                table: "AiRuns",
                columns: new[] { "WorkspaceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiRuns_WorkspaceId_RunType",
                table: "AiRuns",
                columns: new[] { "WorkspaceId", "RunType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiRuns");
        }
    }
}
