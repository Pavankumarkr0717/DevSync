using DevSync.Application.Auth;
using DevSync.Application.Diagrams;
using DevSync.Application.Files;
using DevSync.Application.Identity;
using DevSync.Application.Workspaces;
using DevSync.Infrastructure.Diagrams;
using DevSync.Infrastructure.Files;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using DevSync.Infrastructure.Workspaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevSync.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DevSync";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName);
        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        // The connection string is read when the context is first resolved, so test hosts and
        // environment variables are always applied.
        services.AddDbContext<DevSyncDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured. Set ConnectionStrings__{ConnectionStringName} " +
                    "(see .env.example and README.md).");
            }

            options.UseNpgsql(connectionString);
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(false);
        });

        // ASP.NET Core Identity for accounts and password hashing only (no UI, roles or cookies).
        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty; // user name = email; any characters
                options.Lockout.AllowedForNewUsers = false; // replaced by LoginAttemptLimiter (no enumeration)
            })
            .AddEntityFrameworkStores<DevSyncDbContext>()
            .AddPasswordValidator<PasswordPolicyValidator>();

        services.AddScoped<IUserAccounts, UserAccounts>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddSingleton<ILoginAttemptLimiter, LoginAttemptLimiter>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IWorkspaceAccess, WorkspaceAccess>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IProjectFileRepository, ProjectFileRepository>();
        services.AddScoped<IDiagramRepository, DiagramRepository>();

        return services;
    }
}
