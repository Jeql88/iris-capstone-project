using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class FixBCryptHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$e6AtSfzSfXfCHsk5yjXWIuzIGGfaXRe/Z1GnuMxYx1nfSXlVepAN.");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "$2a$11$1Unk6pMkXdwNQjxP3m96M.DggxMbjbSx57fN9TQ6YWwtObK5SFwwO");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                column: "PasswordHash",
                value: "$2a$11$TMXewyIW8gRGGutz2DDDbeVLEMp9mVyhlijNJvMXzbV5tdZwH07Si");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$8EqYytf5J07NnC6me1jaAOGPnPfXqXV3Ue6qVnvqZJxqjqjqjqjqm");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "$2a$11$8EqYytf5J07NnC6me1jaAOGPnPfXqXV3Ue6qVnvqZJxqjqjqjqjqm");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                column: "PasswordHash",
                value: "$2a$11$8EqYytf5J07NnC6me1jaAOGPnPfXqXV3Ue6qVnvqZJxqjqjqjqjqm");
        }
    }
}
