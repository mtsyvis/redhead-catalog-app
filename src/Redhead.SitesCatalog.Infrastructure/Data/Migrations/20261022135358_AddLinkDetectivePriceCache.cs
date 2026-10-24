using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkDetectivePriceCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LinkDetectivePriceCaches",
                columns: table => new
                {
                    SiteDomain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    MinimumUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AverageUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MedianUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CheckedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryAfterUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkDetectivePriceCaches", x => x.SiteDomain);
                    table.ForeignKey(
                        name: "FK_LinkDetectivePriceCaches_Sites_SiteDomain",
                        column: x => x.SiteDomain,
                        principalTable: "Sites",
                        principalColumn: "Domain",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinkDetectivePriceCaches");
        }
    }
}
