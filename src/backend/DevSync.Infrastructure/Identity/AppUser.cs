using Microsoft.AspNetCore.Identity;

namespace DevSync.Infrastructure.Identity;

/// <summary>
/// A DevSync account, stored by ASP.NET Core Identity in the <c>users</c> table (spec 002 R1).
/// The domain refers to users only by id.
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 100;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
