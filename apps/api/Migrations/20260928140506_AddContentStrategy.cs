using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddContentStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentStrategies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    ContentPillars = table.Column<string>(type: "text", nullable: false),
                    ContentObjectives = table.Column<string>(type: "text", nullable: false),
                    PreferredFormats = table.Column<string>(type: "text", nullable: false),
                    StorytellingStyle = table.Column<string>(type: "text", nullable: false),
                    HashtagStrategy = table.Column<string>(type: "text", nullable: false),
                    TopicsToPrioritize = table.Column<string>(type: "text", nullable: false),
                    TopicsToAvoid = table.Column<string>(type: "text", nullable: false),
                    PreferredPostingTimes = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentStrategies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentStrategies_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentStrategies_WorkspaceId",
                table: "ContentStrategies",
                column: "WorkspaceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentStrategies");
        }
    }
}
