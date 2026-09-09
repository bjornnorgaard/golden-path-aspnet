using WebApi.Annotations;
using TodoId = WebApi.Database.Models.TodoId;

namespace WebApi.Features.Todos.SendTodoCompletedNotification;

/// <summary>
/// Hangfire is a transport, just like the OpenAPI endpoints and GraphQL resolvers: it only
/// dispatches to a feature handler and must not contain business logic itself. This is a Hangfire
/// job target, so it must be resolvable from a plain DI scope (Hangfire creates one per job
/// execution).
/// </summary>
[Service(ServiceLifetime.Transient)]
internal sealed class SendTodoCompletedNotificationJob(SendTodoCompletedNotificationHandler handler)
{
    /// <summary>
    /// Fire-and-forget job: enqueued right after a todo is toggled complete, so the request
    /// thread doesn't wait on a simulated notification side effect.
    /// </summary>
    public Task InvokeAsync(TodoId todoId, CancellationToken ct) =>
        handler.HandleAsync(new SendTodoCompletedNotificationHandler.Command
        {
            TodoId = todoId
        }, ct);
}
