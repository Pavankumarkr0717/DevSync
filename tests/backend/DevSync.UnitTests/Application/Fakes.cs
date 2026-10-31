using DevSync.Application.Auth;
using DevSync.Application.Common;
using DevSync.Application.Files;
using DevSync.Application.Identity;
using DevSync.Application.Workspaces;
using DevSync.Domain.Common;
using DevSync.Domain.Files;
using DevSync.Domain.Identity;
using DevSync.Domain.Workspaces;

namespace DevSync.UnitTests.Application;

internal sealed class FakeCurrentUser(Guid userId) : ICurrentUser
{
    public Guid UserId { get; } = userId;
}

internal sealed class FakeUserDirectory : IUserDirectory
{
    public Dictionary<Guid, (UserRefDto User, string NormalizedEmail)> Users { get; } = [];

    public FakeUserDirectory With(Guid id, string displayName, string email)
    {
        Users[id] = (new UserRefDto(id, displayName), EmailAddress.Normalize(email));
        return this;
    }

    public Task<UserRefDto?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.TryGetValue(userId, out var u) ? u.User : null);

    public Task<string?> GetNormalizedEmailAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.TryGetValue(userId, out var u) ? u.NormalizedEmail : null);
}

internal sealed class FakeWorkspaceRepository : IWorkspaceRepository
{
    public List<Workspace> Added { get; } = [];

    public int SaveCount { get; private set; }

    public (Guid UserId, PageRequest Page)? LastListCall { get; private set; }

    public (Guid WorkspaceId, Guid UserId, bool IncludeEmails)? LastDetailsCall { get; private set; }

    public WorkspaceDetailsDto? DetailsToReturn { get; set; }

    public HashSet<string> MemberEmails { get; } = [];

    public void Add(Workspace workspace) => Added.Add(workspace);

    public Task<Workspace?> GetAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        Task.FromResult(Added.SingleOrDefault(w => w.Id == workspaceId));

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task<PagedResult<WorkspaceSummaryDto>> ListForMemberAsync(Guid userId, PageRequest page, CancellationToken cancellationToken)
    {
        LastListCall = (userId, page);
        return Task.FromResult(new PagedResult<WorkspaceSummaryDto>([], page.Page, page.PageSize, 0));
    }

    public Task<WorkspaceDetailsDto?> GetDetailsForMemberAsync(Guid workspaceId, Guid userId, bool includeEmails, CancellationToken cancellationToken)
    {
        LastDetailsCall = (workspaceId, userId, includeEmails);
        if (DetailsToReturn is not null)
        {
            return Task.FromResult<WorkspaceDetailsDto?>(DetailsToReturn);
        }

        var w = Added.SingleOrDefault(x => x.Id == workspaceId);
        var member = w?.Members.SingleOrDefault(m => m.UserId == userId);
        return Task.FromResult(w is null || member is null
            ? null
            : new WorkspaceDetailsDto(
                w.Id, w.Name, w.Description, new UserRefDto(w.OwnerId, "Owner"), w.CreatedAt, w.UpdatedAt,
                w.Members.Select(m => new MemberDto(m.UserId, "Member", m.Role, m.JoinedAt)).ToList(),
                member.Role));
    }

    public Task<MemberDto?> GetMemberAsync(Guid workspaceId, Guid userId, bool includeEmail, CancellationToken cancellationToken)
    {
        var m = Added.SingleOrDefault(x => x.Id == workspaceId)?.Members.SingleOrDefault(x => x.UserId == userId);
        return Task.FromResult(m is null ? null : new MemberDto(m.UserId, "Member", m.Role, m.JoinedAt));
    }

    public Task<bool> IsMemberByEmailAsync(Guid workspaceId, string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(MemberEmails.Contains(normalizedEmail));
}

internal sealed class FakeInvitationRepository : IInvitationRepository
{
    public List<Invitation> Items { get; } = [];

    public bool SimulateRace { get; set; }

    public Task<bool> TryAddAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        if (SimulateRace)
        {
            return Task.FromResult(false);
        }

        Items.Add(invitation);
        return Task.FromResult(true);
    }

    public Task<Invitation?> FindAsync(Guid invitationId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.Id == invitationId));

    public Task ExpireLapsedAsync(Guid workspaceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var i in Items.Where(i => i.WorkspaceId == workspaceId))
        {
            i.IsOpen(now);
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasPendingAsync(Guid workspaceId, string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(i => i.WorkspaceId == workspaceId && i.NormalizedEmail == normalizedEmail && i.Status == InvitationStatus.Pending));

    public Task<int> CountPendingAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(i => i.WorkspaceId == workspaceId && i.Status == InvitationStatus.Pending));

    public Task<IReadOnlyList<WorkspaceInvitationDto>> ListOpenForWorkspaceAsync(Guid workspaceId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorkspaceInvitationDto>>([]);

    public Task<IReadOnlyList<ReceivedInvitationDto>> ListOpenForEmailAsync(string normalizedEmail, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReceivedInvitationDto>>([]);
}

internal sealed class FakeUserAccounts : IUserAccounts
{
    public Dictionary<string, (UserProfileDto Profile, string Password)> ByEmail { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int VerifyCalls { get; private set; }

    public List<string> CreatedPasswords { get; } = [];

    public UserProfileDto Add(string email, string password, string name = "Person")
    {
        var profile = new UserProfileDto(Guid.CreateVersion7(), name, email, DateTimeOffset.UnixEpoch);
        ByEmail[email] = (profile, password);
        return profile;
    }

    public Task<Outcome<UserProfileDto>> CreateAsync(string displayName, string email, string password, CancellationToken cancellationToken)
    {
        if (ByEmail.ContainsKey(email))
        {
            return Task.FromResult(Outcome<UserProfileDto>.Fail(ErrorCodes.EmailTaken));
        }

        CreatedPasswords.Add(password);
        return Task.FromResult(Outcome<UserProfileDto>.Ok(Add(email, password, displayName)));
    }

    public Task<UserProfileDto?> VerifyAsync(string email, string password, CancellationToken cancellationToken)
    {
        VerifyCalls++;
        return Task.FromResult(ByEmail.TryGetValue(email, out var u) && u.Password == password ? u.Profile : null);
    }

    public Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(ByEmail.Values.Select(v => v.Profile).SingleOrDefault(p => p.Id == userId));
}

internal sealed class FakeSessionRepository : ISessionRepository
{
    public List<Session> Sessions { get; } = [];

    public List<RefreshToken> Tokens { get; } = [];

    public int SaveCount { get; private set; }

    public bool RotationLosesRace { get; set; }

    public void Add(Session session, RefreshToken token)
    {
        Sessions.Add(session);
        Tokens.Add(token);
    }

    public Task<(Session Session, RefreshToken Token)?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        var token = Tokens.SingleOrDefault(t => t.TokenHash.SequenceEqual(tokenHash));
        return Task.FromResult<(Session, RefreshToken)?>(token is null ? null : (Sessions.Single(s => s.Id == token.SessionId), token));
    }

    public Task<Session?> FindAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(Sessions.SingleOrDefault(s => s.Id == sessionId));

    public Task<bool> TryRotateAsync(Session session, RefreshToken presented, RefreshToken next, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (RotationLosesRace)
        {
            return Task.FromResult(false);
        }

        presented.MarkUsed(now);
        Tokens.Add(next);
        return Task.FromResult(true);
    }

    public Task<bool> IsActiveAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(Sessions.Any(s => s.Id == sessionId && s.UserId == userId && s.IsActive(now)));

    public Task<int> RevokeAllForUserAsync(Guid userId, DateTimeOffset now, SessionRevocationReason reason, CancellationToken cancellationToken)
    {
        var active = Sessions.Where(s => s.UserId == userId && s.RevokedAt is null).ToList();
        active.ForEach(s => s.Revoke(now, reason));
        return Task.FromResult(active.Count);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAccessTokenIssuer(TimeProvider time) : IAccessTokenIssuer
{
    public AccessToken Issue(Guid userId, Guid sessionId) =>
        new($"access:{userId}:{sessionId}", time.GetUtcNow().AddMinutes(15));
}

internal sealed class FakeLoginLimiter : ILoginAttemptLimiter
{
    public HashSet<string> Blocked { get; } = [];

    public List<string> Failures { get; } = [];

    public List<string> Resets { get; } = [];

    public bool IsBlocked(string normalizedEmail) => Blocked.Contains(normalizedEmail);

    public void RecordFailure(string normalizedEmail) => Failures.Add(normalizedEmail);

    public void Reset(string normalizedEmail) => Resets.Add(normalizedEmail);
}

/// <summary>In-memory project files: a list of items per workspace and (text, revision) per file.</summary>
internal sealed class FakeProjectFileRepository : IProjectFileRepository
{
    public Dictionary<Guid, List<WorkspaceItem>> Items { get; } = [];

    public Dictionary<Guid, (string Content, long Revision, Guid? UpdatedBy)> Contents { get; } = [];

    public int Commits { get; private set; }

    /// <summary>Simulates the database's sibling-name backstop (a race lost to another request).</summary>
    public bool FailNextCommitWithNameClash { get; set; }

    public Task<ITreeEdit> BeginTreeEditAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var items = Items.TryGetValue(workspaceId, out var list) ? list : Items[workspaceId] = [];
        return Task.FromResult<ITreeEdit>(new Edit(this, workspaceId, new ProjectTree(workspaceId, items)));
    }

    public Task<IReadOnlyList<ItemDto>> GetTreeAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ItemDto>>(
            Items.TryGetValue(workspaceId, out var list) ? list.Select(ItemDto.From).ToList() : []);

    public Task<FileContentDto?> GetContentAsync(Guid workspaceId, Guid itemId, CancellationToken cancellationToken) =>
        Task.FromResult(
            Find(workspaceId, itemId) is { Kind: ItemKind.File } && Contents.TryGetValue(itemId, out var c)
                ? new FileContentDto(c.Content, c.Revision)
                : null);

    public Task<SaveContentResult> TrySaveContentAsync(
        Guid workspaceId, Guid itemId, long expectedRevision, string content, int sizeBytes,
        Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (Find(workspaceId, itemId) is not { Kind: ItemKind.File } || !Contents.TryGetValue(itemId, out var current))
        {
            return Task.FromResult(new SaveContentResult(SaveContentStatus.NotFound));
        }

        if (current.Revision != expectedRevision)
        {
            return Task.FromResult(new SaveContentResult(SaveContentStatus.Conflict, current.Revision));
        }

        Contents[itemId] = (content, current.Revision + 1, userId);
        return Task.FromResult(new SaveContentResult(SaveContentStatus.Saved, current.Revision + 1, now));
    }

    private WorkspaceItem? Find(Guid workspaceId, Guid itemId) =>
        Items.TryGetValue(workspaceId, out var list) ? list.SingleOrDefault(i => i.Id == itemId) : null;

    private sealed class Edit(FakeProjectFileRepository owner, Guid workspaceId, ProjectTree tree) : ITreeEdit
    {
        private readonly List<(WorkspaceItem Item, FileContent? Content)> _added = [];
        private readonly List<WorkspaceItem> _removed = [];

        public ProjectTree Tree { get; } = tree;

        public void Add(WorkspaceItem item, FileContent? content) => _added.Add((item, content));

        public void Remove(WorkspaceItem item) => _removed.Add(item);

        public Task<bool> CommitAsync(CancellationToken cancellationToken)
        {
            if (owner.FailNextCommitWithNameClash)
            {
                owner.FailNextCommitWithNameClash = false;
                return Task.FromResult(false);
            }

            var list = owner.Items[workspaceId];
            foreach (var (item, content) in _added)
            {
                list.Add(item);
                if (content is not null)
                {
                    owner.Contents[item.Id] = (content.Content, content.Revision, content.UpdatedByUserId);
                }
            }

            // Mirrors the database cascade: the tree already dropped the subtree, so keep only what it still holds.
            if (_removed.Count > 0)
            {
                var kept = Tree.Items.Select(i => i.Id).ToHashSet();
                foreach (var gone in list.Where(i => !kept.Contains(i.Id)).ToList())
                {
                    list.Remove(gone);
                    owner.Contents.Remove(gone.Id);
                }
            }

            owner.Commits++;
            return Task.FromResult(true);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
