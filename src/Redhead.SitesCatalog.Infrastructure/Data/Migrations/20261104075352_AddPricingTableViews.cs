using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingTableViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserTablePreferences_TableKey",
                table: "UserTablePreferences");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserTableCustomViews_TableKey",
                table: "UserTableCustomViews");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserTablePreferences_TableKey",
                table: "UserTablePreferences",
                sql: "\"TableKey\" IN ('sites', 'pricing')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserTableCustomViews_TableKey",
                table: "UserTableCustomViews",
                sql: "\"TableKey\" IN ('sites', 'pricing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Pricing preferences cannot satisfy the old Sites-only constraints.
            // Rollback removes only these new preferences, preserving existing Sites views.
            migrationBuilder.Sql("""
                DELETE FROM "UserTablePreferences" WHERE "TableKey" = 'pricing';
                DELETE FROM "UserTableCustomViews" WHERE "TableKey" = 'pricing';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserTablePreferences_TableKey",
                table: "UserTablePreferences");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserTableCustomViews_TableKey",
                table: "UserTableCustomViews");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserTablePreferences_TableKey",
                table: "UserTablePreferences",
                sql: "\"TableKey\" = 'sites'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserTableCustomViews_TableKey",
                table: "UserTableCustomViews",
                sql: "\"TableKey\" = 'sites'");
        }
    }
}
