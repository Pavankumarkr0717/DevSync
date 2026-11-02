using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace DevSync.Api.Realtime;

/// <summary>
/// The live hub connections of this process, so the server can close them when their session ends
/// or their access token expires (spec 005 FR-004). Closing is enforced by the server; a modified
/// client cannot ignore it.
/// </summary>
public sealed class ConnectionRegistry(TimeProvider time, ILogger<ConnectionRegistry> logger)
{
    private readonly ConcurrentDictionary<string, Entry> _connections = new(StringComparer.Ordinal);

    /// <summary>Registers the connection and closes it at <paramref name="expiresAt"/> (the access token's expiry).</summary>
    public void Add(HubCallerContext context, DateTimeOffset expiresAt)
    {
        var entry = new Entry(context, new CancellationTokenSource());
        _connections[context.ConnectionId] = entry;
        _ = CloseAtExpiryAsync(entry, expiresAt);
    }

    public void Remove(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var entry))
        {
            entry.Expiry.Cancel();
            entry.Expiry.Dispose();
        }
    }

    /// <summary>Closes the connection; its normal disconnect handling then runs. Unknown ids are ignored.</summary>
    public void Abort(string connectionId)
    {
        if (_connections.TryGetValue(connectionId, out var entry))
        {
            entry.Context.Abort();
        }
    }

    private async Task CloseAtExpiryAsync(Entry entry, DateTimeOffset expiresAt)
    {
        try
        {
            var remaining = expiresAt - time.GetUtcNow();
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, time, entry.Expiry.Token);
            }

            logger.LogInformation(
                "Presence event {Event}: connection {ConnectionId} closed at access-token expiry",
                "presence.token_expired", entry.Context.ConnectionId);
            entry.Context.Abort();
        }
        catch (OperationCanceledException)
        {
            // The connection ended first.
        }
        catch (ObjectDisposedException)
        {
            // Removed while the delay was being set up.
        }
    }

    private sealed record Entry(HubCallerContext Context, CancellationTokenSource Expiry);
}
