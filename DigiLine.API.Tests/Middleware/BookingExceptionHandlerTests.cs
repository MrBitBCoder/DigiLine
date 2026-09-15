using System.Text.Json;
using DigiLine.API.Middleware;
using DigiLine.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigiLine.API.Tests;

public sealed class BookingExceptionHandlerTests
{
    [Theory]
    [InlineData(typeof(ArgumentException), 400)]
    [InlineData(typeof(KeyNotFoundException), 404)]
    [InlineData(typeof(InvalidOperationException), 409)]
    [InlineData(typeof(ApplicationConflictException), 409)]
    public async Task ExpectedErrors_ReturnSafeProblemDetails(Type exceptionType, int status)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Safe user message.")!;
        var context = Context();

        var handled = await new BookingExceptionHandler(NullLogger<BookingExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, default);

        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        var problem = await Read(context);
        Assert.Equal("Safe user message.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task UnexpectedError_ReturnsGenericProblemWithoutInternalDetails()
    {
        var context = Context();
        await new BookingExceptionHandler(NullLogger<BookingExceptionHandler>.Instance)
            .TryHandleAsync(context, new Exception("SQL constraint IX_Staff_NormalizedEmail failed"), default);

        Assert.Equal(500, context.Response.StatusCode);
        var json = (await Read(context)).ToString();
        Assert.Contains("An unexpected error occurred", json);
        Assert.DoesNotContain("SQL", json);
        Assert.DoesNotContain("constraint", json);
    }

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> Read(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return (await JsonDocument.ParseAsync(context.Response.Body)).RootElement.Clone();
    }
}
