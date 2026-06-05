using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientExportUsageLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilterSummaryJson",
                table: "ExportLogs");

            migrationBuilder.AddColumn<int>(
                name: "DailyExportOperationsLimit",
                table: "RoleSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyUniqueExportedDomainsLimit",
                table: "RoleSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyExportOperationsLimit",
                table: "RoleSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyUniqueExportedDomainsLimit",
                table: "RoleSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedReason",
                table: "ExportLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyExportOperationsLimit",
                table: "ExportLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyUniqueExportedDomainsLimit",
                table: "ExportLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Destination",
                table: "ExportLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ExportLimitRows",
                table: "ExportLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExportMode",
                table: "ExportLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ExportedRowsCount",
                table: "ExportLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequestedRowsCount",
                table: "ExportLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "WasTruncated",
                table: "ExportLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyExportOperationsLimit",
                table: "ExportLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyUniqueExportedDomainsLimit",
                table: "ExportLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyExportOperationsLimitOverride",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DailyUniqueExportedDomainsLimitOverride",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyExportOperationsLimitOverride",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyUniqueExportedDomainsLimitOverride",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExportedDomainAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ExportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportedDomainAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportedDomainAccesses_ExportLogs_ExportLogId",
                        column: x => x.ExportLogId,
                        principalTable: "ExportLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "Admin",
                columns: new[] { "DailyExportOperationsLimit", "DailyUniqueExportedDomainsLimit", "WeeklyExportOperationsLimit", "WeeklyUniqueExportedDomainsLimit" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "Client",
                columns: new[] { "DailyExportOperationsLimit", "DailyUniqueExportedDomainsLimit", "WeeklyExportOperationsLimit", "WeeklyUniqueExportedDomainsLimit" },
                values: new object[] { 20, 1000, 60, 3000 });

            migrationBuilder.UpdateData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "Internal",
                columns: new[] { "DailyExportOperationsLimit", "DailyUniqueExportedDomainsLimit", "WeeklyExportOperationsLimit", "WeeklyUniqueExportedDomainsLimit" },
                values: new object[] { null, null, null, null });

            migrationBuilder.UpdateData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "SuperAdmin",
                columns: new[] { "DailyExportOperationsLimit", "DailyUniqueExportedDomainsLimit", "WeeklyExportOperationsLimit", "WeeklyUniqueExportedDomainsLimit" },
                values: new object[] { null, null, null, null });

            migrationBuilder.AddCheckConstraint(
                name: "CK_RoleSettings_DailyExportOperationsLimit_PositiveOrNull",
                table: "RoleSettings",
                sql: "\"DailyExportOperationsLimit\" IS NULL OR \"DailyExportOperationsLimit\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RoleSettings_DailyUniqueExportedDomainsLimit_PositiveOrNull",
                table: "RoleSettings",
                sql: "\"DailyUniqueExportedDomainsLimit\" IS NULL OR \"DailyUniqueExportedDomainsLimit\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RoleSettings_WeeklyExportOperationsLimit_PositiveOrNull",
                table: "RoleSettings",
                sql: "\"WeeklyExportOperationsLimit\" IS NULL OR \"WeeklyExportOperationsLimit\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RoleSettings_WeeklyUniqueExportedDomainsLimit_PositiveOrNull",
                table: "RoleSettings",
                sql: "\"WeeklyUniqueExportedDomainsLimit\" IS NULL OR \"WeeklyUniqueExportedDomainsLimit\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExportLogs_Destination",
                table: "ExportLogs",
                column: "Destination");

            migrationBuilder.CreateIndex(
                name: "IX_ExportLogs_ExportMode",
                table: "ExportLogs",
                column: "ExportMode");

            migrationBuilder.CreateIndex(
                name: "IX_ExportLogs_UserId_TimestampUtc_BlockedReason",
                table: "ExportLogs",
                columns: new[] { "UserId", "TimestampUtc", "BlockedReason" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_DailyExportOperationsLimitOverride_PositiveOrNu~",
                table: "AspNetUsers",
                sql: "\"DailyExportOperationsLimitOverride\" IS NULL OR \"DailyExportOperationsLimitOverride\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_DailyUniqueExportedDomainsLimitOverride_Positiv~",
                table: "AspNetUsers",
                sql: "\"DailyUniqueExportedDomainsLimitOverride\" IS NULL OR \"DailyUniqueExportedDomainsLimitOverride\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_WeeklyExportOperationsLimitOverride_PositiveOrN~",
                table: "AspNetUsers",
                sql: "\"WeeklyExportOperationsLimitOverride\" IS NULL OR \"WeeklyExportOperationsLimitOverride\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_WeeklyUniqueExportedDomainsLimitOverride_Positi~",
                table: "AspNetUsers",
                sql: "\"WeeklyUniqueExportedDomainsLimitOverride\" IS NULL OR \"WeeklyUniqueExportedDomainsLimitOverride\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExportedDomainAccesses_Domain",
                table: "ExportedDomainAccesses",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_ExportedDomainAccesses_ExportedAtUtc",
                table: "ExportedDomainAccesses",
                column: "ExportedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExportedDomainAccesses_ExportLogId",
                table: "ExportedDomainAccesses",
                column: "ExportLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportedDomainAccesses_UserId",
                table: "ExportedDomainAccesses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportedDomainAccesses_UserId_Domain_ExportedAtUtc",
                table: "ExportedDomainAccesses",
                columns: new[] { "UserId", "Domain", "ExportedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportedDomainAccesses_UserId_ExportedAtUtc",
                table: "ExportedDomainAccesses",
                columns: new[] { "UserId", "ExportedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportedDomainAccesses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoleSettings_DailyExportOperationsLimit_PositiveOrNull",
                table: "RoleSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoleSettings_DailyUniqueExportedDomainsLimit_PositiveOrNull",
                table: "RoleSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoleSettings_WeeklyExportOperationsLimit_PositiveOrNull",
                table: "RoleSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoleSettings_WeeklyUniqueExportedDomainsLimit_PositiveOrNull",
                table: "RoleSettings");

            migrationBuilder.DropIndex(
                name: "IX_ExportLogs_Destination",
                table: "ExportLogs");

            migrationBuilder.DropIndex(
                name: "IX_ExportLogs_ExportMode",
                table: "ExportLogs");

            migrationBuilder.DropIndex(
                name: "IX_ExportLogs_UserId_TimestampUtc_BlockedReason",
                table: "ExportLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_DailyExportOperationsLimitOverride_PositiveOrNu~",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_DailyUniqueExportedDomainsLimitOverride_Positiv~",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_WeeklyExportOperationsLimitOverride_PositiveOrN~",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_WeeklyUniqueExportedDomainsLimitOverride_Positi~",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DailyExportOperationsLimit",
                table: "RoleSettings");

            migrationBuilder.DropColumn(
                name: "DailyUniqueExportedDomainsLimit",
                table: "RoleSettings");

            migrationBuilder.DropColumn(
                name: "WeeklyExportOperationsLimit",
                table: "RoleSettings");

            migrationBuilder.DropColumn(
                name: "WeeklyUniqueExportedDomainsLimit",
                table: "RoleSettings");

            migrationBuilder.DropColumn(
                name: "BlockedReason",
                table: "ExportLogs");

            migrationBuilder.DropColumn(
                name: "DailyExportOperationsLimit",
                table: "ExportLogs");

            migrationBuilder.DropColumn(
                name: "DailyUniqueExportedDomainsLimit",
                table: "ExportLogs");

            migrationBuilder.DropColumn(
                name: "Destination",
                table: "ExportLogs");

            migrationBuilder.DropColumn(
                name: "ExportLimitRows",
                table: "ExportLogs");

            migrationBuilder.DropColumn(
                name: "ExportMode",
                table: "ExportLogs");

            migrationBuilder.DropColumn(
                name: "ExportedRowsCount",
                table: "ExportLogs");

            migrationBuilder.DropColumn(
                name: "RequestedRowsCount",
                table: "ExportLogs");

            migrationBuilder.DropColumn(
                name: "WasTruncated",
                table: "ExportLogs");

            migrationBuilder.DropColumn(
                name: "WeeklyExportOperationsLimit",
                table: "ExportLogs");

            migrationBuilder.DropColumn(
                name: "WeeklyUniqueExportedDomainsLimit",
                table: "ExportLogs");

            migrationBuilder.DropColumn(
                name: "DailyExportOperationsLimitOverride",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DailyUniqueExportedDomainsLimitOverride",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WeeklyExportOperationsLimitOverride",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WeeklyUniqueExportedDomainsLimitOverride",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "FilterSummaryJson",
                table: "ExportLogs",
                type: "jsonb",
                nullable: true);
        }
    }
}
