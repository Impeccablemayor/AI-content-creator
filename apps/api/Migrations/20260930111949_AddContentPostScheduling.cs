using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddContentPostScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishClaimedAt",
                table: "ContentPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAt",
                table: "ContentPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentPosts_Status_ScheduledAt",
                table: "ContentPosts",
                columns: new[] { "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContentPosts_Status_ScheduledAt",
                table: "ContentPosts");

            migrationBuilder.DropColumn(
                name: "PublishClaimedAt",
                table: "ContentPosts");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "ContentPosts");
        }
    }
}
