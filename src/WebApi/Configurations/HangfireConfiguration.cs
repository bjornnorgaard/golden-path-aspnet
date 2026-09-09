using Hangfire;
using Hangfire.PostgreSql;
using WebApi.Features.Todos.SweepOverdueTodos;

namespace WebApi.Configurations;

public static class HangfireConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddPlatformHangfire()
        {
            var cs = builder.Configuration.GetConnectionStrings().DefaultConnection;

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
        /// <summary>
        /// Applies Hangfire's runtime pieces based on configuration: the dashboard is mapped only
        /// when <c>Hangfire:DashboardEnabled</c> is true, and the recurring-job example is always
        /// registered (idempotently via AddOrUpdate) since it has no configuration toggle yet.
        /// </summary>
        public void UsePlatformHangfire()
        {
            var hangfireOptions = app.Configuration.GetHangfire();

            if (hangfireOptions.DashboardEnabled)
            {
                // No authorization filter: this is a reference template running behind trusted
                // networking, not a hardened default. Add a real IDashboardAuthorizationFilter
                // before exposing this publicly.
                app.MapHangfireDashboard(new DashboardOptions
                {
                    Authorization = []
                });
            }

            // Recurring job example: sweeps for todos that became overdue every minute.
            // Registration is idempotent (AddOrUpdate), so it's safe to call on every app start.
            RecurringJob.AddOrUpdate<SweepOverdueTodosJob>(
                recurringJobId: "sweep-overdue-todos",
                methodCall: job => job.InvokeAsync(CancellationToken.None),
                cronExpression: Cron.Minutely);
        }
    }
}

