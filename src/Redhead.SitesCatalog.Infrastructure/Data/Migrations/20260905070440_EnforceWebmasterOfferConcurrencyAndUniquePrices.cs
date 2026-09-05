using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceWebmasterOfferConcurrencyAndUniquePrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "WebmasterOfferPrices"
                        GROUP BY "SiteWebmasterOfferId", "PriceType"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Duplicate webmaster offer price types exist. No prices were removed.'
                            USING HINT = 'Review duplicate prices before retrying this migration; see docs/deployment.md.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_WebmasterOfferPrices_SiteWebmasterOfferId_PriceType",
                table: "WebmasterOfferPrices");

            migrationBuilder.CreateIndex(
                name: "IX_WebmasterOfferPrices_SiteWebmasterOfferId_PriceType",
                table: "WebmasterOfferPrices",
                columns: new[] { "SiteWebmasterOfferId", "PriceType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebmasterOfferPrices_SiteWebmasterOfferId_PriceType",
                table: "WebmasterOfferPrices");

            migrationBuilder.CreateIndex(
                name: "IX_WebmasterOfferPrices_SiteWebmasterOfferId_PriceType",
                table: "WebmasterOfferPrices",
                columns: new[] { "SiteWebmasterOfferId", "PriceType" });
        }
    }
}
