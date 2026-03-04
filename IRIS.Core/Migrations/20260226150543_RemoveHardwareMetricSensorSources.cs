using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHardwareMetricSensorSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CpuTemperatureSource",
                table: "HardwareMetrics");

            migrationBuilder.DropColumn(
                name: "GpuTemperatureSource",
                table: "HardwareMetrics");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CpuTemperatureSource",
                table: "HardwareMetrics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GpuTemperatureSource",
                table: "HardwareMetrics",
                type: "text",
                nullable: true);
        }
    }
}
