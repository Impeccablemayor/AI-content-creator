using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SignalType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Signal = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningSignals_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSignals_WorkspaceId_CreatedAt",
                table: "LearningSignals",
                columns: new[] { "WorkspaceId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningSignals");
        }
    }
}
