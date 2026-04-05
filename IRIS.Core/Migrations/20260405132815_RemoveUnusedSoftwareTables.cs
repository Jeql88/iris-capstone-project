using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedSoftwareTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SoftwareInstalled");

            migrationBuilder.DropTable(
                name: "SoftwareRequests");

            migrationBuilder.DropTable(
                name: "Software");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Software",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DownloadUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Software", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SoftwareInstalled",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PCId = table.Column<int>(type: "integer", nullable: false),
                    SoftwareId = table.Column<int>(type: "integer", nullable: false),
                    InstalledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InstalledVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UninstalledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareInstalled", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftwareInstalled_PCs_PCId",
                        column: x => x.PCId,
                        principalTable: "PCs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SoftwareInstalled_Software_SoftwareId",
                        column: x => x.SoftwareId,
                        principalTable: "Software",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoftwareRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    SoftwareId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftwareRequests_Software_SoftwareId",
                        column: x => x.SoftwareId,
                        principalTable: "Software",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoftwareRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SoftwareRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Software_Name",
                table: "Software",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareInstalled_PCId_SoftwareId",
                table: "SoftwareInstalled",
                columns: new[] { "PCId", "SoftwareId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareInstalled_SoftwareId",
                table: "SoftwareInstalled",
                column: "SoftwareId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareRequests_ReviewedByUserId",
                table: "SoftwareRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareRequests_SoftwareId",
                table: "SoftwareRequests",
                column: "SoftwareId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareRequests_UserId",
                table: "SoftwareRequests",
                column: "UserId");
        }
    }
}
