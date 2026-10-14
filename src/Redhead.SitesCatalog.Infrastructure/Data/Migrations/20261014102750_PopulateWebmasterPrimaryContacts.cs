using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PopulateWebmasterPrimaryContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Webmasters_PrimaryEmail",
                table: "Webmasters");

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryEmail",
                table: "Webmasters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Webmasters_PrimaryEmail",
                table: "Webmasters",
                column: "PrimaryEmail")
                .Annotation("Npgsql:IndexMethod", "hash");

            migrationBuilder.Sql(ReadScript("Up"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReadScript("Down"));
            migrationBuilder.DropIndex(
                name: "IX_Webmasters_PrimaryEmail",
                table: "Webmasters");

            migrationBuilder.AlterColumn<string>(
                name: "PrimaryEmail",
                table: "Webmasters",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Webmasters_PrimaryEmail",
                table: "Webmasters",
                column: "PrimaryEmail");
        }
        private static string ReadScript(string direction)
        {
            var name = $"Redhead.SitesCatalog.Infrastructure.Data.Migrations.Sql.PopulateWebmasterPrimaryContacts.{direction}.sql";
            using var stream = typeof(PopulateWebmasterPrimaryContacts).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Missing embedded migration resource: {name}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
