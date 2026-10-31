using System.Text.Json.Serialization;
using DevSync.Api.Auth;
using DevSync.Api.Errors;
using DevSync.Api.Files;
using DevSync.Application.Common;
using DevSync.Application.Files;
using DevSync.Domain.Workspaces;

namespace DevSync.Api.Endpoints;

/// <summary><c>parentId</c> must be present (null = top level), so a forgotten field never silently means "top level".</summary>
public sealed record CreateItemRequest([property: JsonRequired] Guid? ParentId, string? Kind, string? Name);

public sealed record RenameItemRequest(string? Name);

/// <summary><c>parentId</c> must be present; null moves the item to the top level.</summary>
public sealed record MoveItemRequest([property: JsonRequired] Guid? ParentId);

/// <summary>
/// Workspace project files (spec 003 contracts/openapi.yaml). Every route is guarded by
/// <c>RequireWorkspace</c>; items are addressed only by id within the route's workspace, and
/// nothing here logs file names or content (NFR-001).
/// </summary>
public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceId}").RequireAuthorization();

        group.MapGet("/tree", GetTreeAsync).RequireWorkspace(WorkspaceOperation.Read);
        group.MapPost("/items", CreateItemAsync).RequireWorkspace(WorkspaceOperation.EditFiles);
        group.MapPost("/items/{itemId}/rename", RenameItemAsync).RequireWorkspace(WorkspaceOperation.EditFiles);
        group.MapPost("/items/{itemId}/move", MoveItemAsync).RequireWorkspace(WorkspaceOperation.EditFiles);
        group.MapDelete("/items/{itemId}", DeleteItemAsync).RequireWorkspace(WorkspaceOperation.DeleteFiles);

        group.MapGet("/files/{itemId}/content", GetContentAsync).RequireWorkspace(WorkspaceOperation.Read);
        group.MapPut("/files/{itemId}/content", SaveContentAsync).RequireWorkspace(WorkspaceOperation.EditFiles);

        return app;
    }

    private static async Task<IResult> GetTreeAsync(HttpContext context, GetTreeHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, ct));

    private static async Task<IResult> CreateItemAsync(
        HttpContext context, CreateItemRequest request, CreateItemHandler handler, CancellationToken ct)
    {
        var workspaceId = WorkspaceAuthorization.Access(context).WorkspaceId;
        var outcome = await handler.HandleAsync(workspaceId, new CreateItemCommand(request.ParentId, request.Kind, request.Name), ct);
        return outcome.IsSuccess
            ? TypedResults.Created($"/api/workspaces/{workspaceId}/items/{outcome.Value.Id}", outcome.Value)
            : Problems.From(outcome);
    }

    private static async Task<IResult> RenameItemAsync(
        HttpContext context, string itemId, RenameItemRequest request, RenameItemHandler handler, CancellationToken ct)
    {
        if (!TryItemId(itemId, out var id, out var notFound))
        {
            return notFound;
        }

        var outcome = await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, id, request.Name, ct);
        return outcome.IsSuccess ? TypedResults.Ok(outcome.Value) : Problems.From(outcome);
    }

    private static async Task<IResult> MoveItemAsync(
        HttpContext context, string itemId, MoveItemRequest request, MoveItemHandler handler, CancellationToken ct)
    {
        if (!TryItemId(itemId, out var id, out var notFound))
        {
            return notFound;
        }

        var outcome = await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, id, request.ParentId, ct);
        return outcome.IsSuccess ? TypedResults.Ok(outcome.Value) : Problems.From(outcome);
    }

    private static async Task<IResult> DeleteItemAsync(HttpContext context, string itemId, DeleteItemHandler handler, CancellationToken ct)
    {
        if (!TryItemId(itemId, out var id, out var notFound))
        {
            return notFound;
        }

        var outcome = await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, id, ct);
        return outcome.IsSuccess ? TypedResults.NoContent() : Problems.From(outcome);
    }

    private static async Task<IResult> GetContentAsync(
        HttpContext context, string itemId, GetFileContentHandler handler, CancellationToken ct)
    {
        if (!TryItemId(itemId, out var id, out var notFound))
        {
            return notFound;
        }

        var content = await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, id, ct);
        if (content is null)
        {
            return notFound;
        }

        // Source code stays out of browser and proxy caches.
        context.Response.Headers.ETag = ContentBody.ETag(content.Revision);
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Text(content.Content, "text/plain; charset=utf-8");
    }

    /// <summary>The raw body is the whole new content; <c>If-Match</c> states the revision it was based on (research R5).</summary>
    private static async Task<IResult> SaveContentAsync(
        HttpContext context, string itemId, SaveFileContentHandler handler, CancellationToken ct)
    {
        if (!TryItemId(itemId, out var id, out var notFound))
        {
            return notFound;
        }

        var (revision, preconditionError) = ContentBody.ParseIfMatch(context.Request);
        if (preconditionError is not null)
        {
            return Problems.Error(preconditionError);
        }

        var (text, bodyError) = await ContentBody.ReadAsync(context.Request, ct);
        if (bodyError is not null)
        {
            return Problems.Error(bodyError);
        }

        var outcome = await handler.HandleAsync(WorkspaceAuthorization.Access(context).WorkspaceId, id, revision, text!, ct);
        if (outcome.IsSuccess)
        {
            context.Response.Headers.ETag = ContentBody.ETag(outcome.Value.Revision);
            return TypedResults.Ok(outcome.Value);
        }

        if (outcome.Extra?[SaveFileContentHandler.CurrentRevision] is long current)
        {
            context.Response.Headers.ETag = ContentBody.ETag(current);
        }

        return Problems.From(outcome);
    }

    /// <summary>Malformed item ids are "not found", exactly like ids that do not exist.</summary>
    private static bool TryItemId(string itemId, out Guid id, out IResult notFound)
    {
        notFound = Problems.Error(ErrorCodes.ItemNotFound);
        return Guid.TryParse(itemId, out id);
    }
}
