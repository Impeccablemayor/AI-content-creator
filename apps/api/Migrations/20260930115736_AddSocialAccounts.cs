using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SocialAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlatformAccountId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProfileUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AccessTokenEncrypted = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    RefreshTokenEncrypted = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Scopes = table.Column<List<string>>(type: "text[]", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialAccounts_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialAccounts_WorkspaceId_Platform_PlatformAccountId",
                table: "SocialAccounts",
                columns: new[] { "WorkspaceId", "Platform", "PlatformAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialAccounts_WorkspaceId_Status",
                table: "SocialAccounts",
                columns: new[] { "WorkspaceId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialAccounts");
        }
    }
}
