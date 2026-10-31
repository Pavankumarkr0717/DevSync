using DevSync.Api.Middleware;

namespace DevSync.Api.Errors;

public static class ProblemDetailsSetup
{
    /// <summary>Every Problem Details response carries <c>code</c> and <c>correlationId</c>.</summary>
    public static IServiceCollection AddDevSyncProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            var status = problem.Status ?? context.HttpContext.Response.StatusCode;
            problem.Extensions.TryAdd("code", ErrorCatalog.CodeForStatus(status));
            problem.Extensions["correlationId"] = CorrelationIdMiddleware.Get(context.HttpContext);
            problem.Extensions.Remove("traceId");
        });
}
