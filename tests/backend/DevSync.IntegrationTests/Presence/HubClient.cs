using System.Text.Json;
using System.Threading.Channels;
using DevSync.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace DevSync.IntegrationTests.Presence;

/// <summary>
/// A real SignalR client for the workspace hub, connected to the in-memory test server over long
/// polling. Every server event is recorded as raw JSON so tests can assert exact payloads.
/// </summary>
public sealed class HubClient : IAsyncDisposable
{
    public const string HubPath = "/api/hubs/workspace";

    private static readonly string[] Events = ["OnlineUsersUpdated", "UserJoined", "UserLeft", "AccessRevoked"];

    private readonly Channel<(string Name, JsonElement Payload)> _events = Channel.CreateUnbounded<(string, JsonElement)>();
    private readonly TaskCompletionSource<Exception?> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private HubClient(HubConnection connection)
    {
        Connection = connection;
        foreach (var name in Events)
        {
            connection.On<JsonElement>(name, payload => _events.Writer.TryWrite((name, payload)));
        }

        connection.Closed += error =>
        {
            _closed.TrySetResult(error);
            return Task.CompletedTask;
        };
    }

    public HubConnection Connection { get; }

    /// <summary>Completes when the server (or the client) closes the connection.</summary>
    public Task<Exception?> Closed => _closed.Task;

    public static HubConnection Build(DevSyncApiFactory factory, string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, HubPath), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (accessToken is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                }
            })
            .Build();

    public static async Task<HubClient> ConnectAsync(DevSyncApiFactory factory, string accessToken)
    {
        var client = new HubClient(Build(factory, accessToken));
        await client.Connection.StartAsync();
        return client;
    }

    public static async Task<HubClient> ConnectAsync(DevSyncApiFactory factory, Guid userId) =>
        await ConnectAsync(factory, (await factory.StartSessionAsync(userId)).AccessToken);

    public Task<JsonElement> JoinAsync(Guid workspaceId) => JoinAsync(workspaceId.ToString());

    public Task<JsonElement> JoinAsync(string? workspaceId, int v = 1) =>
        Connection.InvokeAsync<JsonElement>("JoinWorkspace", new { v, workspaceId });

    public Task LeaveAsync() => Connection.InvokeAsync("LeaveWorkspace");

    /// <summary>The next event with this name; earlier events of other names are skipped.</summary>
    public async Task<JsonElement> NextAsync(string name, TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
        while (true)
        {
            var (eventName, payload) = await _events.Reader.ReadAsync(cts.Token);
            if (eventName == name)
            {
                return payload;
            }
        }
    }

    /// <summary>Every event received within <paramref name="window"/>.</summary>
    public async Task<IReadOnlyList<(string Name, JsonElement Payload)>> DrainAsync(TimeSpan window)
    {
        await Task.Delay(window);
        var events = new List<(string, JsonElement)>();
        while (_events.Reader.TryRead(out var item))
        {
            events.Add(item);
        }

        return events;
    }

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}

public static class PresenceJson
{
    public static IReadOnlyList<string> Names(this JsonElement snapshot) =>
        snapshot.GetProperty("users").EnumerateArray().Select(u => u.GetProperty("displayName").GetString()!).ToArray();

    public static long Version(this JsonElement payload) => payload.GetProperty("version").GetInt64();

    public static string UserName(this JsonElement payload) => payload.GetProperty("user").GetProperty("displayName").GetString()!;
}
