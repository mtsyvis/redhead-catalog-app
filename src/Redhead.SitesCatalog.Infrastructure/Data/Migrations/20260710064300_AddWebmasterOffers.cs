using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebmasterOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PagesCount",
                table: "Sites",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TrafficValueUsd",
                table: "Sites",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LinkbuilderMailboxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Aliases = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "ARRAY[]::text[]"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkbuilderMailboxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Webmasters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ContactRawText = table.Column<string>(type: "text", nullable: false),
                    NormalizedContactRawText = table.Column<string>(type: "text", nullable: false),
                    PrimaryEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Webmasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteWebmasterOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    WebmasterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContactRawText = table.Column<string>(type: "text", nullable: false),
                    OutreachSenderRawText = table.Column<string>(type: "text", nullable: true),
                    LinkbuilderMailboxRawText = table.Column<string>(type: "text", nullable: true),
                    LinkPolicyText = table.Column<string>(type: "text", nullable: true),
                    CommentText = table.Column<string>(type: "text", nullable: true),
                    ClientRawText = table.Column<string>(type: "text", nullable: true),
                    TermRawText = table.Column<string>(type: "text", nullable: true),
                    TermType = table.Column<short>(type: "smallint", nullable: true),
                    TermValue = table.Column<int>(type: "integer", nullable: true),
                    TermUnit = table.Column<short>(type: "smallint", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteWebmasterOffers", x => x.Id);
                    table.CheckConstraint("CK_SiteWebmasterOffers_Term_Consistency", "(\"TermType\" IS NULL AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermType\" = 2 AND \"TermValue\" IS NOT NULL AND \"TermValue\" > 0 AND \"TermUnit\" = 1)");
                    table.ForeignKey(
                        name: "FK_SiteWebmasterOffers_Sites_SiteDomain",
                        column: x => x.SiteDomain,
                        principalTable: "Sites",
                        principalColumn: "Domain",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SiteWebmasterOffers_Webmasters_WebmasterId",
                        column: x => x.WebmasterId,
                        principalTable: "Webmasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteWebmasterOfferLinkbuilderMailboxes",
                columns: table => new
                {
                    SiteWebmasterOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkbuilderMailboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteWebmasterOfferLinkbuilderMailboxes", x => new { x.SiteWebmasterOfferId, x.LinkbuilderMailboxId });
                    table.ForeignKey(
                        name: "FK_SiteWebmasterOfferLinkbuilderMailboxes_LinkbuilderMailboxes~",
                        column: x => x.LinkbuilderMailboxId,
                        principalTable: "LinkbuilderMailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteWebmasterOfferLinkbuilderMailboxes_SiteWebmasterOffers_~",
                        column: x => x.SiteWebmasterOfferId,
                        principalTable: "SiteWebmasterOffers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebmasterOfferPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteWebmasterOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceType = table.Column<short>(type: "smallint", nullable: false),
                    AvailabilityStatus = table.Column<short>(type: "smallint", nullable: false),
                    WebmasterPriceUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    WebmasterPriceDetails = table.Column<string>(type: "text", nullable: true),
                    TermType = table.Column<short>(type: "smallint", nullable: true),
                    TermValue = table.Column<int>(type: "integer", nullable: true),
                    TermUnit = table.Column<short>(type: "smallint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebmasterOfferPrices", x => x.Id);
                    table.CheckConstraint("CK_WebmasterOfferPrices_AvailabilityStatus_Consistency", "(\"AvailabilityStatus\" = 1 AND \"WebmasterPriceUsd\" IS NOT NULL AND \"WebmasterPriceUsd\" > 0) OR (\"AvailabilityStatus\" IN (0, 2, 3) AND \"WebmasterPriceUsd\" IS NULL)");
                    table.CheckConstraint("CK_WebmasterOfferPrices_Term_Consistency", "(\"TermType\" IS NULL AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermType\" = 2 AND \"TermValue\" IS NOT NULL AND \"TermValue\" > 0 AND \"TermUnit\" = 1)");
                    table.CheckConstraint("CK_WebmasterOfferPrices_WebmasterPriceUsd_PositiveOrNull", "\"WebmasterPriceUsd\" IS NULL OR \"WebmasterPriceUsd\" > 0");
                    table.ForeignKey(
                        name: "FK_WebmasterOfferPrices_SiteWebmasterOffers_SiteWebmasterOffer~",
                        column: x => x.SiteWebmasterOfferId,
                        principalTable: "SiteWebmasterOffers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "RoleSettings",
                columns: new[] { "RoleName", "DailyExportOperationsLimit", "DailyUniqueExportedDomainsLimit", "ExportLimitMode", "ExportLimitRows", "WeeklyExportOperationsLimit", "WeeklyUniqueExportedDomainsLimit" },
                values: new object[] { "Linkbuilder", null, null, 1, null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Sites_PagesCount",
                table: "Sites",
                column: "PagesCount");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_TrafficValueUsd",
                table: "Sites",
                column: "TrafficValueUsd");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_PagesCount_NonNegativeOrNull",
                table: "Sites",
                sql: "\"PagesCount\" IS NULL OR \"PagesCount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sites_TrafficValueUsd_NonNegativeOrNull",
                table: "Sites",
                sql: "\"TrafficValueUsd\" IS NULL OR \"TrafficValueUsd\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_LinkbuilderMailboxes_Aliases",
                table: "LinkbuilderMailboxes",
                column: "Aliases")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_LinkbuilderMailboxes_Email",
                table: "LinkbuilderMailboxes",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkbuilderMailboxes_IsActive",
                table: "LinkbuilderMailboxes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SiteWebmasterOfferLinkbuilderMailboxes_LinkbuilderMailboxId",
                table: "SiteWebmasterOfferLinkbuilderMailboxes",
                column: "LinkbuilderMailboxId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteWebmasterOffers_ImportFingerprint",
                table: "SiteWebmasterOffers",
                column: "ImportFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteWebmasterOffers_SiteDomain",
                table: "SiteWebmasterOffers",
                column: "SiteDomain");

            migrationBuilder.CreateIndex(
                name: "IX_SiteWebmasterOffers_SiteDomain_Status",
                table: "SiteWebmasterOffers",
                columns: new[] { "SiteDomain", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteWebmasterOffers_Status",
                table: "SiteWebmasterOffers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SiteWebmasterOffers_WebmasterId",
                table: "SiteWebmasterOffers",
                column: "WebmasterId");

            migrationBuilder.CreateIndex(
                name: "IX_WebmasterOfferPrices_PriceType",
                table: "WebmasterOfferPrices",
                column: "PriceType");

            migrationBuilder.CreateIndex(
                name: "IX_WebmasterOfferPrices_SiteWebmasterOfferId_PriceType",
                table: "WebmasterOfferPrices",
                columns: new[] { "SiteWebmasterOfferId", "PriceType" });

            migrationBuilder.CreateIndex(
                name: "IX_Webmasters_NormalizedContactRawText",
                table: "Webmasters",
                column: "NormalizedContactRawText",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Webmasters_PrimaryEmail",
                table: "Webmasters",
                column: "PrimaryEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SiteWebmasterOfferLinkbuilderMailboxes");

            migrationBuilder.DropTable(
                name: "WebmasterOfferPrices");

            migrationBuilder.DropTable(
                name: "LinkbuilderMailboxes");

            migrationBuilder.DropTable(
                name: "SiteWebmasterOffers");

            migrationBuilder.DropTable(
                name: "Webmasters");

            migrationBuilder.DropIndex(
                name: "IX_Sites_PagesCount",
                table: "Sites");

            migrationBuilder.DropIndex(
                name: "IX_Sites_TrafficValueUsd",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_PagesCount_NonNegativeOrNull",
                table: "Sites");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sites_TrafficValueUsd_NonNegativeOrNull",
                table: "Sites");

            migrationBuilder.DeleteData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "Linkbuilder");

            migrationBuilder.DropColumn(
                name: "PagesCount",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "TrafficValueUsd",
                table: "Sites");
        }
    }
}
