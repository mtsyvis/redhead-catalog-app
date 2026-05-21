using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionalServiceUnknownPriceStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceCasino_StatusConsistency",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceCrypto_StatusConsistency",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceDating_StatusConsistency",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceLinkInsert_StatusConsistency",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceLinkInsertCasino_StatusConsistency",
                table: "Sites");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceCasino_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceCasinoStatus\" = 1 AND \"PriceCasino\" IS NOT NULL AND \"PriceCasino\" > 0) OR (\"PriceCasinoStatus\" IN (0, 2, 3) AND \"PriceCasino\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceCrypto_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceCryptoStatus\" = 1 AND \"PriceCrypto\" IS NOT NULL AND \"PriceCrypto\" > 0) OR (\"PriceCryptoStatus\" IN (0, 2, 3) AND \"PriceCrypto\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceDating_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceDatingStatus\" = 1 AND \"PriceDating\" IS NOT NULL AND \"PriceDating\" > 0) OR (\"PriceDatingStatus\" IN (0, 2, 3) AND \"PriceDating\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceLinkInsert_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceLinkInsertStatus\" = 1 AND \"PriceLinkInsert\" IS NOT NULL AND \"PriceLinkInsert\" > 0) OR (\"PriceLinkInsertStatus\" IN (0, 2, 3) AND \"PriceLinkInsert\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceLinkInsertCasino_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceLinkInsertCasinoStatus\" = 1 AND \"PriceLinkInsertCasino\" IS NOT NULL AND \"PriceLinkInsertCasino\" > 0) OR (\"PriceLinkInsertCasinoStatus\" IN (0, 2, 3) AND \"PriceLinkInsertCasino\" IS NULL)");
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
                name: "CK_Sites_PriceDating_StatusConsistency",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceLinkInsert_StatusConsistency",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceLinkInsertCasino_StatusConsistency",
                table: "Sites");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceCasino_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceCasinoStatus\" = 1 AND \"PriceCasino\" IS NOT NULL AND \"PriceCasino\" >= 0) OR (\"PriceCasinoStatus\" IN (0, 2) AND \"PriceCasino\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceCrypto_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceCryptoStatus\" = 1 AND \"PriceCrypto\" IS NOT NULL AND \"PriceCrypto\" >= 0) OR (\"PriceCryptoStatus\" IN (0, 2) AND \"PriceCrypto\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceDating_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceDatingStatus\" = 1 AND \"PriceDating\" IS NOT NULL AND \"PriceDating\" >= 0) OR (\"PriceDatingStatus\" IN (0, 2) AND \"PriceDating\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceLinkInsert_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceLinkInsertStatus\" = 1 AND \"PriceLinkInsert\" IS NOT NULL AND \"PriceLinkInsert\" >= 0) OR (\"PriceLinkInsertStatus\" IN (0, 2) AND \"PriceLinkInsert\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceLinkInsertCasino_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceLinkInsertCasinoStatus\" = 1 AND \"PriceLinkInsertCasino\" IS NOT NULL AND \"PriceLinkInsertCasino\" >= 0) OR (\"PriceLinkInsertCasinoStatus\" IN (0, 2) AND \"PriceLinkInsertCasino\" IS NULL)");
        }
    }
}
