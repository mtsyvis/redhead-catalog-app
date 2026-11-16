using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebmasterOfferCurrencySurcharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WebmasterOfferPrices_SourceQuote",
                table: "WebmasterOfferPrices");

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedUsdSurcharge",
                table: "WebmasterOfferPrices",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WebmasterOfferPrices_SourceQuote",
                table: "WebmasterOfferPrices",
                sql: "\"SourceCurrency\" ~ '^[A-Z]{3}$' AND (\r\n    (\"WebmasterPriceUsd\" IS NULL AND \"SourceAmount\" IS NULL AND \"AppliedUsdRate\" IS NULL) OR\r\n    (\"WebmasterPriceUsd\" IS NOT NULL AND \"SourceCurrency\" = 'USD'\r\n        AND (\"SourceAmount\" IS NULL OR \"SourceAmount\" = \"WebmasterPriceUsd\")\r\n        AND (\"AppliedUsdRate\" IS NULL OR \"AppliedUsdRate\" = 1)) OR\r\n    (\"WebmasterPriceUsd\" IS NOT NULL AND \"SourceCurrency\" <> 'USD'\r\n        AND \"SourceAmount\" IS NOT NULL AND \"SourceAmount\" > 0\r\n        AND \"AppliedUsdRate\" IS NOT NULL AND \"AppliedUsdRate\" > 0)\r\n)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WebmasterOfferPrices_SourceQuote",
                table: "WebmasterOfferPrices");

            migrationBuilder.DropColumn(
                name: "AppliedUsdSurcharge",
                table: "WebmasterOfferPrices");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WebmasterOfferPrices_SourceQuote",
                table: "WebmasterOfferPrices",
                sql: "\"SourceCurrency\" ~ '^[A-Z]{3}$' AND (\r\n    (\"WebmasterPriceUsd\" IS NULL AND \"SourceAmount\" IS NULL AND \"AppliedUsdRate\" IS NULL) OR\r\n    (\"WebmasterPriceUsd\" IS NOT NULL AND \"SourceCurrency\" = 'USD'\r\n        AND (\"SourceAmount\" IS NULL OR \"SourceAmount\" = \"WebmasterPriceUsd\")\r\n        AND (\"AppliedUsdRate\" IS NULL OR \"AppliedUsdRate\" = 1)) OR\r\n    (\"WebmasterPriceUsd\" IS NOT NULL AND \"SourceCurrency\" <> 'USD'\r\n        AND \"SourceAmount\" IS NOT NULL AND \"SourceAmount\" > 0\r\n        AND \"AppliedUsdRate\" IS NOT NULL AND \"AppliedUsdRate\" > 0\r\n        AND (\"WebmasterPriceUsd\" = CEIL(\"SourceAmount\" * \"AppliedUsdRate\")\r\n            OR \"WebmasterPriceUsd\" = FLOOR(\"SourceAmount\" * \"AppliedUsdRate\" + 0.55)))\r\n)");
        }
    }
}
