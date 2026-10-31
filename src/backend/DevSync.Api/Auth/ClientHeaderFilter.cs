using DevSync.Api.Errors;
using DevSync.Application.Common;
using Microsoft.Extensions.Options;

namespace DevSync.Api.Auth;

/// <summary>
/// CSRF defence for the cookie-authenticated <c>/api/auth/*</c> endpoints (spec 002 FR-016, research R7):
/// requires <c>X-DevSync-Client: web</c> (cross-site forms cannot set it and cross-origin scripts would
/// need a CORS preflight the API never grants) and, when present, a same or allowed <c>Origin</c>.
/// </summary>
public sealed class ClientHeaderFilter(IOptions<AuthOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-DevSync-Client";
    public const string HeaderValue = "web";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!string.Equals(request.Headers[HeaderName].ToString(), HeaderValue, StringComparison.Ordinal))
        {
            return Problems.Error(ErrorCodes.CsrfCheckFailed);
        }

        var origin = request.Headers.Origin.ToString();
        if (origin.Length > 0)
        {
            var self = $"{request.Scheme}://{request.Host}";
            var allowed = string.Equals(origin, self, StringComparison.OrdinalIgnoreCase)
                || options.Value.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
            if (!allowed)
            {
                return Problems.Error(ErrorCodes.CsrfCheckFailed);
            }
        }

        return await next(context);
    }
}
