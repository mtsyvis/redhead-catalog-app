using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionalServiceAvailabilityStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "PriceCasinoStatus",
                table: "Sites",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "PriceCryptoStatus",
                table: "Sites",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "PriceLinkInsertStatus",
                table: "Sites",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.Sql("""
                UPDATE "Sites"
                SET "PriceCasinoStatus" = CASE
                    WHEN "PriceCasino" IS NOT NULL THEN 1
                    ELSE 0
                END,
                "PriceCryptoStatus" = CASE
                    WHEN "PriceCrypto" IS NOT NULL THEN 1
                    ELSE 0
                END,
                "PriceLinkInsertStatus" = CASE
                    WHEN "PriceLinkInsert" IS NOT NULL THEN 1
                    ELSE 0
                END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceCasino_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceCasinoStatus\" = 1 AND \"PriceCasino\" IS NOT NULL AND \"PriceCasino\" >= 0) OR (\"PriceCasinoStatus\" IN (0, 2) AND \"PriceCasino\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceCrypto_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceCryptoStatus\" = 1 AND \"PriceCrypto\" IS NOT NULL AND \"PriceCrypto\" >= 0) OR (\"PriceCryptoStatus\" IN (0, 2) AND \"PriceCrypto\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceLinkInsert_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceLinkInsertStatus\" = 1 AND \"PriceLinkInsert\" IS NOT NULL AND \"PriceLinkInsert\" >= 0) OR (\"PriceLinkInsertStatus\" IN (0, 2) AND \"PriceLinkInsert\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceCasino_StatusConsistency",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceCrypto_StatusConsistency",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceLinkInsert_StatusConsistency",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "PriceCasinoStatus",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "PriceCryptoStatus",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "PriceLinkInsertStatus",
                table: "Sites");
        }
    }
}
