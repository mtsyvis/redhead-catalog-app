using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "RoleSettings",
                columns: new[] { "RoleName", "DailyExportOperationsLimit", "DailyUniqueExportedDomainsLimit", "ExportLimitMode", "ExportLimitRows", "WeeklyExportOperationsLimit", "WeeklyUniqueExportedDomainsLimit" },
                values: new object[] { "Editor", null, null, 1, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "Editor");
        }
    }
}
