using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteExtendedSortIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sites_NumberDFLinks",
                table: "Sites",
                column: "NumberDFLinks");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_TermType_TermUnit_TermValue",
                table: "Sites",
                columns: new[] { "TermType", "TermUnit", "TermValue" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sites_NumberDFLinks",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_Sites_TermType_TermUnit_TermValue",
                table: "Sites");
        }
    }
}
