using DevSync.Application.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DevSync.Api.Auth;

/// <summary>
/// Issues short-lived HS256 access tokens carrying only <c>sub</c> (user) and <c>sid</c> (session).
/// Roles are never embedded: authorization reads current membership per request (FR-025).
/// </summary>
public sealed class AccessTokenIssuer(SigningKeyProvider keys, IOptions<AuthOptions> options, TimeProvider time) : IAccessTokenIssuer
{
    public const string Issuer = "devsync";
    public const string Audience = "devsync";
    public const string UserIdClaim = "sub";
    public const string SessionIdClaim = "sid";

    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    public AccessToken Issue(Guid userId, Guid sessionId)
    {
        var now = time.GetUtcNow();
        var expires = now.AddMinutes(options.Value.AccessTokenMinutes);
        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [UserIdClaim] = userId.ToString(),
                [SessionIdClaim] = sessionId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(keys.Key, SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }
}
