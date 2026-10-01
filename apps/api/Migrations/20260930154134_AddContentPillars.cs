using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddContentPillars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContentPillarId",
                table: "ContentIdeas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContentPillars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Objective = table.Column<string>(type: "text", nullable: true),
                    TargetAudienceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentPillars", x => x.Id);
                    table.UniqueConstraint("AK_ContentPillars_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.ForeignKey(
                        name: "FK_ContentPillars_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentIdeas_WorkspaceId_ContentPillarId",
                table: "ContentIdeas",
                columns: new[] { "WorkspaceId", "ContentPillarId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPillars_WorkspaceId_IsActive",
                table: "ContentPillars",
                columns: new[] { "WorkspaceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPillars_WorkspaceId_Name",
                table: "ContentPillars",
                columns: new[] { "WorkspaceId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentIdeas_ContentPillars_WorkspaceId_ContentPillarId",
                table: "ContentIdeas",
                columns: new[] { "WorkspaceId", "ContentPillarId" },
                principalTable: "ContentPillars",
                principalColumns: new[] { "WorkspaceId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentIdeas_ContentPillars_WorkspaceId_ContentPillarId",
                table: "ContentIdeas");

            migrationBuilder.DropTable(
                name: "ContentPillars");

            migrationBuilder.DropIndex(
                name: "IX_ContentIdeas_WorkspaceId_ContentPillarId",
                table: "ContentIdeas");

            migrationBuilder.DropColumn(
                name: "ContentPillarId",
                table: "ContentIdeas");
        }
    }
}
