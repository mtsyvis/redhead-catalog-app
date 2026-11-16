using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBulkOfferPriceOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BulkOfferPriceOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Actor = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrimaryEmail = table.Column<string>(type: "text", nullable: false),
                    UpdatedCount = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkOfferPriceOperations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BulkOfferPriceOperations_AppliedAtUtc",
                table: "BulkOfferPriceOperations",
                column: "AppliedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BulkOfferPriceOperations");
        }
    }
}
