using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Configurations;

public static class ExceptionHandlingConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformExceptionHandling()
        {
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<PlatformExceptionHandler>();
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatformExceptionHandling()
        {
            app.UseExceptionHandler();
        }
    }

    private sealed class PlatformExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception ex, CancellationToken ct)
        {
            Activity.Current?.AddException(ex);
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);

            var problem = Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unhandled exception",
                detail: "An unexpected error occurred.");

            await problem.ExecuteAsync(httpContext);
            return true;
        }
    }
}