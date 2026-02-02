using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class RemovePolicyAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoShutdownEnabled",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "BlockUnauthorizedApplications",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "EnableAccessControl",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "MonitorApplicationUsage",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "MonitorWebsiteUsage",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "ScheduledShutdownTime",
                table: "Policies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoShutdownEnabled",
                table: "Policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BlockUnauthorizedApplications",
                table: "Policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableAccessControl",
                table: "Policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MonitorApplicationUsage",
                table: "Policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MonitorWebsiteUsage",
                table: "Policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ScheduledShutdownTime",
                table: "Policies",
                type: "interval",
                nullable: true);
        }
    }
}
