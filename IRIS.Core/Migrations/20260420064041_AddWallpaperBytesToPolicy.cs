using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddWallpaperBytesToPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WallpaperPath",
                table: "Policies");

            migrationBuilder.AddColumn<byte[]>(
                name: "WallpaperData",
                table: "Policies",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WallpaperFileName",
                table: "Policies",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WallpaperHash",
                table: "Policies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WallpaperData",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "WallpaperFileName",
                table: "Policies");

            migrationBuilder.DropColumn(
                name: "WallpaperHash",
                table: "Policies");

            migrationBuilder.AddColumn<string>(
                name: "WallpaperPath",
                table: "Policies",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
