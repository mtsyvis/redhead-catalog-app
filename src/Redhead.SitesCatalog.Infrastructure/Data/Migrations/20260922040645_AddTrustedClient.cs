using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrustedClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTrustedClient",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers"
                SET "IsTrustedClient" = TRUE,
                    "ClientSelectionLimitOverride" = NULL
                WHERE "ClientSelectionLimitOverride" > 100;

                UPDATE "AspNetUsers"
                SET "ClientSelectionLimitOverride" = 1
                WHERE "ClientSelectionLimitOverride" < 1;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_ClientSelectionLimitOverride_ValidRange",
                table: "AspNetUsers",
                sql: "\"ClientSelectionLimitOverride\" IS NULL OR (\"ClientSelectionLimitOverride\" BETWEEN 1 AND 100)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_ClientSelectionLimitOverride_ValidRange",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsTrustedClient",
                table: "AspNetUsers");
        }
    }
}
