using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicySustainDurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CriticalSustainSeconds",
                table: "Policies",
                type: "integer",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "WarningSustainSeconds",
                table: "Policies",
                type: "integer",
                nullable: false,
                defaultValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriticalSustainSeconds",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "WarningSustainSeconds",
                table: "Policies");
        }
    }
}
