using System.Text.RegularExpressions;

namespace DevSync.Api.Middleware;

/// <summary>
/// Accepts a well-formed inbound X-Correlation-ID or generates one, echoes it in the response,
/// and adds it to the logging scope for the rest of the request.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "DevSync.CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var inbound = context.Request.Headers[HeaderName].ToString();
        var correlationId = ValidId().IsMatch(inbound) ? inbound : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["correlationId"] = correlationId }))
        {
            await next(context);
        }
    }

    public static string Get(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string id ? id : string.Empty;

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex ValidId();
}
