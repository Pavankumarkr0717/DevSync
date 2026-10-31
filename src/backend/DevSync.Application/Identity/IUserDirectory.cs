namespace DevSync.Application.Identity;

/// <summary>Read access to known users.</summary>
public interface IUserDirectory
{
    Task<UserRefDto?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The account's normalized email (see <c>EmailAddress.Normalize</c>), or null if the user does not exist.</summary>
    Task<string?> GetNormalizedEmailAsync(Guid userId, CancellationToken cancellationToken);
}
