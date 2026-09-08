using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Configurations;

namespace WebApi.Tests;

public sealed class GlobalExceptionHandlerTests
{
    [Test]
    public async Task UnhandledException_ReturnsGenericInternalServerErrorProblem()
    {
        var context = new DefaultHttpContext();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        context.RequestServices = services;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var handler = new ExceptionHandlingConfiguration.GlobalExceptionHandler(
            NullLogger<ExceptionHandlingConfiguration.GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("Sensitive exception detail"),
            CancellationToken.None);

        await Assert.That(handled).IsTrue();
        await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);
        await Assert.That(context.Response.ContentType).IsEqualTo("application/problem+json");

        responseBody.Position = 0;
        using var problem = await JsonDocument.ParseAsync(responseBody);
        await Assert.That(problem.RootElement.GetProperty("title").GetString()).IsEqualTo("Unhandled exception");
        await Assert.That(problem.RootElement.GetProperty("detail").GetString()).IsEqualTo("An unexpected error occurred.");
        await Assert.That(problem.RootElement.GetProperty("status").GetInt32()).IsEqualTo((int)HttpStatusCode.InternalServerError);
        await Assert.That(problem.RootElement.GetRawText()).DoesNotContain("Sensitive exception detail");
    }
}
