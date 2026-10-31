using DevSync.Application.Common;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DevSync.Api.Errors;

/// <summary>Maps unhandled exceptions to Problem Details without exposing exception details.</summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = Classify(exception);

        if (status >= 500)
        {
            logger.LogError(exception, "Request failed with {Code}", code);
        }
        else
        {
            logger.LogInformation("Rejected malformed request: {Reason}", exception.Message);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { ["code"] = code },
            },
        });
    }

    private static (int Status, string Code, string Title) Classify(Exception exception) => exception switch
    {
        BadHttpRequestException bad => (bad.StatusCode, ErrorCodes.MalformedRequest, "The request is malformed."),
        _ when DatabaseErrors.IsUnavailable(exception) =>
            (StatusCodes.Status503ServiceUnavailable, ErrorCodes.ServiceUnavailable, "The database is unavailable."),
        _ => (StatusCodes.Status500InternalServerError, ErrorCodes.InternalError, "An unexpected error occurred."),
    };
}
