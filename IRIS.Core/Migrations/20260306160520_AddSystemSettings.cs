using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Key);
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Key", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { "DataRetention.CleanupHourUtc", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "2" },
                    { "DataRetention.HardwareMetricDays", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "30" },
                    { "DataRetention.NetworkMetricDays", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "30" },
                    { "DataRetention.ResolvedAlertDays", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "90" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}
