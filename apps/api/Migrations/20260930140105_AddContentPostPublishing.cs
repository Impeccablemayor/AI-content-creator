using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddContentPostPublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublishFailureReason",
                table: "ContentPosts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "ContentPosts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishFailureReason",
                table: "ContentPosts");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "ContentPosts");
        }
    }
}
