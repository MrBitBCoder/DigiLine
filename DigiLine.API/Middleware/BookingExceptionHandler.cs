using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DigiLine.Application.Common;

namespace DigiLine.API.Middleware
{
    public sealed class BookingExceptionHandler
       : IExceptionHandler
    {
        private readonly ILogger<BookingExceptionHandler> _logger;

        public BookingExceptionHandler(
            ILogger<BookingExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var path = httpContext.Request.Path;
            if (exception is DigiLine.Application.Sessions.SessionCompletionException completion)
            {
                httpContext.Response.StatusCode = 409;
                var problem = new ProblemDetails { Status = 409, Title = "Session is not ready to complete", Detail = completion.Message };
                problem.Extensions["counts"] = completion.Counts;
                await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken); return true;
            }
            if (exception is ApplicationConflictException conflict)
            {
                await WriteProblem(httpContext, StatusCodes.Status409Conflict, "The request conflicts with the current state.", conflict.Message, cancellationToken);
                return true;
            }
            if (exception is UnauthorizedAccessException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Patient access is invalid or expired.",
                    Detail = exception.Message,
                    Instance = httpContext.Request.Path
                }, cancellationToken);
                return true;
            }

            if (exception is ArgumentException or KeyNotFoundException or TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                httpContext.Response.StatusCode = exception is KeyNotFoundException ? 404 : 400;
                await WriteProblem(httpContext, httpContext.Response.StatusCode,
                    exception is KeyNotFoundException ? "The requested record was not found." : "The request is invalid.", exception.Message, cancellationToken);
                return true;
            }
            if (exception is InvalidOperationException invalidOperation)
            {
                _logger.LogWarning(exception, "Request conflict for {Path}.", path);
                await WriteProblem(httpContext, StatusCodes.Status409Conflict,
                    "The action conflicts with the current state.", invalidOperation.Message, cancellationToken);
                return true;
            }

            _logger.LogError(exception, "Unhandled request failure for {Path}.", path);
            await WriteProblem(httpContext, StatusCodes.Status500InternalServerError,
                "The request could not be completed.", "An unexpected error occurred. Please try again.", cancellationToken);
            return true;
        }

        private static async Task WriteProblem(HttpContext context, int status, string title, string detail, CancellationToken cancellationToken)
        {
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            }, cancellationToken);
        }
    }
}
