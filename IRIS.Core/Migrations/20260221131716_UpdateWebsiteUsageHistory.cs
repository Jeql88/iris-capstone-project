using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class UpdateWebsiteUsageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebsiteUsageHistory_PCId_VisitedAt",
                table: "WebsiteUsageHistory");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "WebsiteUsageHistory");

            migrationBuilder.AlterColumn<string>(
                name: "Domain",
                table: "WebsiteUsageHistory",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Browser",
                table: "WebsiteUsageHistory",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WebsiteUsageHistory_PCId_Browser_Domain_VisitedAt",
                table: "WebsiteUsageHistory",
                columns: new[] { "PCId", "Browser", "Domain", "VisitedAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebsiteUsageHistory_PCId_Browser_Domain_VisitedAt",
                table: "WebsiteUsageHistory");

            migrationBuilder.DropColumn(
                name: "Browser",
                table: "WebsiteUsageHistory");

            migrationBuilder.AlterColumn<string>(
                name: "Domain",
                table: "WebsiteUsageHistory",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "WebsiteUsageHistory",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WebsiteUsageHistory_PCId_VisitedAt",
                table: "WebsiteUsageHistory",
                columns: new[] { "PCId", "VisitedAt" });
        }
    }
}
