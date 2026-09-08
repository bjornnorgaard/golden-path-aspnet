using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;

namespace WebApi.Configurations;

public static class HangfireConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformHangfire()
        {
            var cs = builder.Configuration.GetConnectionString("DefaultConnection");

            builder.Services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(cs)));

            builder.Services.AddHangfireServer();
        }
    }

    extension(WebApplication app)
    {
        public void UsePlatformHangfireDashboard()
        {
            // No authorization filter: this is a reference template running behind trusted
            // networking, not a hardened default. Add a real IDashboardAuthorizationFilter
            // before exposing this publicly.
            app.MapHangfireDashboard(new DashboardOptions
            {
                Authorization = []
            });
        }
    }
}
