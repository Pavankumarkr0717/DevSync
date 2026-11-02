using DevSync.Application.Auth;
using DevSync.Application.Diagrams;
using DevSync.Application.Files;
using DevSync.Application.Presence;
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

        services.AddScoped<GetTreeHandler>();
        services.AddScoped<CreateItemHandler>();
        services.AddScoped<GetFileContentHandler>();
        services.AddScoped<SaveFileContentHandler>();
        services.AddScoped<RenameItemHandler>();
        services.AddScoped<MoveItemHandler>();
        services.AddScoped<DeleteItemHandler>();

        services.AddSingleton(DevSync.Domain.Diagrams.DiagramDocumentUpgrader.Production);
        services.AddScoped<ListDiagramsHandler>();
        services.AddScoped<CreateDiagramHandler>();
        services.AddScoped<GetDiagramHandler>();
        services.AddScoped<RenameDiagramHandler>();
        services.AddScoped<SaveDiagramDocumentHandler>();
        services.AddScoped<DeleteDiagramHandler>();

        // Presence state lives for the life of the process (spec 005 FR-019).
        services.AddSingleton<IPresenceTracker, PresenceTracker>();
        services.AddScoped<WorkspacePresenceService>();
        return services;
    }
}
