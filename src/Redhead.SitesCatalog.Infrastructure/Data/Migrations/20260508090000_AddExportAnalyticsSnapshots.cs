using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExportAnalyticsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExportAnalyticsSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotVersion = table.Column<int>(type: "integer", nullable: false),
                    FiltersSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SortSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SearchSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportAnalyticsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportAnalyticsSnapshots_ExportLogs_ExportLogId",
                        column: x => x.ExportLogId,
                        principalTable: "ExportLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportAnalyticsSnapshots_ExportLogId",
                table: "ExportAnalyticsSnapshots",
                column: "ExportLogId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportAnalyticsSnapshots");
        }
    }
}
