using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DevSync.Api.Auth;
using DevSync.Api.Endpoints;
using DevSync.Api.Errors;
using DevSync.Api.Middleware;
using DevSync.Api.Realtime;
using DevSync.Application;
using DevSync.Application.Common;
using DevSync.Infrastructure;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "O";
});

builder.Services.Configure<JsonOptions>(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
// Binding failures (malformed JSON, wrong types) throw so they become 400 malformed_request Problem Details.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.AddDevSyncProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddDevSyncAuth();
builder.Services.AddSingleton<RefreshCookie>();
builder.Services.AddDevSyncRealtime();
builder.Services.AddHealthChecks().AddDbContextCheck<DevSyncDbContext>(tags: ["ready"]);

// Per-client limit on register/login (spec 002 FR-008). In-process state: single API instance.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(AuthEndpoints.IpRateLimitPolicy, context =>
    {
        var limit = context.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.IpRateLimitPerMinute;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await Problems.Error(ErrorCodes.TooManyAttempts).ExecuteAsync(context.HttpContext);
    };
});

var app = builder.Build();

// Fail fast: refuse to start without a signing key outside Development (NFR-004).
_ = app.Services.GetRequiredService<SigningKeyProvider>();

if (app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.ApplyMigrationsOnStartup)
{
    await MigrationRunner.ApplyAsync(app.Services);
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.MapAuthEndpoints();
app.MapWorkspaceEndpoints();
app.MapInvitationEndpoints();
app.MapFileEndpoints();
app.MapDiagramEndpoints();
app.MapDevSyncHubs();

await app.RunAsync();

/// <summary>Entry point; public so integration tests can use WebApplicationFactory.</summary>
public partial class Program;
