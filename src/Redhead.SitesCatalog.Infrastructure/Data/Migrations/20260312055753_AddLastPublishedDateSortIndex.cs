using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLastPublishedDateSortIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sites_LastPublishedDate_LastPublishedDateIsMonthOnly_Domain",
                table: "Sites",
                columns: new[] { "LastPublishedDate", "LastPublishedDateIsMonthOnly", "Domain" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sites_LastPublishedDate_LastPublishedDateIsMonthOnly_Domain",
                table: "Sites");
        }
    }
}
