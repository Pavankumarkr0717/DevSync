using DevSync.Application.Auth;
using DevSync.Application.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace DevSync.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SessionStarter>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<LogoutAllHandler>();
        services.AddScoped<GetProfileHandler>();

        services.AddScoped<CreateWorkspaceHandler>();
        services.AddScoped<ListMyWorkspacesHandler>();
        services.AddScoped<GetWorkspaceDetailsHandler>();
        services.AddScoped<UpdateWorkspaceHandler>();
        services.AddScoped<ChangeMemberRoleHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<InviteMemberHandler>();
        services.AddScoped<RevokeInvitationHandler>();
        services.AddScoped<ListWorkspaceInvitationsHandler>();
        services.AddScoped<ListMyInvitationsHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<DeclineInvitationHandler>();
        return services;
    }
}
