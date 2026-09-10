using Hangfire;
using Hangfire.AspNetCore;
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
            var hangfireOptions = builder.Configuration.GetHangfire();

            builder.Services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(opts => opts.UseNpgsqlConnection(cs), new PostgreSqlStorageOptions
                {
                    // Storage is isolated per schema so independently started hosts (for example
                    // parallel integration-test hosts) don't pick up each other's jobs.
                    SchemaName = hangfireOptions.SchemaName
                }));

            builder.Services.AddHangfireServer();

            // Bind job activation to this host's container. Hangfire otherwise falls back to its
            // process-wide activator, which captures the first host configured in the process and
            // then resolves job targets (feature handlers and their loggers, DbContext, ...) from a
            // provider that may already be disposed.
            builder.Services.AddSingleton<JobActivator>(sp => new AspNetCoreJobActivator(sp.GetRequiredService<IServiceScopeFactory>()));
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
            RebindHangfireGlobals(app.Services);

            var hangfireOptions = app.Configuration.GetHangfire();

            if (hangfireOptions.DashboardEnabled)
            {
                // No Hangfire-specific authorization filter: the dashboard is mapped as a regular
                // endpoint, so the global fallback policy (see AuthenticationConfiguration) already
                // requires an authenticated, allow-listed GitHub login before this is reachable.
                app.MapHangfireDashboard(new DashboardOptions
                {
                    Authorization = []
                });
            }

            // Recurring job example: sweeps for todos that became overdue every minute.
            // Registration is idempotent (AddOrUpdate), so it's safe to call on every app start.
            //
            // The recurring job manager is resolved from this app's container rather than using the
            // static RecurringJob facade, which registers against Hangfire's process-wide globals
            // instead of this host's storage and activator.
            var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();

            recurringJobs.AddOrUpdate<SweepOverdueTodosHandler>(
                recurringJobId: "sweep-overdue-todos",
                methodCall: job => job.HandleAsync(CancellationToken.None),
                cronExpression: Cron.Minutely);
        }
    }

    /// <summary>
    /// Points Hangfire's process-wide logging global at a live host.
    /// <para>
    /// <c>AddHangfire</c> configures <see cref="GlobalConfiguration"/> the first time it runs in a
    /// process, capturing that host's <c>ILoggerFactory</c> in the log provider. That global is not
    /// refreshed when another host starts in the same process (as the integration tests do), so
    /// Hangfire keeps creating loggers from a disposed factory and throws
    /// <see cref="ObjectDisposedException"/> for <c>LoggerFactory</c> - which surfaces as failures
    /// to enqueue or start jobs. Job activation itself is not rebound here: Hangfire resolves the
    /// job activator per host from DI, so job targets - including feature handlers invoked directly
    /// - already get their dependencies from their own host's container.
    /// </para>
    /// </summary>
    private static void RebindHangfireGlobals(IServiceProvider services)
    {
        HangfireHostServices.Current = services;

        // Resolving Hangfire's DI services runs AddHangfire's configuration action, which writes the
        // process-wide GlobalConfiguration. Force that to happen first so the rebinding below is not
        // overwritten immediately afterwards.
        _ = services.GetRequiredService<IGlobalConfiguration>();

        GlobalConfiguration.Configuration.UseLogProvider(new HostScopedLogProvider());

        JobStorage.Current = services.GetRequiredService<JobStorage>();
    }

    /// <summary>
    /// Holds the most recently started host's service provider so Hangfire's process-wide log
    /// provider can resolve against a live container instead of one captured at first configuration.
    /// </summary>
    private static class HangfireHostServices
    {
        public static IServiceProvider? Current { get; set; }
    }

    /// <summary>
    /// Bridges Hangfire's internal logging onto the current host's logger factory, degrading to no
    /// logging (rather than throwing) if that host is torn down while Hangfire is still logging.
    /// </summary>
    private sealed class HostScopedLogProvider : Hangfire.Logging.ILogProvider
    {
        public Hangfire.Logging.ILog GetLogger(string name) => new HostScopedLog(name);
    }

    private sealed class HostScopedLog(string name) : Hangfire.Logging.ILog
    {
        public bool Log(Hangfire.Logging.LogLevel logLevel, Func<string>? messageFunc, Exception? exception = null)
        {
            var logger = TryGetLogger();
            if (logger is null)
            {
                return false;
            }

            var level = logLevel switch
            {
                Hangfire.Logging.LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
                Hangfire.Logging.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
                Hangfire.Logging.LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
                Hangfire.Logging.LogLevel.Warn => Microsoft.Extensions.Logging.LogLevel.Warning,
                Hangfire.Logging.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                Hangfire.Logging.LogLevel.Fatal => Microsoft.Extensions.Logging.LogLevel.Critical,
                _ => Microsoft.Extensions.Logging.LogLevel.None
            };

            // Hangfire probes for enabled levels by passing a null message factory.
            if (messageFunc is null)
            {
                return logger.IsEnabled(level);
            }

            try
            {
                logger.Log(level, exception, "{Message}", messageFunc());
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            return true;
        }

        private ILogger? TryGetLogger()
        {
            try
            {
                return HangfireHostServices.Current?.GetRequiredService<ILoggerFactory>().CreateLogger(name);
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }
    }
}
