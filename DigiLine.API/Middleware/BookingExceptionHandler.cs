using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

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
            if (exception is DigiLine.Application.Sessions.SessionCompletionException completion)
            {
                httpContext.Response.StatusCode = 409;
                var problem = new ProblemDetails { Status = 409, Title = "Session is not ready to complete", Detail = completion.Message };
                problem.Extensions["counts"] = completion.Counts;
                await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken); return true;
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
                await httpContext.Response.WriteAsJsonAsync(new ProblemDetails { Status = httpContext.Response.StatusCode, Title = "Request rejected", Detail = exception.Message }, cancellationToken);
                return true;
            }
            if (exception is not InvalidOperationException)
            {
                return false;
            }

            _logger.LogWarning(
                exception,
                "Booking request was rejected.");

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Booking request was rejected.",
                Detail = exception.Message,
                Instance = httpContext.Request.Path
            };

            httpContext.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                cancellationToken);

            return true;
        }
    }
}
