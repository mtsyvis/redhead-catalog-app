using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingDomainsAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MultiSearchAnalyticsRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SearchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MultiSearchAnalyticsRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MultiSearchAnalyticsRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MissingDomainSearches",
                columns: table => new
                {
                    SearchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissingDomainSearches", x => new { x.SearchId, x.Domain });
                    table.ForeignKey(
                        name: "FK_MissingDomainSearches_MultiSearchAnalyticsRequests_SearchId",
                        column: x => x.SearchId,
                        principalTable: "MultiSearchAnalyticsRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MissingDomainSearches_Domain",
                table: "MissingDomainSearches",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_MultiSearchAnalyticsRequests_SearchedAtUtc_Role",
                table: "MultiSearchAnalyticsRequests",
                columns: new[] { "SearchedAtUtc", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_MultiSearchAnalyticsRequests_UserId_RequestId",
                table: "MultiSearchAnalyticsRequests",
                columns: new[] { "UserId", "RequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissingDomainSearches");

            migrationBuilder.DropTable(
                name: "MultiSearchAnalyticsRequests");
        }
    }
}
