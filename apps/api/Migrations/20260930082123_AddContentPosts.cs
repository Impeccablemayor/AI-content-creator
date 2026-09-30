using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddContentPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ContentIdeas_WorkspaceId_Id",
                table: "ContentIdeas",
                columns: new[] { "WorkspaceId", "Id" });

            migrationBuilder.CreateTable(
                name: "ContentPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentIdeaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    Hook = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CallToAction = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Hashtags = table.Column<List<string>>(type: "text[]", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StorytellingStructure = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentPosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentPosts_ContentIdeas_WorkspaceId_ContentIdeaId",
                        columns: x => new { x.WorkspaceId, x.ContentIdeaId },
                        principalTable: "ContentIdeas",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPosts_ContentIdeaId_Platform",
                table: "ContentPosts",
                columns: new[] { "ContentIdeaId", "Platform" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentPosts_WorkspaceId_ContentIdeaId",
                table: "ContentPosts",
                columns: new[] { "WorkspaceId", "ContentIdeaId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentPosts_WorkspaceId_Status_CreatedAt",
                table: "ContentPosts",
                columns: new[] { "WorkspaceId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentPosts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ContentIdeas_WorkspaceId_Id",
                table: "ContentIdeas");
        }
    }
}
