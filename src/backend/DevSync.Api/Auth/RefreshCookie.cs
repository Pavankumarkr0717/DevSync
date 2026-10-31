using Microsoft.Extensions.Options;

namespace DevSync.Api.Auth;

/// <summary>
/// The refresh-token cookie: HttpOnly (unreadable by scripts, FR-015), SameSite=Strict, scoped to
/// /api/auth so it is only ever sent to the auth endpoints, Secure unless configured off for local HTTP.
/// </summary>
public sealed class RefreshCookie(IOptions<AuthOptions> options, TimeProvider time)
{
    public const string Name = "devsync_rt";
    public const string CookiePath = "/api/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public void Append(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, Options(maxAge: expiresAt - time.GetUtcNow()));

    public void Clear(HttpResponse response) =>
        response.Cookies.Append(Name, string.Empty, Options(maxAge: TimeSpan.Zero));

    private CookieOptions Options(TimeSpan maxAge) => new()
    {
        HttpOnly = true,
        Secure = options.Value.RefreshCookie.Secure,
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        MaxAge = maxAge < TimeSpan.Zero ? TimeSpan.Zero : maxAge,
        IsEssential = true,
    };
}
