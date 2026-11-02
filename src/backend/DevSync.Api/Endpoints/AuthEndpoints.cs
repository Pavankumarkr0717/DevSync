using DevSync.Api.Auth;
using DevSync.Api.Errors;
using DevSync.Api.Realtime;
using DevSync.Application.Auth;
using DevSync.Application.Common;

namespace DevSync.Api.Endpoints;

public sealed record RegisterRequest(string? DisplayName, string? Email, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

/// <summary>Registration, sign-in, renewal and sign-out (spec 002 US1, US2).</summary>
public static class AuthEndpoints
{
    public const string IpRateLimitPolicy = "auth-ip";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var anonymous = app.MapGroup("/api/auth")
            .AllowAnonymous()
            .AddEndpointFilter<ClientHeaderFilter>();

        anonymous.MapPost("/register", RegisterAsync).RequireRateLimiting(IpRateLimitPolicy);
        anonymous.MapPost("/login", LoginAsync).RequireRateLimiting(IpRateLimitPolicy);
        anonymous.MapPost("/refresh", RefreshAsync);
        anonymous.MapPost("/logout", LogoutAsync);

        // Requires a valid access token (fallback policy).
        app.MapPost("/api/auth/logout-all", LogoutAllAsync).AddEndpointFilter<ClientHeaderFilter>();

        app.MapGet("/api/me", async (GetProfileHandler handler, CancellationToken ct) =>
            await handler.HandleAsync(ct) is { } profile ? Results.Ok(profile) : Problems.Error(ErrorCodes.SessionEnded));

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, RegisterHandler handler, RefreshCookie cookie, HttpResponse response, CancellationToken ct)
    {
        var outcome = await handler.HandleAsync(new RegisterCommand(request.DisplayName, request.Email, request.Password), ct);
        if (!outcome.IsSuccess)
        {
            return Problems.From(outcome);
        }

        cookie.Append(response, outcome.Value.RefreshToken, outcome.Value.RefreshTokenExpiresAt);
        return TypedResults.Created("/api/me", outcome.Value.Session);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, LoginHandler handler, RefreshCookie cookie, HttpResponse response, CancellationToken ct)
    {
        var outcome = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), ct);
        if (!outcome.IsSuccess)
        {
            return Problems.From(outcome);
        }

        cookie.Append(response, outcome.Value.RefreshToken, outcome.Value.RefreshTokenExpiresAt);
        return TypedResults.Ok(outcome.Value.Session);
    }

    private static async Task<IResult> RefreshAsync(
        HttpRequest request, HttpResponse response, RefreshHandler handler, RefreshCookie cookie, CancellationToken ct)
    {
        var outcome = await handler.HandleAsync(RefreshCookie.Read(request), ct);
        if (outcome.IsSuccess)
        {
            cookie.Append(response, outcome.Value.RefreshToken, outcome.Value.RefreshTokenExpiresAt);
            return TypedResults.Ok(outcome.Value.Session);
        }

        if (outcome.Error == ErrorCodes.SessionEnded)
        {
            cookie.Clear(response);
        }

        return Problems.From(outcome);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context, LogoutHandler handler, RefreshCookie cookie, PresenceBroadcaster presence, CancellationToken ct)
    {
        Guid? sessionId = Guid.TryParse(context.User.FindFirst(AccessTokenIssuer.SessionIdClaim)?.Value, out var sid) ? sid : null;
        if (await handler.HandleAsync(RefreshCookie.Read(context.Request), sessionId, ct) is { } ended)
        {
            presence.EndSession(ended);
        }

        cookie.Clear(context.Response);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> LogoutAllAsync(
        HttpResponse response, LogoutAllHandler handler, RefreshCookie cookie, ICurrentUser user,
        PresenceBroadcaster presence, CancellationToken ct)
    {
        await handler.HandleAsync(ct);
        presence.EndAllSessions(user.UserId);
        cookie.Clear(response);
        return TypedResults.NoContent();
    }
}
