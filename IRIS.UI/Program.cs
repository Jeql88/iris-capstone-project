using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using IRIS.Core.Data;

namespace IRIS.UI
{
    // Design-time DbContext factory for EF migrations
    public class IRISDbContextFactory : IDesignTimeDbContextFactory<IRISDbContext>
    {
        public IRISDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<IRISDbContext>();
            optionsBuilder.UseNpgsql(configuration.GetConnectionString("IRISDatabase"));

            return new IRISDbContext(optionsBuilder.Options);
        }
    }
}