using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddContentPostMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ContentPosts_WorkspaceId_Id",
                table: "ContentPosts",
                columns: new[] { "WorkspaceId", "Id" });

            migrationBuilder.CreateTable(
                name: "ContentPostMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentPostId = table.Column<Guid>(type: "uuid", nullable: false),
                    Impressions = table.Column<long>(type: "bigint", nullable: true),
                    Likes = table.Column<long>(type: "bigint", nullable: true),
                    Comments = table.Column<long>(type: "bigint", nullable: true),
                    Shares = table.Column<long>(type: "bigint", nullable: true),
                    Clicks = table.Column<long>(type: "bigint", nullable: true),
                    ProfileVisits = table.Column<long>(type: "bigint", nullable: true),
                    FollowersGained = table.Column<long>(type: "bigint", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentPostMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentPostMetrics_ContentPosts_WorkspaceId_ContentPostId",
                        columns: x => new { x.WorkspaceId, x.ContentPostId },
                        principalTable: "ContentPosts",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPostMetrics_ContentPostId_RecordedAt",
                table: "ContentPostMetrics",
                columns: new[] { "ContentPostId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPostMetrics_WorkspaceId_ContentPostId",
                table: "ContentPostMetrics",
                columns: new[] { "WorkspaceId", "ContentPostId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPostMetrics_WorkspaceId_RecordedAt",
                table: "ContentPostMetrics",
                columns: new[] { "WorkspaceId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentPostMetrics");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ContentPosts_WorkspaceId_Id",
                table: "ContentPosts");
        }
    }
}
