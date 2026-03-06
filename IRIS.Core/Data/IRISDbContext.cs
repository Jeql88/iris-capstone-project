using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using IRIS.Core.Models;

namespace IRIS.Core.Data
{
    public class IRISDbContext : DbContext
    {
        public IRISDbContext(DbContextOptions<IRISDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Only configure if not already configured (for design-time)
            if (!optionsBuilder.IsConfigured)
            {
                // This will be overridden by DI configuration at runtime
            }
            
            // Suppress the pending model changes warning during migrations
            optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        // DbSets for all entities
        public DbSet<User> Users { get; set; }
        public DbSet<PC> PCs { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<HardwareMetric> HardwareMetrics { get; set; }
        public DbSet<NetworkMetric> NetworkMetrics { get; set; }
        public DbSet<Software> Software { get; set; }
        public DbSet<SoftwareInstalled> SoftwareInstalled { get; set; }
        public DbSet<SoftwareRequest> SoftwareRequests { get; set; }
        public DbSet<SoftwareUsageHistory> SoftwareUsageHistory { get; set; }
        public DbSet<WebsiteUsageHistory> WebsiteUsageHistory { get; set; }
        public DbSet<UserLog> UserLogs { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<Policy> Policies { get; set; }
        public DbSet<PCHardwareConfig> PCHardwareConfigs { get; set; }
        public DbSet<DeploymentLog> DeploymentLogs { get; set; }
        public DbSet<SystemSettings> SystemSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships and constraints
            ConfigureUser(modelBuilder);
            ConfigurePC(modelBuilder);
            ConfigureRoom(modelBuilder);
            ConfigureHardwareMetric(modelBuilder);
            ConfigureNetworkMetric(modelBuilder);
            ConfigureSoftware(modelBuilder);
            ConfigureSoftwareInstalled(modelBuilder);
            ConfigureSoftwareRequest(modelBuilder);
            ConfigureSoftwareUsageHistory(modelBuilder);
            ConfigureWebsiteUsageHistory(modelBuilder);
            ConfigureUserLog(modelBuilder);
            ConfigureAlert(modelBuilder);
            ConfigurePolicy(modelBuilder);
            ConfigurePCHardwareConfig(modelBuilder);
            ConfigureDeploymentLog(modelBuilder);
            ConfigureSystemSettings(modelBuilder);

            // Seed test users with BCrypt hashed passwords (password: "admin")
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    PasswordHash = "$2a$11$e6AtSfzSfXfCHsk5yjXWIuzIGGfaXRe/Z1GnuMxYx1nfSXlVepAN.",
                    Role = UserRole.SystemAdministrator,
                    IsActive = true,
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    LastLoginAt = null
                },
                new User
                {
                    Id = 2,
                    Username = "itperson",
                    PasswordHash = "$2a$11$1Unk6pMkXdwNQjxP3m96M.DggxMbjbSx57fN9TQ6YWwtObK5SFwwO",
                    Role = UserRole.ITPersonnel,
                    IsActive = true,
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    LastLoginAt = null
                },
                new User
                {
                    Id = 3,
                    Username = "faculty",
                    PasswordHash = "$2a$11$TMXewyIW8gRGGutz2DDDbeVLEMp9mVyhlijNJvMXzbV5tdZwH07Si",
                    Role = UserRole.Faculty,
                    IsActive = true,
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    LastLoginAt = null
                }
            );

            // Seed default rooms
            modelBuilder.Entity<Room>().HasData(
                new Room
                {
                    Id = 1,
                    RoomNumber = "Lab 1",
                    Description = "Architecture Computer Lab 1",
                    Capacity = 20,
                    IsActive = true,
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Room
                {
                    Id = 2,
                    RoomNumber = "Lab 2",
                    Description = "Architecture Computer Lab 2",
                    Capacity = 20,
                    IsActive = true,
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Room
                {
                    Id = 3,
                    RoomNumber = "Lab 3",
                    Description = "Architecture Computer Lab 3",
                    Capacity = 20,
                    IsActive = true,
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new Room
                {
                    Id = 4,
                    RoomNumber = "Lab 4",
                    Description = "Architecture Computer Lab 4",
                    Capacity = 20,
                    IsActive = true,
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }

        private void ConfigureUser(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();
        }

        private void ConfigurePC(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PC>()
                .HasIndex(p => p.MacAddress)
                .IsUnique();

            modelBuilder.Entity<PC>()
                .Property(p => p.Status)
                .HasConversion<string>();

            modelBuilder.Entity<PC>()
                .HasOne(p => p.Room)
                .WithMany(r => r.PCs)
                .HasForeignKey(p => p.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private void ConfigureRoom(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Room>()
                .HasIndex(r => r.RoomNumber)
                .IsUnique();
        }

        private void ConfigureHardwareMetric(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HardwareMetric>()
                .HasOne(hm => hm.PC)
                .WithMany(p => p.HardwareMetrics)
                .HasForeignKey(hm => hm.PCId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HardwareMetric>()
                .HasIndex(hm => new { hm.PCId, hm.Timestamp });
        }

        private void ConfigureNetworkMetric(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<NetworkMetric>()
                .HasOne(nm => nm.PC)
                .WithMany(p => p.NetworkMetrics)
                .HasForeignKey(nm => nm.PCId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NetworkMetric>()
                .HasIndex(nm => new { nm.PCId, nm.Timestamp });
        }

        private void ConfigureSoftware(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Software>()
                .HasIndex(s => s.Name)
                .IsUnique();
        }

        private void ConfigureSoftwareInstalled(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SoftwareInstalled>()
                .HasOne(si => si.PC)
                .WithMany(p => p.SoftwareInstalled)
                .HasForeignKey(si => si.PCId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SoftwareInstalled>()
                .HasOne(si => si.Software)
                .WithMany(s => s.SoftwareInstalled)
                .HasForeignKey(si => si.SoftwareId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SoftwareInstalled>()
                .HasIndex(si => new { si.PCId, si.SoftwareId })
                .IsUnique();
        }

        private void ConfigureSoftwareRequest(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SoftwareRequest>()
                .HasOne(sr => sr.User)
                .WithMany(u => u.SoftwareRequests)
                .HasForeignKey(sr => sr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SoftwareRequest>()
                .HasOne(sr => sr.Software)
                .WithMany(s => s.SoftwareRequests)
                .HasForeignKey(sr => sr.SoftwareId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SoftwareRequest>()
                .HasOne(sr => sr.ReviewedByUser)
                .WithMany()
                .HasForeignKey(sr => sr.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SoftwareRequest>()
                .Property(sr => sr.Status)
                .HasConversion<string>();
        }

        private void ConfigureSoftwareUsageHistory(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SoftwareUsageHistory>()
                .HasOne(suh => suh.PC)
                .WithMany(p => p.SoftwareUsageHistory)
                .HasForeignKey(suh => suh.PCId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SoftwareUsageHistory>()
                .HasIndex(suh => new { suh.PCId, suh.StartTime });
        }

        private void ConfigureWebsiteUsageHistory(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WebsiteUsageHistory>()
                .HasOne(wuh => wuh.PC)
                .WithMany(p => p.WebsiteUsageHistory)
                .HasForeignKey(wuh => wuh.PCId)
                .OnDelete(DeleteBehavior.Cascade);

           modelBuilder.Entity<WebsiteUsageHistory>()
                .HasIndex(wuh => new { wuh.PCId, wuh.Browser, wuh.Domain, wuh.VisitedAt })
                .IsUnique();
        }

        private void ConfigureUserLog(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserLog>()
                .HasOne(ul => ul.User)
                .WithMany(u => u.UserLogs)
                .HasForeignKey(ul => ul.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserLog>()
                .HasOne(ul => ul.PC)
                .WithMany(p => p.UserLogs)
                .HasForeignKey(ul => ul.PCId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserLog>()
                .HasIndex(ul => new { ul.UserId, ul.Timestamp });
        }

        private void ConfigureAlert(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Alert>()
                .HasOne(a => a.PC)
                .WithMany(p => p.Alerts)
                .HasForeignKey(a => a.PCId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Alert>()
                .HasOne(a => a.User)
                .WithMany(u => u.Alerts)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Severity and Type stored as integer (enum default) — no HasConversion<string>()
            // to match actual PostgreSQL column types.

            modelBuilder.Entity<Alert>()
                .HasIndex(a => new { a.PCId, a.AlertKey, a.IsResolved });

            modelBuilder.Entity<Alert>()
                .HasIndex(a => new { a.IsResolved, a.CreatedAt });
        }

        private void ConfigurePolicy(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Policy>()
                .HasOne(p => p.Room)
                .WithMany(r => r.Policies)
                .HasForeignKey(p => p.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Policy>()
                .HasIndex(p => new { p.RoomId, p.Name })
                .IsUnique();
        }

        private void ConfigurePCHardwareConfig(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PCHardwareConfig>()
                .HasOne(phc => phc.PC)
                .WithMany(p => p.HardwareConfigs)
                .HasForeignKey(phc => phc.PCId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        private void ConfigureDeploymentLog(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DeploymentLog>()
                .HasOne(dl => dl.PC)
                .WithMany()
                .HasForeignKey(dl => dl.PCId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<DeploymentLog>()
                .HasIndex(dl => new { dl.PCId, dl.Timestamp });

            modelBuilder.Entity<DeploymentLog>()
                .HasIndex(dl => dl.Timestamp);
        }

        private void ConfigureSystemSettings(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SystemSettings>()
                .HasKey(s => s.Key);

            // Seed default retention settings
            var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            modelBuilder.Entity<SystemSettings>().HasData(
                new SystemSettings { Key = SettingsKeys.HardwareMetricRetentionDays, Value = "30", UpdatedAt = seedDate },
                new SystemSettings { Key = SettingsKeys.NetworkMetricRetentionDays, Value = "30", UpdatedAt = seedDate },
                new SystemSettings { Key = SettingsKeys.AlertRetentionDays, Value = "90", UpdatedAt = seedDate },
                new SystemSettings { Key = SettingsKeys.CleanupHourUtc, Value = "2", UpdatedAt = seedDate }
            );
        }

    }
}