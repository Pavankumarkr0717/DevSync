using System.Diagnostics;
using System.Security.Claims;
using DevSync.Api.Auth;

namespace DevSync.Api.Middleware;

/// <summary>Writes one structured log line per request. Never logs bodies or header values.</summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
            var userId = context.User.FindFirstValue(AccessTokenIssuer.UserIdClaim);
            logger.LogInformation(
                "HTTP {Method} {Route} responded {StatusCode} in {DurationMs:0.0} ms (correlationId {CorrelationId}, userId {UserId})",
                context.Request.Method,
                route,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                CorrelationIdMiddleware.Get(context),
                userId ?? "anonymous");
        }
    }
}
