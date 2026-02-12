using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExportLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowsReturned = table.Column<int>(type: "integer", nullable: false),
                    FilterSummaryJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Inserted = table.Column<int>(type: "integer", nullable: false),
                    Duplicates = table.Column<int>(type: "integer", nullable: false),
                    Matched = table.Column<int>(type: "integer", nullable: false),
                    Unmatched = table.Column<int>(type: "integer", nullable: false),
                    ErrorsCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleSettings",
                columns: table => new
                {
                    RoleName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExportLimitRows = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleSettings", x => x.RoleName);
                });

            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    Domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DR = table.Column<int>(type: "integer", nullable: false),
                    Traffic = table.Column<long>(type: "bigint", nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PriceUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PriceCasino = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PriceCrypto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PriceLinkInsert = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Niche = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Categories = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsQuarantined = table.Column<bool>(type: "boolean", nullable: false),
                    QuarantineReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    QuarantineUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => x.Domain);
                });

            migrationBuilder.InsertData(
                table: "RoleSettings",
                columns: new[] { "RoleName", "ExportLimitRows" },
                values: new object[,]
                {
                    { "Admin", 1000000 },
                    { "Client", 1000 },
                    { "Editor", 10000 },
                    { "UserManager", 0 },
                    { "Viewer", 5000 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportLogs_TimestampUtc",
                table: "ExportLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ImportLogs_TimestampUtc",
                table: "ImportLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_DR",
                table: "Sites",
                column: "DR");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_IsQuarantined",
                table: "Sites",
                column: "IsQuarantined");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_Location",
                table: "Sites",
                column: "Location");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_PriceUsd",
                table: "Sites",
                column: "PriceUsd");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_Traffic",
                table: "Sites",
                column: "Traffic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportLogs");

            migrationBuilder.DropTable(
                name: "ImportLogs");

            migrationBuilder.DropTable(
                name: "RoleSettings");

            migrationBuilder.DropTable(
                name: "Sites");
        }
    }
}
