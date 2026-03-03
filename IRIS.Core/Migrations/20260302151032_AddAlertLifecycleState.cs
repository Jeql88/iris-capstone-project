using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertLifecycleState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Alerts_PCId",
                table: "Alerts");

            migrationBuilder.AddColumn<string>(
                name: "AlertKey",
                table: "Alerts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsResolved",
                table: "Alerts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_IsResolved_CreatedAt",
                table: "Alerts",
                columns: new[] { "IsResolved", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_PCId_AlertKey_IsResolved",
                table: "Alerts",
                columns: new[] { "PCId", "AlertKey", "IsResolved" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Alerts_IsResolved_CreatedAt",
                table: "Alerts");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_PCId_AlertKey_IsResolved",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "AlertKey",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "IsResolved",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Alerts");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_PCId",
                table: "Alerts",
                column: "PCId");
        }
    }
}
