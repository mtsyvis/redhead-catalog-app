using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforcePositivePriceUsd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"Sites\" " +
                "SET \"PriceUsd\" = NULL " +
                "WHERE \"PriceUsd\" = 0;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PriceUsd_PositiveOrNull",
                table: "Sites",
                sql: "\"PriceUsd\" IS NULL OR \"PriceUsd\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PriceUsd_PositiveOrNull",
                table: "Sites");
        }
    }
}
