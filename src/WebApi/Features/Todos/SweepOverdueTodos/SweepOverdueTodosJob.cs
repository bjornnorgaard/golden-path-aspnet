using WebApi.Annotations;

namespace WebApi.Features.Todos.SweepOverdueTodos;

/// <summary>
/// Hangfire is a transport, just like the OpenAPI endpoints and GraphQL resolvers: it only
/// dispatches to a feature handler and must not contain business logic itself. This is a Hangfire
/// job target, so it must be resolvable from a plain DI scope (Hangfire creates one per job
/// execution).
/// </summary>
[Service(ServiceLifetime.Transient)]
internal sealed class SweepOverdueTodosJob(SweepOverdueTodosHandler handler)
{
    /// <summary>
    /// Recurring job: runs on a schedule (see <see cref="Configurations.HangfireConfiguration"/>)
    /// to sweep for todos that became overdue without ever being explicitly scheduled.
    /// </summary>
    public Task InvokeAsync(CancellationToken ct) => handler.HandleAsync(ct);
}
