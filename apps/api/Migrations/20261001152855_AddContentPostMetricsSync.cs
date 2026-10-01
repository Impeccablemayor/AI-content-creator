using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddContentPostMetricsSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MetricsLastAttemptedAt",
                table: "ContentPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedExternalPostId",
                table: "ContentPosts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentPosts_Status_MetricsLastAttemptedAt",
                table: "ContentPosts",
                columns: new[] { "Status", "MetricsLastAttemptedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContentPosts_Status_MetricsLastAttemptedAt",
                table: "ContentPosts");

            migrationBuilder.DropColumn(
                name: "MetricsLastAttemptedAt",
                table: "ContentPosts");

            migrationBuilder.DropColumn(
                name: "PublishedExternalPostId",
                table: "ContentPosts");
        }
    }
}
