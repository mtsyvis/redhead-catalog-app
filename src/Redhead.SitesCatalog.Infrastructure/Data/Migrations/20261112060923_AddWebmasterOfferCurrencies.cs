using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddWebmasterOfferCurrencies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "AppliedUsdRate",
            table: "WebmasterOfferPrices",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "RateDate",
            table: "WebmasterOfferPrices",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "SourceAmount",
            table: "WebmasterOfferPrices",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceCurrency",
            table: "WebmasterOfferPrices",
            type: "character varying(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "USD");

        migrationBuilder.CreateTable(
            name: "CurrencyReferenceRefreshStates",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastAttemptSucceeded = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CurrencyReferenceRefreshStates", x => x.Id);
                table.CheckConstraint("CK_CurrencyRefresh_Singleton", "\"Id\" = 1");
            });

        migrationBuilder.CreateTable(
            name: "CurrencyReferenceSnapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProviderAsOfUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ProviderNextUpdateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                FetchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UsdRatesJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CurrencyReferenceSnapshots", x => x.Id);
            });

        // Legacy amounts are already operational USD: copy exactly, without changing audit fields.
        migrationBuilder.Sql("""
            UPDATE "WebmasterOfferPrices"
            SET "SourceAmount" = "WebmasterPriceUsd",
                "AppliedUsdRate" = CASE WHEN "WebmasterPriceUsd" IS NOT NULL THEN 1 ELSE NULL END;
            """);

        migrationBuilder.AddCheckConstraint(
            name: "CK_WebmasterOfferPrices_SourceQuote",
            table: "WebmasterOfferPrices",
            sql: "\"SourceCurrency\" ~ '^[A-Z]{3}$' AND (\r\n    (\"WebmasterPriceUsd\" IS NULL AND \"SourceAmount\" IS NULL AND \"AppliedUsdRate\" IS NULL) OR\r\n    (\"WebmasterPriceUsd\" IS NOT NULL AND \"SourceCurrency\" = 'USD'\r\n        AND (\"SourceAmount\" IS NULL OR \"SourceAmount\" = \"WebmasterPriceUsd\")\r\n        AND (\"AppliedUsdRate\" IS NULL OR \"AppliedUsdRate\" = 1)) OR\r\n    (\"WebmasterPriceUsd\" IS NOT NULL AND \"SourceCurrency\" <> 'USD'\r\n        AND \"SourceAmount\" IS NOT NULL AND \"SourceAmount\" > 0\r\n        AND \"AppliedUsdRate\" IS NOT NULL AND \"AppliedUsdRate\" > 0\r\n        AND (\"WebmasterPriceUsd\" = CEIL(\"SourceAmount\" * \"AppliedUsdRate\")\r\n            OR \"WebmasterPriceUsd\" = FLOOR(\"SourceAmount\" * \"AppliedUsdRate\" + 0.55)))\r\n)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM "WebmasterOfferPrices" WHERE "SourceCurrency" <> 'USD') THEN
                    RAISE EXCEPTION 'Currency rollback blocked: preserve and explicitly resolve foreign payment metadata before removing this schema. USD prices must not be recalculated.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropTable(
            name: "CurrencyReferenceRefreshStates");

        migrationBuilder.DropTable(
            name: "CurrencyReferenceSnapshots");

        migrationBuilder.DropCheckConstraint(
            name: "CK_WebmasterOfferPrices_SourceQuote",
            table: "WebmasterOfferPrices");

        migrationBuilder.DropColumn(
            name: "RateDate",
            table: "WebmasterOfferPrices");

        migrationBuilder.DropColumn(
            name: "AppliedUsdRate",
            table: "WebmasterOfferPrices");

        migrationBuilder.DropColumn(
            name: "SourceAmount",
            table: "WebmasterOfferPrices");

        migrationBuilder.DropColumn(
            name: "SourceCurrency",
            table: "WebmasterOfferPrices");
    }
}
