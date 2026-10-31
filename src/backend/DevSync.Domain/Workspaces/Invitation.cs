using DevSync.Domain.Common;

namespace DevSync.Domain.Workspaces;

/// <summary>
/// An Owner's offer for an email address to join a workspace as Editor or Viewer
/// (spec 002 FR-020). Only the account whose normalized email matches may answer it.
/// </summary>
public sealed class Invitation
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invitation()
    {
        Email = string.Empty;
        NormalizedEmail = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Email { get; private set; }

    public string NormalizedEmail { get; private set; }

    public WorkspaceRole Role { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public InvitationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public Guid? AcceptedUserId { get; private set; }

    public static bool IsAssignableRole(WorkspaceRole role) => role is WorkspaceRole.Editor or WorkspaceRole.Viewer;

    public static Result<Invitation> Create(Guid workspaceId, string? email, WorkspaceRole role, Guid invitedBy, TimeProvider time)
    {
        var emailResult = EmailAddress.Create(email);
        var errors = emailResult.Errors.ToList();
        if (!IsAssignableRole(role))
        {
            errors.Add(new FieldError("role", "Role must be editor or viewer."));
        }

        if (errors.Count > 0)
        {
            return Result<Invitation>.Failure(errors);
        }

        var now = time.GetUtcNow();
        return Result<Invitation>.Success(new Invitation
        {
            Id = Guid.CreateVersion7(now),
            WorkspaceId = workspaceId,
            Email = emailResult.Value,
            NormalizedEmail = EmailAddress.Normalize(emailResult.Value),
            Role = role,
            InvitedByUserId = invitedBy,
            Status = InvitationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        });
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>Pending and not yet expired at <paramref name="now"/>; marks it expired if it has lapsed.</summary>
    public bool IsOpen(DateTimeOffset now)
    {
        if (Status == InvitationStatus.Pending && IsExpired(now))
        {
            Status = InvitationStatus.Expired;
        }

        return Status == InvitationStatus.Pending;
    }

    public DomainError? Accept(Guid userId, string userNormalizedEmail, DateTimeOffset now)
    {
        if (!IsOpen(now) || !string.Equals(NormalizedEmail, userNormalizedEmail, StringComparison.Ordinal))
        {
            return WorkspaceErrors.InvitationNotFound;
        }

        Status = InvitationStatus.Accepted;
        RespondedAt = now;
        AcceptedUserId = userId;
        return null;
    }

    public DomainError? Decline(string userNormalizedEmail, DateTimeOffset now)
    {
        if (!IsOpen(now) || !string.Equals(NormalizedEmail, userNormalizedEmail, StringComparison.Ordinal))
        {
            return WorkspaceErrors.InvitationNotFound;
        }

        Status = InvitationStatus.Declined;
        RespondedAt = now;
        return null;
    }

    public DomainError? Revoke(DateTimeOffset now)
    {
        if (!IsOpen(now))
        {
            return WorkspaceErrors.InvitationNotFound;
        }

        Status = InvitationStatus.Revoked;
        RespondedAt = now;
        return null;
    }
}
