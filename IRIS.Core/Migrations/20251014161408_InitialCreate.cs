using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IRIS.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Software",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DownloadUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Software", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PCs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MacAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SubnetMask = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DefaultGateway = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    Hostname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OperatingSystem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PCs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PCs_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    ResetWallpaperOnStartup = table.Column<bool>(type: "boolean", nullable: false),
                    EnableAccessControl = table.Column<bool>(type: "boolean", nullable: false),
                    AutoShutdownEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoShutdownIdleMinutes = table.Column<int>(type: "integer", nullable: true),
                    ScheduledShutdownTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    BlockUnauthorizedApplications = table.Column<bool>(type: "boolean", nullable: false),
                    MonitorApplicationUsage = table.Column<bool>(type: "boolean", nullable: false),
                    MonitorWebsiteUsage = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Policies_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoftwareRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    SoftwareId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PCId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_PCs_PCId",
                        column: x => x.PCId,
                        principalTable: "PCs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Alerts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HardwareMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PCId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CpuUsage = table.Column<double>(type: "double precision", nullable: true),
                    CpuTemperature = table.Column<double>(type: "double precision", nullable: true),
                    MemoryUsage = table.Column<double>(type: "double precision", nullable: true),
                    MemoryUsed = table.Column<long>(type: "bigint", nullable: true),
                    MemoryTotal = table.Column<long>(type: "bigint", nullable: true),
                    GpuUsage = table.Column<double>(type: "double precision", nullable: true),
                    GpuTemperature = table.Column<double>(type: "double precision", nullable: true),
                    GpuMemoryUsed = table.Column<long>(type: "bigint", nullable: true),
                    GpuMemoryTotal = table.Column<long>(type: "bigint", nullable: true),
                    DiskUsage = table.Column<double>(type: "double precision", nullable: true),
                    DiskUsed = table.Column<long>(type: "bigint", nullable: true),
                    DiskTotal = table.Column<long>(type: "bigint", nullable: true),
                    DiskReadSpeed = table.Column<double>(type: "double precision", nullable: true),
                    DiskWriteSpeed = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardwareMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardwareMetrics_PCs_PCId",
                        column: x => x.PCId,
                        principalTable: "PCs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PCId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DownloadSpeed = table.Column<double>(type: "double precision", nullable: true),
                    UploadSpeed = table.Column<double>(type: "double precision", nullable: true),
                    BytesReceived = table.Column<long>(type: "bigint", nullable: true),
                    BytesSent = table.Column<long>(type: "bigint", nullable: true),
                    Latency = table.Column<double>(type: "double precision", nullable: true),
                    PacketLoss = table.Column<double>(type: "double precision", nullable: true),
                    IsConnected = table.Column<bool>(type: "boolean", nullable: true),
                    NetworkInterface = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkMetrics_PCs_PCId",
                        column: x => x.PCId,
                        principalTable: "PCs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PCHardwareConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PCId = table.Column<int>(type: "integer", nullable: false),
                    Processor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GraphicsCard = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Motherboard = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RamCapacity = table.Column<long>(type: "bigint", nullable: true),
                    StorageCapacity = table.Column<long>(type: "bigint", nullable: true),
                    StorageType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PCHardwareConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PCHardwareConfigs_PCs_PCId",
                        column: x => x.PCId,
                        principalTable: "PCs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SoftwareInstalled",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PCId = table.Column<int>(type: "integer", nullable: false),
                    SoftwareId = table.Column<int>(type: "integer", nullable: false),
                    InstalledVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InstalledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UninstalledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "SoftwareUsageHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PCId = table.Column<int>(type: "integer", nullable: false),
                    ApplicationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareUsageHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftwareUsageHistory_PCs_PCId",
                        column: x => x.PCId,
                        principalTable: "PCs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PCId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLogs_PCs_PCId",
                        column: x => x.PCId,
                        principalTable: "PCs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WebsiteUsageHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PCId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Domain = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VisitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VisitCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebsiteUsageHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebsiteUsageHistory_PCs_PCId",
                        column: x => x.PCId,
                        principalTable: "PCs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_PCId",
                table: "Alerts",
                column: "PCId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_UserId",
                table: "Alerts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HardwareMetrics_PCId_Timestamp",
                table: "HardwareMetrics",
                columns: new[] { "PCId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkMetrics_PCId_Timestamp",
                table: "NetworkMetrics",
                columns: new[] { "PCId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_PCHardwareConfigs_PCId",
                table: "PCHardwareConfigs",
                column: "PCId");

            migrationBuilder.CreateIndex(
                name: "IX_PCs_MacAddress",
                table: "PCs",
                column: "MacAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PCs_RoomId",
                table: "PCs",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_RoomId_Name",
                table: "Policies",
                columns: new[] { "RoomId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_RoomNumber",
                table: "Rooms",
                column: "RoomNumber",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareUsageHistory_PCId_StartTime",
                table: "SoftwareUsageHistory",
                columns: new[] { "PCId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLogs_PCId",
                table: "UserLogs",
                column: "PCId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogs_UserId_Timestamp",
                table: "UserLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebsiteUsageHistory_PCId_VisitedAt",
                table: "WebsiteUsageHistory",
                columns: new[] { "PCId", "VisitedAt" });

            // Seed default room to satisfy FK_PCs_Rooms_RoomId
            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "RoomNumber", "Description", "Capacity", "IsActive", "CreatedAt" },
                values: new object[] { 1, "DEFAULT", "Default room for unassigned PCs", 0, true, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
            );

            // Seed default users with BCrypt hashed passwords (password: "admin")
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Username", "PasswordHash", "Role", "FullName", "IsActive", "CreatedAt", "LastLoginAt" },
                values: new object[,]
                {
                    { 1, "admin", "$2a$11$e6AtSfzSfXfCHsk5yjXWIuzIGGfaXRe/Z1GnuMxYx1nfSXlVepAN.", "SystemAdministrator", null, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 2, "itperson", "$2a$11$1Unk6pMkXdwNQjxP3m96M.DggxMbjbSx57fN9TQ6YWwtObK5SFwwO", "ITPersonnel", null, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 3, "faculty", "$2a$11$TMXewyIW8gRGGutz2DDDbeVLEMp9mVyhlijNJvMXzbV5tdZwH07Si", "Faculty", null, true, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 1);

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

            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "HardwareMetrics");

            migrationBuilder.DropTable(
                name: "NetworkMetrics");

            migrationBuilder.DropTable(
                name: "PCHardwareConfigs");

            migrationBuilder.DropTable(
                name: "Policies");

            migrationBuilder.DropTable(
                name: "SoftwareInstalled");

            migrationBuilder.DropTable(
                name: "SoftwareRequests");

            migrationBuilder.DropTable(
                name: "SoftwareUsageHistory");

            migrationBuilder.DropTable(
                name: "UserLogs");

            migrationBuilder.DropTable(
                name: "WebsiteUsageHistory");

            migrationBuilder.DropTable(
                name: "Software");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "PCs");

            migrationBuilder.DropTable(
                name: "Rooms");
        }
    }
}
