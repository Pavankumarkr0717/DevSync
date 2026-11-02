namespace DevSync.Api.Realtime;

public static class RealtimeSetup
{
    /// <summary>The workspace hub's path; under /api so the existing same-origin proxies carry it.</summary>
    public const string WorkspaceHubPath = "/api/hubs/workspace";

    /// <summary>The path prefix on which an access token may arrive in the query string (research R2).</summary>
    public const string HubsPathPrefix = "/api/hubs";

    /// <summary>SignalR for workspace presence (spec 005): in-process, JSON, small messages, no error details.</summary>
    public static IServiceCollection AddDevSyncRealtime(this IServiceCollection services)
    {
        services.AddOptions<PresenceOptions>().BindConfiguration(PresenceOptions.SectionName);
        services.AddSingleton<ConnectionRegistry>();
        services.AddSingleton<PresenceBroadcaster>();
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false;
            options.MaximumReceiveMessageSize = 4 * 1024;
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        });
        return services;
    }

    public static void MapDevSyncHubs(this IEndpointRouteBuilder app) =>
        app.MapHub<WorkspaceHub>(WorkspaceHubPath, options =>
        {
            // Backstop on the system clock; ConnectionRegistry closes connections at token expiry on
            // the app's TimeProvider (FR-004).
            options.CloseOnAuthenticationExpiration = true;
        });
}
