using DevSync.Api.Errors;
using DevSync.Application.Auth;
using DevSync.Application.Common;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DevSync.Api.Auth;

public static class AuthSetup
{
    private const string AuthErrorItem = "DevSync.AuthError";

    /// <summary>
    /// JWT bearer authentication with a per-request session check (immediate revocation, FR-017),
    /// a fallback policy that protects every endpoint by default (FR-026), and workspace authorization.
    /// </summary>
    public static IServiceCollection AddDevSyncAuth(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName);
        services.AddSingleton<SigningKeyProvider>();
        services.AddSingleton<IAccessTokenIssuer, AccessTokenIssuer>();
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AuthOptions>>().Value;
            return new SessionSettings(TimeSpan.FromDays(options.SessionIdleDays), TimeSpan.FromDays(options.SessionAbsoluteDays));
        });
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<SigningKeyProvider, TimeProvider>((options, keys, time) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = AccessTokenIssuer.Issuer,
                    ValidAudience = AccessTokenIssuer.Audience,
                    IssuerSigningKey = keys.Key,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = AccessTokenIssuer.UserIdClaim,
                    // Lifetime is checked against the app's TimeProvider so tests can control time.
                    LifetimeValidator = (notBefore, expires, _, _) =>
                    {
                        var now = time.GetUtcNow().UtcDateTime;
                        return expires is { } exp && now < exp && (notBefore is not { } nbf || now >= nbf);
                    },
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateSessionAsync,
                    OnAuthenticationFailed = context =>
                    {
                        // Let database outages surface as 503 instead of turning into 401.
                        if (!DatabaseErrors.IsUnavailable(context.Exception))
                        {
                            context.HttpContext.Items.TryAdd(AuthErrorItem,
                                context.Exception is SecurityTokenExpiredException or SecurityTokenInvalidLifetimeException
                                    ? ErrorCodes.TokenExpired
                                    : ErrorCodes.Unauthenticated);
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = WriteChallengeAsync,
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddSingleton<IAuthorizationHandler, WorkspaceOperationHandler>();

        return services;
    }

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst(AccessTokenIssuer.UserIdClaim)?.Value, out var userId)
            || !Guid.TryParse(principal?.FindFirst(AccessTokenIssuer.SessionIdClaim)?.Value, out var sessionId))
        {
            context.HttpContext.Items[AuthErrorItem] = ErrorCodes.Unauthenticated;
            context.Fail("Missing subject or session.");
            return;
        }

        var services = context.HttpContext.RequestServices;
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var active = await services.GetRequiredService<ISessionRepository>()
            .IsActiveAsync(sessionId, userId, now, context.HttpContext.RequestAborted);
        if (!active)
        {
            context.HttpContext.Items[AuthErrorItem] = ErrorCodes.SessionEnded;
            context.Fail("Session ended.");
        }
    }

    private static async Task WriteChallengeAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        var code = context.HttpContext.Items.TryGetValue(AuthErrorItem, out var value) && value is string c
            ? c
            : ErrorCodes.Unauthenticated;

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = ErrorCatalog.TitleFor(code),
                Extensions = { ["code"] = code },
            },
        });
    }
}
