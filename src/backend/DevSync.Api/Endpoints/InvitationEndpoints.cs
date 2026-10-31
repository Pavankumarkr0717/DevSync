using DevSync.Api.Errors;
using DevSync.Application.Workspaces;

namespace DevSync.Api.Endpoints;

/// <summary>The invitee's inbox (spec 002 FR-020). Invitations not addressed to the caller are "not found".</summary>
public static class InvitationEndpoints
{
    private const string NotFoundTitle = "Invitation not found.";

    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invitations").RequireAuthorization();

        group.MapGet(string.Empty, async (ListMyInvitationsHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(ct)));

        group.MapPost("/{invitationId}/accept", async (string invitationId, AcceptInvitationHandler handler, CancellationToken ct) =>
        {
            if (!Guid.TryParse(invitationId, out var id))
            {
                return Problems.NotFound(NotFoundTitle);
            }

            var outcome = await handler.HandleAsync(id, ct);
            return outcome.IsSuccess
                ? Results.Ok(outcome.Value)
                : outcome.Error == Application.Common.ErrorCodes.NotFound ? Problems.NotFound(NotFoundTitle) : Problems.From(outcome);
        });

        group.MapPost("/{invitationId}/decline", async (string invitationId, DeclineInvitationHandler handler, CancellationToken ct) =>
        {
            if (!Guid.TryParse(invitationId, out var id))
            {
                return Problems.NotFound(NotFoundTitle);
            }

            var outcome = await handler.HandleAsync(id, ct);
            return outcome.IsSuccess ? Results.NoContent() : Problems.NotFound(NotFoundTitle);
        });

        return app;
    }
}
