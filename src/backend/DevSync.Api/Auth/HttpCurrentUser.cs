using DevSync.Application.Common;

namespace DevSync.Api.Auth;

/// <summary>Reads the authenticated user id (<c>sub</c>) from the current request's principal.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirst(AccessTokenIssuer.UserIdClaim)?.Value;
            return Guid.TryParse(value, out var id)
                ? id
                : throw new InvalidOperationException("No authenticated user on the current request.");
        }
    }
}
