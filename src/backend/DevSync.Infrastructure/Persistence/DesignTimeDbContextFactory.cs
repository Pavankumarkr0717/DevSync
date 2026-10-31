using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevSync.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c> to create migrations. It never connects, so the connection string
/// below is a non-secret placeholder.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DevSyncDbContext>
{
    public DevSyncDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DevSyncDbContext>()
            .UseNpgsql("Host=localhost;Database=devsync_design_time")
            .Options;
        return new DevSyncDbContext(options);
    }
}
