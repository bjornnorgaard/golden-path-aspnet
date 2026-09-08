using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace WebApi.Configurations;

public static class ExceptionHandlingConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformExceptionHandling()
        {
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatformExceptionHandling()
        {
            app.UseExceptionHandler();
        }
    }

    public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception ex, CancellationToken ct)
        {
            logger.LogError(
                ex,
                "Unhandled exception while handling HTTP {RequestMethod} {RequestPath}",
                httpContext.Request.Method,
                httpContext.Request.Path);

            var activity = Activity.Current;
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, "Unhandled exception");
            activity?.SetTag("error.type", ex.GetType().FullName);

            var problem = Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unhandled exception",
                detail: "An unexpected error occurred.");

            await problem.ExecuteAsync(httpContext);
            return true;
        }
    }
}
