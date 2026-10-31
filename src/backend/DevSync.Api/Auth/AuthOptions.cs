namespace DevSync.Api.Auth;

/// <summary>Bound from the <c>DevSync:Auth</c> configuration section (spec 002 plan, Configuration).</summary>
public sealed class AuthOptions
{
    public const string SectionName = "DevSync:Auth";

    /// <summary>Base64, at least 32 bytes. Unset in Development = temporary key; required elsewhere.</summary>
    public string? SigningKey { get; set; }

    public int AccessTokenMinutes { get; set; } = 15;

    public int SessionIdleDays { get; set; } = 7;

    public int SessionAbsoluteDays { get; set; } = 30;

    /// <summary>Per-client-IP limit for register and login requests.</summary>
    public int IpRateLimitPerMinute { get; set; } = 20;

    /// <summary>Extra origins allowed to call /api/auth/*; the request's own origin is always allowed.</summary>
    public string[] AllowedOrigins { get; set; } = [];

    public RefreshCookieOptions RefreshCookie { get; set; } = new();

    public sealed class RefreshCookieOptions
    {
        public bool Secure { get; set; } = true;
    }
}
