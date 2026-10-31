using DevSync.Application.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Infrastructure.Identity;

internal sealed class UserDirectory(DevSyncDbContext db) : IUserDirectory
{
    public Task<UserRefDto?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserRefDto(u.Id, u.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<string?> GetNormalizedEmailAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.NormalizedEmail)
            .SingleOrDefaultAsync(cancellationToken);
}
