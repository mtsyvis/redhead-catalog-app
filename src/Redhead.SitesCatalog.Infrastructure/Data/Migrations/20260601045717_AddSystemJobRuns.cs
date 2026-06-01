using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemJobRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemJobRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemJobRuns", x => x.Id);
                    table.CheckConstraint("CK_SystemJobRuns_Status_ValidValue", "\"Status\" IN (1, 2, 3)");
                });

            migrationBuilder.CreateTable(
                name: "SystemJobArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemJobRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ExternalFileId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemJobArtifacts", x => x.Id);
                    table.CheckConstraint("CK_SystemJobArtifacts_FileSizeBytes_NonNegative", "\"FileSizeBytes\" >= 0");
                    table.ForeignKey(
                        name: "FK_SystemJobArtifacts_SystemJobRuns_SystemJobRunId",
                        column: x => x.SystemJobRunId,
                        principalTable: "SystemJobRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemJobArtifacts_ExternalFileId",
                table: "SystemJobArtifacts",
                column: "ExternalFileId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemJobArtifacts_SystemJobRunId",
                table: "SystemJobArtifacts",
                column: "SystemJobRunId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemJobRuns_StartedAtUtc",
                table: "SystemJobRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SystemJobRuns_Status",
                table: "SystemJobRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_SystemJobRuns_JobName_PeriodKey_Succeeded",
                table: "SystemJobRuns",
                columns: new[] { "JobName", "PeriodKey" },
                unique: true,
                filter: "\"Status\" = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemJobArtifacts");

            migrationBuilder.DropTable(
                name: "SystemJobRuns");
        }
    }
}
