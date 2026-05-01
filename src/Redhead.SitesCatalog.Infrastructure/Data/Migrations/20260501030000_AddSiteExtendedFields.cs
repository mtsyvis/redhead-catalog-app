using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteExtendedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumberDFLinks",
                table: "Sites",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceDating",
                table: "Sites",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "PriceDatingStatus",
                table: "Sites",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceLinkInsertCasino",
                table: "Sites",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "PriceLinkInsertCasinoStatus",
                table: "Sites",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "TermType",
                table: "Sites",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "TermUnit",
                table: "Sites",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TermValue",
                table: "Sites",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_NumberDFLinks_PositiveOrNull",
                table: "Sites",
                sql: "\"NumberDFLinks\" IS NULL OR \"NumberDFLinks\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceDating_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceDatingStatus\" = 1 AND \"PriceDating\" IS NOT NULL AND \"PriceDating\" >= 0) OR (\"PriceDatingStatus\" IN (0, 2) AND \"PriceDating\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceLinkInsertCasino_StatusConsistency",
                table: "Sites",
                sql: "(\"PriceLinkInsertCasinoStatus\" = 1 AND \"PriceLinkInsertCasino\" IS NOT NULL AND \"PriceLinkInsertCasino\" >= 0) OR (\"PriceLinkInsertCasinoStatus\" IN (0, 2) AND \"PriceLinkInsertCasino\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_Term_Consistency",
                table: "Sites",
                sql: "(\"TermType\" IS NULL AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermType\" = 1 AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermType\" = 2 AND \"TermValue\" IS NOT NULL AND \"TermValue\" > 0 AND \"TermUnit\" = 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_NumberDFLinks_PositiveOrNull",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceDating_StatusConsistency",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceLinkInsertCasino_StatusConsistency",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_Term_Consistency",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "NumberDFLinks",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "PriceDating",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "PriceDatingStatus",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "PriceLinkInsertCasino",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "PriceLinkInsertCasinoStatus",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "TermType",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "TermUnit",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "TermValue",
                table: "Sites");
        }
    }
}
