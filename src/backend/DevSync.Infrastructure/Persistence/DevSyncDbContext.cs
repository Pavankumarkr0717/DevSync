using DevSync.Domain.Diagrams;
using DevSync.Domain.Files;
using DevSync.Domain.Identity;
using DevSync.Domain.Workspaces;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Infrastructure.Persistence;

/// <summary>
/// The single database context. Identity uses it for <see cref="AppUser"/> only; Identity's roles,
/// claims, logins and tokens tables are intentionally not mapped (spec 002 research R1).
/// </summary>
public sealed class DevSyncDbContext(DbContextOptions<DevSyncDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();

    public DbSet<Invitation> WorkspaceInvitations => Set<Invitation>();

    public DbSet<WorkspaceItem> WorkspaceItems => Set<WorkspaceItem>();

    public DbSet<FileContent> FileContents => Set<FileContent>();

    public DbSet<Diagram> Diagrams => Set<Diagram>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DevSyncDbContext).Assembly);
    }
}
