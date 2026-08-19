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
