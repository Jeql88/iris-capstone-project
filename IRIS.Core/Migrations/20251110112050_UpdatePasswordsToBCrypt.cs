using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePasswordsToBCrypt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "FullName", "IsActive", "LastLoginAt", "PasswordHash", "Role", "Username" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, null, "$2a$11$8EqYytf5J07NnC6me1jaAOGPnPfXqXV3Ue6qVnvqZJxqjqjqjqjqm", "SystemAdministrator", "admin" },
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, null, "$2a$11$8EqYytf5J07NnC6me1jaAOGPnPfXqXV3Ue6qVnvqZJxqjqjqjqjqm", "ITPersonnel", "itperson" },
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, null, "$2a$11$8EqYytf5J07NnC6me1jaAOGPnPfXqXV3Ue6qVnvqZJxqjqjqjqjqm", "Faculty", "faculty" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
