using Microsoft.EntityFrameworkCore;
using IRIS.Core.Models;

namespace IRIS.Core.Data
{
    public class IRISDbContext : DbContext
    {
        public IRISDbContext(DbContextOptions<IRISDbContext> options) : base(options)
        {
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
                .HasIndex(wuh => new { wuh.PCId, wuh.VisitedAt });
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

            modelBuilder.Entity<Alert>()
                .Property(a => a.Severity)
                .HasConversion<string>();

            modelBuilder.Entity<Alert>()
                .Property(a => a.Type)
                .HasConversion<string>();
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
    }
}