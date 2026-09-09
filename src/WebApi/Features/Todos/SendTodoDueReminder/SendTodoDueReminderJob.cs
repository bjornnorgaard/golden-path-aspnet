using WebApi.Annotations;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.SendTodoDueReminder;

/// <summary>
/// Hangfire is a transport, just like the OpenAPI endpoints and GraphQL resolvers: it only
/// dispatches to a feature handler and must not contain business logic itself. This is a Hangfire
/// job target, so it must be resolvable from a plain DI scope (Hangfire creates one per job
/// execution).
/// </summary>
[Service(ServiceLifetime.Transient)]
internal sealed class SendTodoDueReminderJob(SendTodoDueReminderHandler handler)
{
    /// <summary>
    /// Delayed job: scheduled to run at a todo's due date when the todo is created with one, so a
    /// single reminder fires once the deadline arrives.
    /// </summary>
    public Task InvokeAsync(TodoId todoId, CancellationToken ct) =>
        handler.HandleAsync(new SendTodoDueReminderHandler.Command
        {
            TodoId = todoId
        }, ct);
}
