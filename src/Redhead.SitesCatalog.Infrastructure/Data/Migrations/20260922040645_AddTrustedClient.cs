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
                UPDATE "ClientCatalogAlerts" AS alerts
                SET "ReviewedAtUtc" = CURRENT_TIMESTAMP,
                    "ReviewedByUserId" = NULL,
                    "NextEmailAttemptAtUtc" = NULL
                FROM "AspNetUsers" AS users
                WHERE alerts."UserId" = users."Id"
                    AND alerts."ReviewedAtUtc" IS NULL
                    AND users."ClientSelectionLimitOverride" > 100
                    AND EXISTS (
                        SELECT 1
                        FROM "AspNetUserRoles" AS user_roles
                        INNER JOIN "AspNetRoles" AS roles ON roles."Id" = user_roles."RoleId"
                        WHERE user_roles."UserId" = users."Id" AND roles."Name" = 'Client');

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
