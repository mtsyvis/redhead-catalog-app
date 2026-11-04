using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkDetectiveBackgroundSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LinkDetectiveSyncRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SelectedCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedCount = table.Column<int>(type: "integer", nullable: false),
                    PricesFoundCount = table.Column<int>(type: "integer", nullable: false),
                    NotFoundCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    ReusedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WaitingReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkDetectiveSyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinkDetectiveSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    NextSelectionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QuotaLimit = table.Column<long>(type: "bigint", nullable: true),
                    QuotaUsed = table.Column<long>(type: "bigint", nullable: true),
                    QuotaRemaining = table.Column<long>(type: "bigint", nullable: true),
                    QuotaPeriod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    QuotaResetAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QuotaObservedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkDetectiveSyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinkDetectiveSyncRunItems",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkDetectiveSyncRunItems", x => new { x.RunId, x.Domain });
                    table.ForeignKey(
                        name: "FK_LinkDetectiveSyncRunItems_LinkDetectiveSyncRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "LinkDetectiveSyncRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LinkDetectiveSyncRunItems_RunId_Status_NextAttemptAtUtc",
                table: "LinkDetectiveSyncRunItems",
                columns: new[] { "RunId", "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LinkDetectiveSyncRuns_StartedAtUtc",
                table: "LinkDetectiveSyncRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LinkDetectiveSyncRuns_Status",
                table: "LinkDetectiveSyncRuns",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinkDetectiveSyncRunItems");

            migrationBuilder.DropTable(
                name: "LinkDetectiveSyncStates");

            migrationBuilder.DropTable(
                name: "LinkDetectiveSyncRuns");
        }
    }
}
