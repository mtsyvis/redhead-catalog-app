using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAhrefsMonthlySync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AhrefsLastSyncedAt",
                table: "Sites",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AhrefsSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RunKind = table.Column<int>(type: "integer", nullable: false),
                    TriggeredByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Force = table.Column<bool>(type: "boolean", nullable: false),
                    IsFullCoverage = table.Column<bool>(type: "boolean", nullable: false),
                    WasLimitedByBudget = table.Column<bool>(type: "boolean", nullable: false),
                    SnapshotMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    UsageResetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EligibleSitesCount = table.Column<int>(type: "integer", nullable: false),
                    SelectedSitesCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedSitesCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedSitesCount = table.Column<int>(type: "integer", nullable: false),
                    FailedSitesCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedSitesCount = table.Column<int>(type: "integer", nullable: false),
                    CostPerSite = table.Column<int>(type: "integer", nullable: false),
                    FullEstimatedUnits = table.Column<long>(type: "bigint", nullable: false),
                    SelectedEstimatedUnits = table.Column<long>(type: "bigint", nullable: false),
                    ActualUnits = table.Column<long>(type: "bigint", nullable: false),
                    AvailableUnitsBefore = table.Column<long>(type: "bigint", nullable: false),
                    AvailableUnitsAfter = table.Column<long>(type: "bigint", nullable: true),
                    SafetyBufferUnits = table.Column<int>(type: "integer", nullable: false),
                    StopIfRemainingUnitsBelow = table.Column<int>(type: "integer", nullable: false),
                    BatchSize = table.Column<int>(type: "integer", nullable: false),
                    MaxSitesPerRun = table.Column<int>(type: "integer", nullable: false),
                    TargetMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Protocol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VolumeMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AhrefsSyncRuns", x => x.Id);
                    table.CheckConstraint("CK_AhrefsSyncRuns_RunKind_ValidValue", "\"RunKind\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_AhrefsSyncRuns_Status_ValidValue", "\"Status\" IN (1, 2, 3, 4, 5, 6, 7, 8)");
                });

            migrationBuilder.CreateTable(
                name: "AhrefsSyncRunItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OldTraffic = table.Column<long>(type: "bigint", nullable: false),
                    NewTraffic = table.Column<long>(type: "bigint", nullable: true),
                    OldDomainRating = table.Column<double>(type: "double precision", nullable: false),
                    NewDomainRating = table.Column<double>(type: "double precision", nullable: true),
                    SnapshotMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    SnapshotSaved = table.Column<bool>(type: "boolean", nullable: false),
                    AhrefsIndex = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AhrefsSyncRunItems", x => x.Id);
                    table.CheckConstraint("CK_AhrefsSyncRunItems_Status_ValidValue", "\"Status\" IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_AhrefsSyncRunItems_AhrefsSyncRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AhrefsSyncRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteMetricSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SnapshotMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Traffic = table.Column<long>(type: "bigint", nullable: false),
                    DomainRating = table.Column<double>(type: "double precision", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AhrefsSyncRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteMetricSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteMetricSnapshots_AhrefsSyncRuns_AhrefsSyncRunId",
                        column: x => x.AhrefsSyncRunId,
                        principalTable: "AhrefsSyncRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AhrefsSyncRunItems_RunId",
                table: "AhrefsSyncRunItems",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_AhrefsSyncRunItems_RunId_Domain",
                table: "AhrefsSyncRunItems",
                columns: new[] { "RunId", "Domain" });

            migrationBuilder.CreateIndex(
                name: "IX_AhrefsSyncRuns_SnapshotMonth_Status_IsFullCoverage",
                table: "AhrefsSyncRuns",
                columns: new[] { "SnapshotMonth", "Status", "IsFullCoverage" });

            migrationBuilder.CreateIndex(
                name: "IX_AhrefsSyncRuns_StartedAt",
                table: "AhrefsSyncRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SiteMetricSnapshots_AhrefsSyncRunId",
                table: "SiteMetricSnapshots",
                column: "AhrefsSyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteMetricSnapshots_Domain_SnapshotMonth",
                table: "SiteMetricSnapshots",
                columns: new[] { "Domain", "SnapshotMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AhrefsSyncRunItems");

            migrationBuilder.DropTable(
                name: "SiteMetricSnapshots");

            migrationBuilder.DropTable(
                name: "AhrefsSyncRuns");

            migrationBuilder.DropColumn(
                name: "AhrefsLastSyncedAt",
                table: "Sites");
        }
    }
}
