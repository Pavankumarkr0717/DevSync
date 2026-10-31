using DevSync.Domain.Common;

namespace DevSync.Domain.Workspaces;

/// <summary>
/// A named container for future files, diagrams and collaborators. Aggregate root.
/// Invariant: exactly one Owner membership, for <see cref="OwnerId"/>, which never changes (spec 002 FR-023).
/// </summary>
public sealed class Workspace
{
    private readonly List<WorkspaceMembership> _members = [];

    // Used by EF Core when materializing.
    private Workspace()
    {
        Name = string.Empty;
    }

    private Workspace(Guid id, string name, string? description, Guid ownerId, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Description = description;
        OwnerId = ownerId;
        CreatedAt = now;
        UpdatedAt = now;
        _members.Add(new WorkspaceMembership(id, ownerId, WorkspaceRole.Owner, now));
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Guid OwnerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<WorkspaceMembership> Members => _members.AsReadOnly();

    /// <summary>
    /// Validates the input and creates a workspace whose creator is its owner and only member.
    /// Returns every field error at once; never returns a partially built workspace.
    /// </summary>
    public static Result<Workspace> Create(string? name, string? description, Guid ownerId, TimeProvider time)
    {
        var nameResult = WorkspaceName.Create(name);
        var descriptionResult = WorkspaceDescription.Create(description);

        var errors = nameResult.Errors.Concat(descriptionResult.Errors).ToList();
        if (errors.Count > 0)
        {
            return Result<Workspace>.Failure(errors);
        }

        var now = time.GetUtcNow();
        var workspace = new Workspace(
            Guid.CreateVersion7(now),
            nameResult.Value.Value,
            descriptionResult.Value?.Value,
            ownerId,
            now);

        return Result<Workspace>.Success(workspace);
    }

    /// <summary>Changes name and description with the same rules as creation. Returns field errors; changes nothing on failure.</summary>
    public IReadOnlyList<FieldError> UpdateDetails(string? name, string? description, TimeProvider time)
    {
        var nameResult = WorkspaceName.Create(name);
        var descriptionResult = WorkspaceDescription.Create(description);

        var errors = nameResult.Errors.Concat(descriptionResult.Errors).ToList();
        if (errors.Count > 0)
        {
            return errors;
        }

        Name = nameResult.Value.Value;
        Description = descriptionResult.Value?.Value;
        UpdatedAt = time.GetUtcNow();
        return [];
    }

    /// <summary>Adds a member (invitation acceptance). Only Editor or Viewer can be granted.</summary>
    public DomainError? AddMember(Guid userId, WorkspaceRole role, DateTimeOffset now)
    {
        if (!Invitation.IsAssignableRole(role))
        {
            return WorkspaceErrors.AssignableRoleOnly;
        }

        if (_members.Any(m => m.UserId == userId))
        {
            return WorkspaceErrors.AlreadyMember;
        }

        _members.Add(new WorkspaceMembership(Id, userId, role, now));
        return null;
    }

    public DomainError? ChangeMemberRole(Guid userId, WorkspaceRole role)
    {
        if (!Invitation.IsAssignableRole(role))
        {
            return WorkspaceErrors.AssignableRoleOnly;
        }

        var member = _members.SingleOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return WorkspaceErrors.MemberNotFound;
        }

        if (member.Role == WorkspaceRole.Owner)
        {
            return WorkspaceErrors.CannotChangeOwner;
        }

        member.ChangeRole(role);
        return null;
    }

    public DomainError? RemoveMember(Guid userId)
    {
        var member = _members.SingleOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return WorkspaceErrors.MemberNotFound;
        }

        if (member.Role == WorkspaceRole.Owner)
        {
            return WorkspaceErrors.CannotRemoveOwner;
        }

        _members.Remove(member);
        return null;
    }
}
