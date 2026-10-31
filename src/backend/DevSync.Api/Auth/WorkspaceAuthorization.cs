using DevSync.Api.Errors;
using DevSync.Application.Common;
using DevSync.Application.Workspaces;
using DevSync.Domain.Workspaces;
using Microsoft.AspNetCore.Authorization;

namespace DevSync.Api.Auth;

/// <summary>The resource checked by workspace authorization: which workspace, and the caller's current role in it.</summary>
public sealed record WorkspaceAccessContext(Guid WorkspaceId, WorkspaceRole Role);

public sealed class WorkspaceOperationRequirement(WorkspaceOperation operation) : IAuthorizationRequirement
{
    public WorkspaceOperation Operation { get; } = operation;
}

/// <summary>ASP.NET Core resource-based authorization backed by the domain permission table.</summary>
public sealed class WorkspaceOperationHandler : AuthorizationHandler<WorkspaceOperationRequirement, WorkspaceAccessContext>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, WorkspaceOperationRequirement requirement, WorkspaceAccessContext resource)
    {
        if (WorkspacePermissions.Allows(resource.Role, requirement.Operation))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public static class WorkspaceAuthorization
{
    private const string AccessItem = "DevSync.WorkspaceAccess";

    /// <summary>
    /// Guards a <c>{workspaceId}</c> route (spec 002 plan, Authorization flow): malformed id or no
    /// membership → 404 (indistinguishable from missing); member without the operation → 403.
    /// The role is read from the database on every request (FR-025).
    /// </summary>
    public static RouteHandlerBuilder RequireWorkspace(this RouteHandlerBuilder builder, WorkspaceOperation operation) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (!Guid.TryParse(http.Request.RouteValues["workspaceId"] as string, out var workspaceId))
            {
                return Problems.NotFound();
            }

            var services = http.RequestServices;
            var userId = services.GetRequiredService<ICurrentUser>().UserId;
            var role = await services.GetRequiredService<IWorkspaceAccess>().GetRoleAsync(workspaceId, userId, http.RequestAborted);
            if (role is null)
            {
                return Problems.NotFound();
            }

            var access = new WorkspaceAccessContext(workspaceId, role.Value);
            var result = await services.GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(http.User, access, new WorkspaceOperationRequirement(operation));
            if (!result.Succeeded)
            {
                return Problems.Error(ErrorCodes.Forbidden);
            }

            http.Items[AccessItem] = access;
            return await next(context);
        });

    /// <summary>The access established by <see cref="RequireWorkspace"/> for this request.</summary>
    public static WorkspaceAccessContext Access(HttpContext context) =>
        context.Items[AccessItem] as WorkspaceAccessContext
        ?? throw new InvalidOperationException("RequireWorkspace was not applied to this endpoint.");
}
