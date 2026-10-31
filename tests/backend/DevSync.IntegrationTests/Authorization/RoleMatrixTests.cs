using System.Net;
using System.Net.Http.Json;
using DevSync.Domain.Workspaces;
using DevSync.Infrastructure.Persistence;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DevSync.IntegrationTests.Authorization;

public enum Caller
{
    Owner,
    Editor,
    Viewer,
    NonMember,
    Anonymous,
}

public enum Operation
{
    GetDetails,
    EditDetails,
    ChangeRole,
    RemoveMember,
    ListInvitations,
    Invite,
    RevokeInvitation,

    // Spec 003 FR-024
    GetTree,
    CreateItem,
    ReadContent,
    SaveContent,
    RenameItem,
    MoveItem,
    DeleteItem,

    // Spec 004 FR-030
    ListDiagrams,
    CreateDiagram,
    GetDiagram,
    SaveDiagram,
    RenameDiagram,
    DeleteDiagram,
}

/// <summary>
/// Every protected workspace endpoint × every kind of caller (spec 002 FR-027, NFR-003, SC-002;
/// spec 003 FR-024, NFR-002, SC-005).
/// Each case runs against a fresh workspace and verifies that refused calls changed nothing.
/// </summary>
public sealed class RoleMatrixTests(PostgresFixture postgres)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static Shared? s_shared;

    public static TheoryData<Operation, Caller, int> Cases()
    {
        var data = new TheoryData<Operation, Caller, int>();
        foreach (var op in Enum.GetValues<Operation>())
        {
            foreach (var caller in Enum.GetValues<Caller>())
            {
                data.Add(op, caller, Expected(op, caller));
            }
        }

        return data;
    }

    private static int Expected(Operation op, Caller caller) => caller switch
    {
        Caller.Anonymous => 401,
        Caller.NonMember => 404,
        Caller.Owner => op switch
        {
            Operation.Invite or Operation.CreateItem or Operation.CreateDiagram => 201,
            Operation.RemoveMember or Operation.RevokeInvitation or Operation.DeleteItem or Operation.DeleteDiagram => 204,
            _ => 200,
        },
        Caller.Editor => op switch
        {
            Operation.GetDetails or Operation.EditDetails or Operation.GetTree or Operation.ReadContent or Operation.SaveContent
                or Operation.RenameItem or Operation.MoveItem
                or Operation.ListDiagrams or Operation.GetDiagram or Operation.SaveDiagram or Operation.RenameDiagram => 200,
            Operation.CreateItem or Operation.CreateDiagram => 201,
            _ => 403,
        },
        Caller.Viewer => op is Operation.GetDetails or Operation.GetTree or Operation.ReadContent
            or Operation.ListDiagrams or Operation.GetDiagram ? 200 : 403,
        _ => throw new ArgumentOutOfRangeException(nameof(caller)),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Each_role_gets_exactly_its_permissions(Operation operation, Caller caller, int expectedStatus)
    {
        var shared = await SharedAsync();
        var factory = shared.Factory;
        var owner = await factory.ClientForAsync(shared.Owner);
        var workspaceId = (await owner.CreateWorkspaceAsync("Matrix")).GetProperty("id").GetGuid();
        await factory.AddMemberAsync(workspaceId, shared.Editor, WorkspaceRole.Editor);
        await factory.AddMemberAsync(workspaceId, shared.Viewer, WorkspaceRole.Viewer);
        await factory.AddMemberAsync(workspaceId, shared.Target, WorkspaceRole.Viewer);
        var invitationId = (await (await owner.PostAsJsonAsync($"/api/workspaces/{workspaceId}/invitations",
            new { email = $"pending-{Guid.NewGuid():N}@example.com", role = "viewer" })).ReadJsonAsync()).GetProperty("id").GetGuid();
        var folderId = await owner.NewFolderAsync(workspaceId, null, "src");
        var fileId = await owner.NewFileAsync(workspaceId, folderId, "main.ts");
        await owner.SaveContentAsync(workspaceId, fileId, 1, "original");
        var diagramId = await owner.NewDiagramAsync(workspaceId, "Architecture");
        await owner.SaveDocumentAsync(workspaceId, diagramId, 1, DiagramTestData.SampleDocument().ToJsonString());
        var before = await SnapshotAsync(factory, workspaceId);

        var client = caller switch
        {
            Caller.Owner => owner,
            Caller.Editor => await factory.ClientForAsync(shared.Editor),
            Caller.Viewer => await factory.ClientForAsync(shared.Viewer),
            Caller.NonMember => await factory.ClientForAsync(shared.NonMember),
            _ => factory.CreateClient(),
        };
        var basePath = $"/api/workspaces/{workspaceId}";
        var response = operation switch
        {
            Operation.GetDetails => await client.GetAsync(basePath),
            Operation.EditDetails => await client.PutAsJsonAsync(basePath, new { name = "Renamed" }),
            Operation.ChangeRole => await client.PatchAsJsonAsync($"{basePath}/members/{shared.Target}", new { role = "editor" }),
            Operation.RemoveMember => await client.DeleteAsync($"{basePath}/members/{shared.Target}"),
            Operation.ListInvitations => await client.GetAsync($"{basePath}/invitations"),
            Operation.Invite => await client.PostAsJsonAsync($"{basePath}/invitations", new { email = "new@example.com", role = "editor" }),
            Operation.RevokeInvitation => await client.DeleteAsync($"{basePath}/invitations/{invitationId}"),
            Operation.GetTree => await client.GetAsync($"{basePath}/tree"),
            Operation.CreateItem => await client.CreateItemAsync(workspaceId, folderId, "file", "new.ts"),
            Operation.ReadContent => await client.GetContentResponseAsync(workspaceId, fileId),
            Operation.SaveContent => await client.SaveContentAsync(workspaceId, fileId, 2, "changed"),
            Operation.RenameItem => await client.RenameAsync(workspaceId, fileId, "renamed.ts"),
            Operation.MoveItem => await client.MoveAsync(workspaceId, fileId, null),
            Operation.DeleteItem => await client.DeleteItemAsync(workspaceId, folderId),
            Operation.ListDiagrams => await client.GetAsync(DiagramTestData.DiagramsPath(workspaceId)),
            Operation.CreateDiagram => await client.CreateDiagramAsync(workspaceId, "New one"),
            Operation.GetDiagram => await client.GetDiagramResponseAsync(workspaceId, diagramId),
            Operation.SaveDiagram => await client.SaveDocumentAsync(workspaceId, diagramId, 2, DiagramTestData.Document().ToJsonString()),
            Operation.RenameDiagram => await client.RenameDiagramAsync(workspaceId, diagramId, "Renamed"),
            Operation.DeleteDiagram => await client.DeleteDiagramAsync(workspaceId, diagramId),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        Assert.True(expectedStatus == (int)response.StatusCode,
            $"{operation} as {caller}: expected {expectedStatus}, got {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        if (expectedStatus >= 400)
        {
            var code = expectedStatus switch { 401 => "unauthenticated", 403 => "forbidden", _ => "not_found" };
            await response.AssertProblemAsync(expectedStatus, code);
            Assert.Equal(before, await SnapshotAsync(factory, workspaceId));
        }
    }

    /// <summary>Everything a refused call must not change: name, memberships, invitations, items and file contents.</summary>
    private static async Task<string> SnapshotAsync(DevSyncApiFactory factory, Guid workspaceId)
    {
        await using var scope = factory.CreateDbScope();
        var db = scope.Db;
        var name = await db.Workspaces.AsNoTracking().Where(w => w.Id == workspaceId).Select(w => w.Name).SingleAsync();
        var members = await db.WorkspaceMemberships.AsNoTracking().Where(m => m.WorkspaceId == workspaceId)
            .OrderBy(m => m.UserId).Select(m => m.UserId + ":" + m.Role).ToListAsync();
        var invitations = await db.WorkspaceInvitations.AsNoTracking().Where(i => i.WorkspaceId == workspaceId)
            .OrderBy(i => i.Id).Select(i => i.Email + ":" + i.Status).ToListAsync();
        var items = await db.WorkspaceItems.AsNoTracking().Where(i => i.WorkspaceId == workspaceId)
            .OrderBy(i => i.Id).Select(i => i.Id + ":" + i.ParentId + ":" + i.Name).ToListAsync();
        var contents = await (
                from c in db.FileContents.AsNoTracking()
                join i in db.WorkspaceItems.AsNoTracking() on c.ItemId equals i.Id
                where i.WorkspaceId == workspaceId
                orderby c.ItemId
                select c.ItemId + ":" + c.Revision + ":" + c.Content)
            .ToListAsync();
        var diagrams = await db.Diagrams.AsNoTracking().Where(d => d.WorkspaceId == workspaceId)
            .OrderBy(d => d.Id).Select(d => d.Id + ":" + d.Name + ":" + d.Revision + ":" + d.Document).ToListAsync();
        return $"{name}|{string.Join(",", members)}|{string.Join(",", invitations)}|{string.Join(",", items)}|{string.Join(",", contents)}|{string.Join(",", diagrams)}";
    }

    private async Task<Shared> SharedAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (s_shared is null)
            {
                var factory = await DevSyncApiFactory.CreateAsync(postgres);
                s_shared = new Shared(
                    factory,
                    await factory.CreateAccountAsync("Owner"),
                    await factory.CreateAccountAsync("Editor"),
                    await factory.CreateAccountAsync("Viewer"),
                    await factory.CreateAccountAsync("Target"),
                    await factory.CreateAccountAsync("Outsider"));
            }

            return s_shared;
        }
        finally
        {
            Gate.Release();
        }
    }

    private sealed record Shared(DevSyncApiFactory Factory, Guid Owner, Guid Editor, Guid Viewer, Guid Target, Guid NonMember);
}
