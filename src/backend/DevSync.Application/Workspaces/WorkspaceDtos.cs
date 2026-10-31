using System.Text.Json.Serialization;
using DevSync.Application.Identity;
using DevSync.Domain.Workspaces;

namespace DevSync.Application.Workspaces;

public sealed record WorkspaceDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    UserRefDto Owner,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MemberDto> Members,
    WorkspaceRole MyRole);

/// <summary>A member. <see cref="Email"/> is only filled (and only serialized) for callers allowed to see it.</summary>
public sealed record MemberDto(
    Guid UserId,
    string DisplayName,
    WorkspaceRole Role,
    DateTimeOffset JoinedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Email = null);

public sealed record WorkspaceSummaryDto(Guid Id, string Name, WorkspaceRole Role, DateTimeOffset CreatedAt);

public sealed record WorkspaceRefDto(Guid Id, string Name);

public sealed record WorkspaceInvitationDto(
    Guid Id,
    string Email,
    WorkspaceRole Role,
    UserRefDto InvitedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record ReceivedInvitationDto(
    Guid Id,
    WorkspaceRefDto Workspace,
    WorkspaceRole Role,
    UserRefDto InvitedBy,
    DateTimeOffset ExpiresAt);
