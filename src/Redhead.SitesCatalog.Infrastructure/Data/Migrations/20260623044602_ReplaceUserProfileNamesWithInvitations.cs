using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceUserProfileNamesWithInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                table: "AspNetUsers",
                name: "DisplayName",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationExpiresAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationTokenHash",
                table: "AspNetUsers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers"
                SET "DisplayName" = LEFT(TRIM("FirstName") || ' ' || TRIM("LastName"), 100)
                WHERE NULLIF(TRIM("FirstName"), '') IS NOT NULL
                  AND NULLIF(TRIM("LastName"), '') IS NOT NULL;

                UPDATE "AspNetUsers"
                SET "ActivatedAtUtc" = NOW();
                """);

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_InvitationTokenHash",
                table: "AspNetUsers",
                column: "InvitationTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_InvitationTokenHash",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "InvitationExpiresAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "InvitationTokenHash",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                table: "AspNetUsers",
                name: "FirstName",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers"
                SET "FirstName" = "DisplayName"
                WHERE NULLIF(TRIM("DisplayName"), '') IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "AspNetUsers");
        }
    }
}
