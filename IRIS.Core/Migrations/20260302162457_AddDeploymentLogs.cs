using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeploymentLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PCId = table.Column<int>(type: "integer", nullable: true),
                    PCName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IPAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentLogs_PCs_PCId",
                        column: x => x.PCId,
                        principalTable: "PCs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentLogs_PCId_Timestamp",
                table: "DeploymentLogs",
                columns: new[] { "PCId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentLogs_Timestamp",
                table: "DeploymentLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeploymentLogs");
        }
    }
}
