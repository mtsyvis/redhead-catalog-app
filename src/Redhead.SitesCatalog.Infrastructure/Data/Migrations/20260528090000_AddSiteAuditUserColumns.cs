using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Redhead.SitesCatalog.Infrastructure.Data;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260528090000_AddSiteAuditUserColumns")]
public partial class AddSiteAuditUserColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CreatedBy",
            table: "Sites",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UpdatedBy",
            table: "Sites",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CreatedBy",
            table: "Sites");

        migrationBuilder.DropColumn(
            name: "UpdatedBy",
            table: "Sites");
    }
}
