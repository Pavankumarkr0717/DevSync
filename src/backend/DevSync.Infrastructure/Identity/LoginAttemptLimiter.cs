using DevSync.Application.Auth;
using Microsoft.Extensions.Caching.Memory;

namespace DevSync.Infrastructure.Identity;

/// <summary>
/// At most 5 failed sign-ins per normalized email within 15 minutes, then refuse that email for
/// 15 minutes (spec 002 FR-008). Applies identically to emails with and without accounts, so it
/// reveals nothing. State is in-process: correct for the single-instance modular monolith (research R6).
/// </summary>
internal sealed class LoginAttemptLimiter(IMemoryCache cache, TimeProvider time) : ILoginAttemptLimiter
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(15);

    private readonly Lock _gate = new();

    public bool IsBlocked(string normalizedEmail)
    {
        lock (_gate)
        {
            return cache.TryGetValue(Key(normalizedEmail), out Entry? entry)
                && entry!.BlockedUntil is { } until
                && time.GetUtcNow() < until;
        }
    }

    public void RecordFailure(string normalizedEmail)
    {
        lock (_gate)
        {
            var now = time.GetUtcNow();
            if (!cache.TryGetValue(Key(normalizedEmail), out Entry? entry)
                || now - entry!.WindowStart >= Window
                || entry.BlockedUntil is { } until && now >= until)
            {
                entry = new Entry { WindowStart = now };
            }

            entry.Failures++;
            if (entry.Failures >= MaxFailures)
            {
                entry.BlockedUntil = now + BlockDuration;
            }

            cache.Set(Key(normalizedEmail), entry, Window + BlockDuration);
        }
    }

    public void Reset(string normalizedEmail)
    {
        lock (_gate)
        {
            cache.Remove(Key(normalizedEmail));
        }
    }

    private static string Key(string normalizedEmail) => "login-failures:" + normalizedEmail;

    private sealed class Entry
    {
        public DateTimeOffset WindowStart { get; init; }

        public int Failures { get; set; }

        public DateTimeOffset? BlockedUntil { get; set; }
    }
}
