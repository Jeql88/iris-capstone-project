using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyThresholdProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CpuTemperatureCriticalThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "CpuTemperatureWarningThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "CpuUsageCriticalThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "CpuUsageWarningThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DiskUsageCriticalThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DiskUsageWarningThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "GpuTemperatureCriticalThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "GpuTemperatureWarningThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LatencyCriticalThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LatencyWarningThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PacketLossCriticalThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PacketLossWarningThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RamUsageCriticalThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RamUsageWarningThreshold",
                table: "Policies",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CpuTemperatureCriticalThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "CpuTemperatureWarningThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "CpuUsageCriticalThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "CpuUsageWarningThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "DiskUsageCriticalThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "DiskUsageWarningThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "GpuTemperatureCriticalThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "GpuTemperatureWarningThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "LatencyCriticalThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "LatencyWarningThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "PacketLossCriticalThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "PacketLossWarningThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "RamUsageCriticalThreshold",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "RamUsageWarningThreshold",
                table: "Policies");
        }
    }
}
