using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleDriveConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoogleDriveConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    GoogleSubjectId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    GoogleEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RefreshTokenEncrypted = table.Column<string>(type: "text", nullable: false),
                    GrantedScopes = table.Column<string>(type: "text", nullable: false),
                    ExportFolderId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExportFolderName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleDriveConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoogleDriveConnections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleDriveConnections_GoogleEmail",
                table: "GoogleDriveConnections",
                column: "GoogleEmail");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleDriveConnections_UserId",
                table: "GoogleDriveConnections",
                column: "UserId",
                unique: true,
                filter: "\"RevokedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoogleDriveConnections");
        }
    }
}
