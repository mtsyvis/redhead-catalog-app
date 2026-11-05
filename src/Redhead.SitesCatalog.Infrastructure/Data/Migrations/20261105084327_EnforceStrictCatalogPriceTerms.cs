using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceStrictCatalogPriceTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SitePriceOptions_Term_Consistency",
                table: "SitePriceOptions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SitePriceOptions_Term_Consistency",
                table: "SitePriceOptions",
                sql: "((\"TermKey\" = 'unknown' AND \"TermType\" IS NULL AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermKey\" = 'permanent' AND \"TermType\" = 1 AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermType\" = 2 AND \"TermValue\" IS NOT NULL AND \"TermValue\" > 0 AND \"TermUnit\" = 1 AND \"TermKey\" = ('finite:' || \"TermValue\"::text || ':year'))) IS TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SitePriceOptions_Term_Consistency",
                table: "SitePriceOptions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SitePriceOptions_Term_Consistency",
                table: "SitePriceOptions",
                sql: "(\"TermKey\" = 'unknown' AND \"TermType\" IS NULL AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermKey\" = 'permanent' AND \"TermType\" = 1 AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermType\" = 2 AND \"TermValue\" IS NOT NULL AND \"TermValue\" > 0 AND \"TermUnit\" = 1 AND \"TermKey\" = ('finite:' || \"TermValue\"::text || ':year'))");
        }
    }
}
