using DevSync.Api.Auth;
using DevSync.Api.Errors;
using DevSync.Api.Files;
using DevSync.Application.Common;
using DevSync.Application.Diagrams;
using DevSync.Domain.Diagrams;
using DevSync.Domain.Workspaces;

namespace DevSync.Api.Endpoints;

public sealed record DiagramNameRequest(string? Name);

/// <summary>
/// Workspace diagrams (spec 004 contracts/openapi.yaml). Every route is guarded by
/// <c>RequireWorkspace</c>; diagrams are addressed by id within the route's workspace, and nothing
/// here logs diagram names or content (NFR-001).
/// </summary>
public static class DiagramEndpoints
{
    public static IEndpointRouteBuilder MapDiagramEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceId}/diagrams").RequireAuthorization();

        group.MapGet(string.Empty, ListAsync).RequireWorkspace(WorkspaceOperation.Read);
        group.MapPost(string.Empty, CreateAsync).RequireWorkspace(WorkspaceOperation.EditDiagrams);
        group.MapGet("/{diagramId}", GetAsync).RequireWorkspace(WorkspaceOperation.Read);
        group.MapPost("/{diagramId}/rename", RenameAsync).RequireWorkspace(WorkspaceOperation.EditDiagrams);
        group.MapPut("/{diagramId}/document", SaveDocumentAsync).RequireWorkspace(WorkspaceOperation.EditDiagrams);
        group.MapDelete("/{diagramId}", DeleteAsync).RequireWorkspace(WorkspaceOperation.DeleteDiagrams);

        return app;
    }

    private static async Task<IResult> ListAsync(HttpContext context, ListDiagramsHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, ct));

    private static async Task<IResult> CreateAsync(
        HttpContext context, DiagramNameRequest request, CreateDiagramHandler handler, CancellationToken ct)
    {
        var workspaceId = WorkspaceAuthorization.Access(context).WorkspaceId;
        var outcome = await handler.HandleAsync(workspaceId, request.Name, ct);
        return outcome.IsSuccess
            ? TypedResults.Created($"/api/workspaces/{workspaceId}/diagrams/{outcome.Value.Id}", outcome.Value)
            : Problems.From(outcome);
    }

    private static async Task<IResult> GetAsync(HttpContext context, string diagramId, GetDiagramHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParse(diagramId, out var id))
        {
            return Problems.Error(ErrorCodes.DiagramNotFound);
        }

        var diagram = await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, id, ct);
        if (diagram is null)
        {
            return Problems.Error(ErrorCodes.DiagramNotFound);
        }

        context.Response.Headers.ETag = ContentBody.ETag(diagram.Revision);
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(diagram);
    }

    private static async Task<IResult> RenameAsync(
        HttpContext context, string diagramId, DiagramNameRequest request, RenameDiagramHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParse(diagramId, out var id))
        {
            return Problems.Error(ErrorCodes.DiagramNotFound);
        }

        var outcome = await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, id, request.Name, ct);
        return outcome.IsSuccess ? TypedResults.Ok(outcome.Value) : Problems.From(outcome);
    }

    /// <summary>The body is the whole document as JSON; <c>If-Match</c> states the revision it was based on (research R5, R6).</summary>
    private static async Task<IResult> SaveDocumentAsync(
        HttpContext context, string diagramId, bool? replaceUnsupported, SaveDiagramDocumentHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParse(diagramId, out var id))
        {
            return Problems.Error(ErrorCodes.DiagramNotFound);
        }

        var (revision, preconditionError) = ContentBody.ParseIfMatch(context.Request);
        if (preconditionError is not null)
        {
            return Problems.Error(preconditionError);
        }

        var (body, bodyError) = await ContentBody.ReadBytesAsync(context.Request, ContentBody.IsJson, DiagramLimits.MaxDocumentBytes, ct);
        if (bodyError is not null)
        {
            return Problems.Error(bodyError);
        }

        var outcome = await handler.HandleAsync(
            WorkspaceAuthorization.Access(context).WorkspaceId, id, revision, body, replaceUnsupported == true, ct);
        if (outcome.IsSuccess)
        {
            context.Response.Headers.ETag = ContentBody.ETag(outcome.Value.Revision);
            return TypedResults.Ok(outcome.Value);
        }

        if (outcome.Extra?.GetValueOrDefault(SaveDiagramDocumentHandler.CurrentRevision) is long current)
        {
            context.Response.Headers.ETag = ContentBody.ETag(current);
        }

        return Problems.From(outcome);
    }

    private static async Task<IResult> DeleteAsync(HttpContext context, string diagramId, DeleteDiagramHandler handler, CancellationToken ct)
    {
        if (!Guid.TryParse(diagramId, out var id))
        {
            return Problems.Error(ErrorCodes.DiagramNotFound);
        }

        var outcome = await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, id, ct);
        return outcome.IsSuccess ? TypedResults.NoContent() : Problems.From(outcome);
    }
}
